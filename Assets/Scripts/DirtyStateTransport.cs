using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using ENet;
using UnityEngine;
using BitStream = BitStreams.BitStream;

public class DirtyStateTransport : MonoBehaviour
{
    private readonly float InputSendRate = 10;
    private readonly float StateSendRate = 5;
    
    public Server Server { get; private set; }
    public  Client Client { get; private set; }

    public static float LerpT = 0;
    
    private static ushort currentPacketId = 1;
    private static ushort latestPacketReceived = 0;

    private float _timer = 0;
    
    //Temp?
    public GameObject PlayerPrefab;
    
    private Dictionary<ushort, GameObject> NetworkedObjects = new Dictionary<ushort, GameObject>();

    public void StartServer()
    {
        Server = new Server();
        Server.PeerConnected += ServerOnPeerConnected;
        Server.PeerDisconncted += ServerOnPeerDisconncted;
        Server.PacketReceived += ServerOnPacketReceived;
        
        Server.Listen(Constants.DefaultPort);
    }

    private void ServerOnPacketReceived(byte[] data, uint senderId)
    {
        InputPacket inputPacket = InputPacket.Deserialize(data);
        InputCompressor.Inputs inputs = InputCompressor.DecompressInput(inputPacket.inputByte);
        
        // GAME LOGIC BEGIN, MOVE TO NEW AREA LATER
        int horizontal = Convert.ToSByte(inputs.D) - Convert.ToSByte(inputs.A);
        int vertical = Convert.ToSByte(inputs.W) - Convert.ToSByte(inputs.S);
        // update state
        NetworkEntity2 entity = NetworkState.LatestEntityDict[(ushort)senderId];
        entity.Position += new Vector3(horizontal, 0, vertical) * (1 / InputSendRate);
    }

    private void ServerOnPeerDisconncted(uint id)
    {
        StatePacket disconnectPacket;
        disconnectPacket.packetType = StatePacket.PacketType.Disconnect;
        disconnectPacket.id = (ushort)id;
        Server.BroadcastBytes(disconnectPacket.Serialize());
        
        // Destroy the networked object
        Destroy(NetworkedObjects[(ushort)id]);
        NetworkedObjects.Remove((ushort) id);
        
        NetworkState.LatestEntityDict.Remove((ushort)id);
    }

    private void ServerOnPeerConnected(uint id)
    {
        GameObject obj = Instantiate(PlayerPrefab);
        obj.GetComponent<FollowState>().Id = (ushort)id;
        NetworkedObjects[(ushort) id] = obj;
        
        NetworkEntity2 entity = new NetworkEntity2()
        {
            id = (ushort) id,
            Position = obj.transform.position,
            Rotation = obj.transform.rotation
        };
        NetworkState.LatestEntityDict[(ushort)id] = entity;

        StatePacket connectPacket;
        connectPacket.packetType = StatePacket.PacketType.Connect;
        connectPacket.id = (ushort)id;
        // Broadcast the connection to all peers except for the peer that is connecting
        // They will receive their Instantiation in their initial state packet
        Server.BroadcastBytesToEveryoneExcept(connectPacket.Serialize(), id);

        StatePacket initialStatePacket;
        initialStatePacket.packetType = StatePacket.PacketType.InitialState;
        initialStatePacket.id = currentPacketId;
        Server.BroadcastBytesTo(initialStatePacket.Serialize(), id);
    }

    public void StartClient()
    {
        Client = new Client();
        Client.PacketReceived += ClientOnPacketReceived;
        
        Client.Connect(Constants.DefaultHost, Constants.DefaultPort);
    }

    private void ClientOnPacketReceived(byte[] data)
    {
        ushort id = StatePacket.GetId(data);

        GameObject obj;
        switch (StatePacket.DeserializeType(data))
        {
            case StatePacket.PacketType.State:
                StatePacket.UpdateNetworkState(latestPacketReceived, data);

                LerpT = 0;
                break;
            case StatePacket.PacketType.InitialState:
                StatePacket.UpdateNetworkState(0, data);
                foreach (var pair in NetworkState.LatestEntityDict)
                {
                    obj = Instantiate(PlayerPrefab, pair.Value.Position, pair.Value.Rotation);
                    obj.GetComponent<FollowState>().Id = pair.Key;
                    NetworkedObjects[pair.Key] = obj;
                }
                break;
            case StatePacket.PacketType.Connect:
                // Get the new entities data from packet
                StatePacket.UpdateNetworkEntity(data);
                // Create the player's object
                obj = Instantiate(PlayerPrefab);
                obj.GetComponent<FollowState>().Id = id;
                NetworkedObjects[id] = obj;
                break;
            case StatePacket.PacketType.Disconnect:
                Destroy(NetworkedObjects[id]);
                NetworkedObjects.Remove(id);
                break;
        }
    }

