using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using ENet;
using UnityEngine;
using BitStream = BitStreams.BitStream;

public class DirtyStateTransport : MonoBehaviour
{
    public static readonly float InputSendRate = 20;
    public static readonly float StateSendRate = 5;
    public static readonly float RotationDifferenceAngleThreshold = 0.1f;

    // Happens right before the input packet is created then sent, ushort is the latestPacketReceived
    public static event Action<ushort> PreClientInputSend;
    public static event Action<ushort> PostClientInputSend;
    // Triggers when the client receives a state from the server, to validate client side prediction
    public static event Action<ushort, Vector3> ServerPositionReceived; 

    public Server Server { get; private set; }
    public  Client Client { get; private set; }

    public static float LerpT = 0;
    
    private static ushort currentPacketId = 1;
    private static ushort latestPacketReceived = 0;

    private float _timer = 0;
    
    //Temp?
    public GameObject PlayerPrefab;
    
    private Dictionary<ushort, GameObject> NetworkedObjects = new Dictionary<ushort, GameObject>();

    // SERVER ONLY
    // Keeps track of the last input packet that each client sent
    private Dictionary<ushort, ClientInputRecord> ClientInputHistory = new Dictionary<ushort, ClientInputRecord>();
    // Keeps track of all the apple pickings and spawnings, is reset after it is sent out
    private AppleState _appleState = AppleState.InitialPacket();
    
    // CLIENT ONLY
    private ushort _clientOwnedId;
    private GameObject _clientOwnedObject;
    private Transform _clientOwnedCamera;
    private bool _clientInitialized;
    // Reference to the previous input packet, to determine if rotation is dirty
    private InputPacket _previousInputPacket = InputPacket.InitialPacket();

    public void StartServer()
    {
        Server = new Server();
        Server.PeerConnected += ServerOnPeerConnected;
        Server.PeerDisconncted += ServerOnPeerDisconncted;
        Server.PacketReceived += ServerOnPacketReceived;
        
        TreeBehavior.ApplePicked += TreeBehaviorOnApplePicked;
        
        Server.Listen(Constants.DefaultPort);
    }
    
    // Event is raised by a tree when one of its apples has been picked
    private void TreeBehaviorOnApplePicked(byte treeId, byte appleId)
    {
        AppleState.AppleStateEntry entry;
        entry.picked = true;
        entry.appleId = appleId;
        entry.treeId = treeId;
        _appleState.changes.Add(entry);
    }