    private void LateUpdate()
    {
        Server?.PollEvents();
        Client?.PollEvents();

        _timer += Time.deltaTime;
        if (Server != null)
        {
            if (_timer >= 1 / StateSendRate)
            {
                _timer = 0;

                StatePacket statePacket;
                statePacket.packetType = StatePacket.PacketType.State;
                statePacket.id = currentPacketId++;
                Server.BroadcastBytes(statePacket.Serialize());
            }
        }
        else if (Client != null)
        {
            if (_timer >= 1 / InputSendRate)
            {
                _timer = 0;
                
                InputPacket inputPacket = InputPacket.ComposePacket(currentPacketId++);
                Client.SendBytes(inputPacket.Serialize());
            }

            LerpT += Time.deltaTime * StateSendRate;
        }
    }
    
    private void OnApplicationQuit()
    {
        Server?.Disconnect();
        Client?.Disconnect();
    }

    private struct StatePacket
    {
        public enum PacketType : byte {Connect, Disconnect, State, InitialState}

        public PacketType packetType;
        public ushort id;

        public byte[] Serialize()
        {
            byte[] data = new byte[1]; // The Stream will increase this as needed *lazy...*
            BitStream stream = BitStream.Create(data);
            stream.AutoIncreaseStream = true;

            stream.WriteByte((byte)packetType);
            stream.WriteUInt16(id);
            switch (packetType)
            {
                case PacketType.State:
                    NetworkState.Serialize(stream);
                    break;
                case PacketType.InitialState:
                    NetworkState.Serialize(stream, true);
                    break;
                case PacketType.Connect:
                    NetworkEntity2 newEntity = NetworkState.LatestEntityDict[id];
                    newEntity.Serialize(stream, true);
                    break;
            }
            
            return stream.CloneAsMemoryStream().ToArray();
        }

        // Grab the packet type off the first byte
        public static PacketType DeserializeType(byte[] data)
        {
            return (PacketType)data[0];
        }

        public static ushort GetId(byte[] data)
        {
            // Skip over the first type byte
            return BitConverter.ToUInt16(data, 1);
        }

        // Used for connect packets, to snap an entity to its initial transform
        public static void UpdateNetworkEntity(byte[] data)
        {
            BitStream stream  = new BitStream(data);
            stream.Seek(1, 0);
            ushort id = stream.ReadUInt16();
            NetworkEntity2 entity = NetworkEntity2.Deserialize(stream);

            NetworkState.PreviousEntityDict[id] = entity;
            NetworkState.LatestEntityDict[id] = entity;
        }

        // Update the network state
        public static void UpdateNetworkState(ushort latestPacket, byte[] data)
        {
            BitStream stream  = new BitStream(data);
            // Skip the first type byte
            stream.Seek(1, 0);
            ushort id = stream.ReadUInt16();
            if (id > latestPacket)
                NetworkState.Deserialize(stream);
        }
    }

    private struct InputPacket
    {
        public ushort id;
        public byte inputByte;

        public static InputPacket Deserialize(byte[] data)
        {
            InputPacket returnPacket;
            returnPacket.id = BitConverter.ToUInt16(data, 0);
            returnPacket.inputByte = data[2];
            return returnPacket;
        }

        public byte[] Serialize()
        {
            byte[] bytes = new byte[5];
            Buffer.BlockCopy(BitConverter.GetBytes(id), 0, bytes, 0, 2);
            bytes[2] = inputByte;
            return bytes;
        }

        public static InputPacket ComposePacket(ushort currentPacketId)
        {
            InputCompressor.Inputs inputs;
            inputs.W = Input.GetKey(KeyCode.W);
            inputs.A = Input.GetKey(KeyCode.A);
            inputs.S = Input.GetKey(KeyCode.S);
            inputs.D = Input.GetKey(KeyCode.D);
            inputs.Space = Input.GetKey(KeyCode.Space);

            InputPacket inputPacket;
            inputPacket.id = currentPacketId;
            inputPacket.inputByte = InputCompressor.CompressInput(inputs);
            return inputPacket;
        }
    }
}