    private void ServerOnPacketReceived(byte[] data, uint senderId)
    {
        // How many frames worth of movement to apply to the entity
        // If the packet is old, none, if this is the next packet, 1
        // If we detect there was a gap in packets, double, etc
        int multiplier = 0;
        
        InputPacket inputPacket = InputPacket.Deserialize(data, ClientInputHistory[(ushort)senderId]);
        InputCompressor.Inputs inputs = InputCompressor.DecompressInput(inputPacket.inputByte);

        Transform ownerTransform = NetworkedObjects[(ushort) senderId].transform;

        // Check it against the client input history
        if (ClientInputHistory.ContainsKey((ushort) senderId))
        {
            // Check to see if this input is newer than the newest one we know of
            if (inputPacket.id > ClientInputHistory[(ushort) senderId].packetId)
            {
                multiplier = inputPacket.id - ClientInputHistory[(ushort) senderId].packetId;
                
                // Put the record into the dictionary
                ClientInputRecord inputRecord;
                inputRecord.packetId = inputPacket.id;
                inputRecord.inputs = inputs;
                inputRecord.rotation = inputPacket.rotation;
                ClientInputHistory[(ushort) senderId] = inputRecord;
            }
            else
            {
                multiplier = 0;
                Debug.Log("Server received out of order client input packet");
            }
        }
        else
        {
            multiplier = 1;
            
            // Put the record into the dictionary
            ClientInputRecord inputRecord;
            inputRecord.packetId = inputPacket.id;
            inputRecord.inputs = inputs;
            inputRecord.rotation = inputPacket.rotation;
            ClientInputHistory[(ushort) senderId] = inputRecord;
        }
        
        // GAME LOGIC BEGIN, MOVE TO NEW AREA LATER
        int horizontal = Convert.ToSByte(inputs.D) - Convert.ToSByte(inputs.A);
        int vertical = Convert.ToSByte(inputs.W) - Convert.ToSByte(inputs.S);
        // update state
        NetworkEntity2 entity = NetworkState.LatestEntityDict[(ushort)senderId];

        if (inputPacket.dirtyRotation)
        {
            entity.Rotation = inputPacket.rotation;
        }

        // Full up and down rotations are passed, we only want to transform our inputs by the y axis of the rotation
        Quaternion yRotation = Quaternion.Euler(0, entity.Rotation.eulerAngles.y, 0);
        // Calculate the delta of their position
        Vector3 delta = yRotation * new Vector3(horizontal, 0, vertical) * multiplier * (1 / InputSendRate);
        // Move in that direction while simulating collisions
        NetworkedObjects[(ushort) senderId].GetComponent<CharacterController>().Move(delta);
        // Update the NetworkState to this new position
        entity.Position = ownerTransform.position;
        
        // If E was used, raycast for interactions
        if (inputs.E)
        {
            RaycastHit hit;
            Debug.DrawRay(ownerTransform.position + Vector3.up, entity.Rotation * Vector3.forward * 1.5f, Color.red, 2f);
            if (Physics.Raycast(ownerTransform.position + Vector3.up, entity.Rotation * Vector3.forward, out hit, 1.5f))
            {
                AppleBehavior apple = hit.collider.GetComponent<AppleBehavior>();
                if (apple)
                {
                    Debug.Log("Hit");
                    apple.Use();
                }
            }
        }
    }

    private void ServerOnPeerDisconncted(uint id)
    {
        StatePacket disconnectPacket = new StatePacket();
        disconnectPacket.packetType = StatePacket.PacketType.Disconnect;
        disconnectPacket.id = (ushort)id;
        Server.BroadcastBytes(disconnectPacket.Serialize());
        
        // Destroy the networked object
        Destroy(NetworkedObjects[(ushort)id]);
        NetworkedObjects.Remove((ushort) id);
        // Unregister from client input history
        ClientInputHistory.Remove((ushort) id);
        
        NetworkState.LatestEntityDict.Remove((ushort)id);
    }

    private void ServerOnPeerConnected(uint id)
    {
        GameObject obj = Instantiate(PlayerPrefab);
        FollowState followState = obj.GetComponent<FollowState>();
        followState.Id = (ushort)id;
        followState.IsServer = true;
        NetworkedObjects[(ushort) id] = obj;

        NetworkEntity2 entity = new NetworkEntity2()
        {
            id = (ushort) id,
            Position = obj.transform.position,
            Rotation = obj.transform.rotation
        };
        NetworkState.LatestEntityDict[(ushort)id] = entity;

        StatePacket connectPacket = new StatePacket();
        connectPacket.packetType = StatePacket.PacketType.Connect;
        connectPacket.id = (ushort)id;
        // Broadcast the connection to all peers except for the peer that is connecting
        // They will receive their Instantiation in their initial state packet
        Server.BroadcastBytesToEveryoneExcept(connectPacket.Serialize(), id);

        StatePacket initialStatePacket = new StatePacket();
        initialStatePacket.packetType = StatePacket.PacketType.InitialState;
        initialStatePacket.id = currentPacketId;
        initialStatePacket.clientId = (ushort) id;
        Server.BroadcastBytesTo(initialStatePacket.Serialize(), id);
        
        ClientInputHistory[(ushort)id] = ClientInputRecord.InitialRecord();
    }

    public void StartClient(string host, uint playerId)
    {
        Client = new Client();
        Client.PacketReceived += ClientOnPacketReceived;

        Client.Connected += () => {  };
        Client.Connect(host, Constants.DefaultPort, playerId);
    }

    private void ClientOnPacketReceived(byte[] data)
    {
        ushort id = StatePacket.GetId(data);

        StatePacket.PacketType packetType = StatePacket.DeserializeType(data);

        GameObject obj;
        switch (packetType)
        {
            case StatePacket.PacketType.State:
                StatePacket.UpdateNetworkState(latestPacketReceived, data);
                
                // Grab the new position of this client's entity and invoke the event to validate client side prediction
                Vector3 clientOwnedPosition = NetworkState.LatestEntityDict[_clientOwnedId].Position;
                ServerPositionReceived?.Invoke(id, clientOwnedPosition);
                LerpT = 0;
                break;
            case StatePacket.PacketType.InitialState:
                _clientOwnedId = StatePacket.GetClientId(data);
                
                StatePacket.UpdateNetworkState(0, data);
                foreach (var pair in NetworkState.LatestEntityDict)
                {
                    obj = Instantiate(PlayerPrefab, pair.Value.Position, pair.Value.Rotation);
                    obj.GetComponent<FollowState>().Id = pair.Key;
                    NetworkedObjects[pair.Key] = obj;

                    if (pair.Key == _clientOwnedId)
                    {
                        obj.AddComponent<ClientInput>();
                    }
                }
                
                _clientOwnedObject = NetworkedObjects[_clientOwnedId];
                // Set the entity that this client owns so that client side prediction knows who to affect
                _clientOwnedObject.GetComponent<FollowState>().SetOwner();
                // Disable rendering of the player model for the owner client
                _clientOwnedObject.GetComponent<DisableRendering>().Disable();

                // Grab the client's camera (the only camera in the scene)
                _clientOwnedCamera = Camera.main.transform;
                // Client is now initialized
                _clientInitialized = true;
                break;
            case StatePacket.PacketType.Connect:
                // Get the new entities data from packet
                StatePacket.DeserializeConnectState(data);
                // Create the player's object
                obj = Instantiate(PlayerPrefab);
                obj.GetComponent<FollowState>().Id = id;
                NetworkedObjects[id] = obj;
                break;
            case StatePacket.PacketType.Disconnect:
                Destroy(NetworkedObjects[id]);
                NetworkedObjects.Remove(id);
                break;
            case StatePacket.PacketType.AppleState:
                AppleState appleState = AppleState.Deserialize(data);
                foreach (var change in appleState.changes)
                {
                    if (change.picked)
                        TreeBehavior.Trees[change.treeId].HideApple(change.appleId);
                }

                break;
        }
        
        if (packetType == StatePacket.PacketType.State || packetType == StatePacket.PacketType.InitialState)
        {
            // Update the latest packet received to the id of the received packet if this is some sort of state packet
            // the connect and disconnect packet id's are different, they tell us the id of the peer
            latestPacketReceived = id;
        }
    }

    private void LateUpdate()
    {
        Server?.PollEvents();
        

        _timer += Time.deltaTime;
        if (Server != null)
        {
            if (_timer >= 1 / StateSendRate)
            {
                _timer = 0;

                StatePacket statePacket = new StatePacket();
                statePacket.packetType = StatePacket.PacketType.State;
                statePacket.id = currentPacketId++;
                Server.BroadcastBytes(statePacket.Serialize());
                
                // Tell everyone the apple state changes we have collected then reset
                Server.BroadcastBytes(_appleState.Serialize());
                _appleState = AppleState.InitialPacket();
            }
        }
        else if (Client != null && _clientInitialized)
        {
            if (_timer >= 1 / InputSendRate)
            {
                _timer = 0;

                // Client side prediction
                PreClientInputSend?.Invoke(latestPacketReceived);
                
                // Grab the inputs recorder by Client input and send them
                InputCompressor.Inputs inputs = _clientOwnedObject.GetComponent<ClientInput>().ClientInputs;
                InputPacket inputPacket = InputPacket.ComposePacket(currentPacketId, inputs, _clientOwnedCamera.transform.rotation);

                Client.SendBytes(inputPacket.Serialize(_previousInputPacket));
                ++currentPacketId;
                
                // Keep this packet so we can tell if there was a rotation change next time we make a packet
                _previousInputPacket = inputPacket;
                
                PostClientInputSend?.Invoke(latestPacketReceived);

                // DISABLED FOR CLIENT SIDE PREDICTION
                // InputPacket inputPacket = InputPacket.ComposePacket(currentPacketId++);
                // Client.SendBytes(inputPacket.Serialize());
            }

            LerpT += Time.deltaTime * StateSendRate;
        }
        
        Client?.PollEvents();
    }
    
    private void OnApplicationQuit()
    {
        Server?.Disconnect();
        Client?.Disconnect();
    }

    private class StatePacket
    {
        public enum PacketType : byte {Connect, Disconnect, State, InitialState, AppleState}

        public PacketType packetType;
        public ushort id;
        // Only used for Initial state packet, it is the peer ID of the client
        public ushort clientId;

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
                    stream.WriteUInt16(clientId);
                    NetworkState.Serialize(stream, true);
                    break;
                case PacketType.Connect:
                    NetworkEntity2 newEntity = NetworkState.LatestEntityDict[id];
                    newEntity.Serialize(stream, true);
                    break;
            }
            
            return stream.GetStreamData();
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

        public static ushort GetClientId(byte[] data)
        {
            return BitConverter.ToUInt16(data, 3);
        }

        // Used for connect packets, to snap an entity to its initial transform
        public static void DeserializeConnectState(byte[] data)
        {
            BitStream stream  = new BitStream(data);
            PacketType packetType = (PacketType) stream.ReadByte();
            ushort id = stream.ReadUInt16();
            
            NetworkEntity2 entity = NetworkEntity2.Deserialize(stream);

            NetworkState.PreviousEntityDict[id] = entity;
            NetworkState.LatestEntityDict[id] = entity;
        }

        // Update the network state
        public static void UpdateNetworkState(ushort latestPacket, byte[] data)
        {
            BitStream stream  = new BitStream(data);
            PacketType packetType = (PacketType) stream.ReadByte();
            ushort id = stream.ReadUInt16();
            
            // if this is the initial state, there is an extra ushort in there we need to skip over
            // This sucks and State Packet needs to be redesigned to handle different packet type
            // with different data
            if (packetType == PacketType.InitialState)
                stream.ReadUInt16();
            
            if (id > latestPacket)
            {
                NetworkState.Deserialize(stream);
            }
        }
    }

    private struct InputPacket
    {
        public ushort id;
        public byte inputByte;
        public bool dirtyRotation;
        public Quaternion rotation;

        public static InputPacket InitialPacket()
        {
            InputPacket inputPacket;
            inputPacket.id = 0;
            inputPacket.inputByte = 0;
            inputPacket.dirtyRotation = false;
            inputPacket.rotation = Quaternion.identity;
            
            return inputPacket;
        }

        public static InputPacket Deserialize(byte[] data, ClientInputRecord old)
        {
            InputPacket returnPacket;
            BitStream stream = new BitStream(data);
            returnPacket.id = stream.ReadUInt16();
            returnPacket.inputByte = stream.ReadByte();

            // Rotation
            bool dirty = stream.ReadBit();
            if (dirty)
            {
                returnPacket.dirtyRotation = true;
                
                float x = FloatQuantize.UnQunatizeFloat(stream.ReadInt16(), 32767);
                float y = FloatQuantize.UnQunatizeFloat(stream.ReadInt16(), 32767);
                float z = FloatQuantize.UnQunatizeFloat(stream.ReadInt16(), 32767);
                float w = FloatQuantize.UnQunatizeFloat(stream.ReadInt16(), 32767);
                returnPacket.rotation = new Quaternion(x, y, z, w);
            }
            else
            {
                returnPacket.dirtyRotation = false;
                returnPacket.rotation = old.rotation;
            }
            
            return returnPacket;
        }

        public byte[] Serialize(InputPacket old)
        {
            byte[] bytes = new byte[1];
            BitStream stream = new BitStream(bytes);
            stream.AutoIncreaseStream = true;
            
            stream.WriteUInt16(id);
            stream.WriteByte(inputByte);
            
            // Rotation uses a dirty bit to signify if the rotation has changed
            if (dirtyRotation || Quaternion.Angle(rotation, old.rotation) < RotationDifferenceAngleThreshold)
            {
                // dirty bit
                stream.WriteBit(0);
            }
            else
            {
                // dirty bit
                stream.WriteBit(1);
                stream.WriteInt16(FloatQuantize.QuantizeFloat(rotation.x, 32767));
                stream.WriteInt16(FloatQuantize.QuantizeFloat(rotation.y, 32767));
                stream.WriteInt16(FloatQuantize.QuantizeFloat(rotation.z, 32767));
                stream.WriteInt16(FloatQuantize.QuantizeFloat(rotation.w, 32767));
            }
            
            return stream.GetStreamData();
        }

        public static InputPacket ComposePacket(ushort currentPacketId, InputCompressor.Inputs inputs, Quaternion rotation)
        {
            InputPacket inputPacket;
            inputPacket.id = currentPacketId;
            inputPacket.inputByte = InputCompressor.CompressInput(inputs);
            inputPacket.dirtyRotation = false;
            inputPacket.rotation = rotation;
            return inputPacket;
        }
    }

    private struct ClientInputRecord
    {
        public ushort packetId;
        public InputCompressor.Inputs inputs;
        public Quaternion rotation;

        public static ClientInputRecord InitialRecord()
        {
            ClientInputRecord record;
            record.packetId = 0;
            record.inputs = new InputCompressor.Inputs();
            record.rotation = Quaternion.identity;

            return record;
        }
    }

    private struct AppleState
    {
        private StatePacket.PacketType type;
        public List<AppleStateEntry> changes;
        public struct AppleStateEntry
        {
            // picked vs spawned
            public bool picked;
            public byte treeId;
            public byte appleId;
        }

        public static AppleState InitialPacket()
        {
            AppleState appleState;
            appleState.type = StatePacket.PacketType.AppleState;
            appleState.changes = new List<AppleStateEntry>();
            return appleState;
        }
        
        public byte[] Serialize()
        {
            byte[] bytes = new byte[1];
            BitStream stream = new BitStream(bytes);
            stream.AutoIncreaseStream = true;
            
            stream.WriteByte((byte)type);
            stream.WriteUInt16((ushort)changes.Count);
            foreach (var change in changes)
            {
                stream.WriteBit(change.picked);
                stream.WriteByte(change.treeId);
                stream.WriteByte(change.appleId);
            }

            return stream.GetStreamData();
        }

        public static AppleState Deserialize(byte[] data)
        {
            BitStream stream = new BitStream(data);
            ushort type = stream.ReadByte();
            ushort length = stream.ReadUInt16();
            List<AppleStateEntry> changes = new List<AppleStateEntry>();
            for (int i = 0; i < length; i++)
            {
                AppleStateEntry entry;
                entry.picked = stream.ReadBit();
                entry.treeId = stream.ReadByte();
                entry.appleId = stream.ReadByte();
                
                changes.Add(entry);
            }

            AppleState appleState;
            appleState.type = StatePacket.PacketType.AppleState;
            appleState.changes = changes;
            return appleState;
        }
    }
}
