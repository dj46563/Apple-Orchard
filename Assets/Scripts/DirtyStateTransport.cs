using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using ENet;
using UnityEngine;
using UnityEngine.Networking;
using Utils;
using BitStream = BitStreams.BitStream;

public class DirtyStateTransport : MonoBehaviour
{
    public static readonly float InputSendRate = 20;
    public static readonly float StateSendRate = 5;
    // How much of a difference in rotation warrants a packet updating the rotation
    public static readonly float RotationDifferenceAngleThreshold = 0.1f;
    // How often should the database gets updated
    public static readonly float AppleDatabaseUpdateTime = 10f;

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
    private Dictionary<ushort, string> EntityNames = new Dictionary<ushort, string>();
    private Dictionary<ushort, ushort> EntityApples = new Dictionary<ushort, ushort>();

    // SERVER ONLY
    // Keeps track of the last input packet that each client sent
    private Dictionary<ushort, InputPacket> _clientInputHistory = new Dictionary<ushort, InputPacket>();
    // Keeps track of the last delta that we've calculated for this client
    private Dictionary<ushort, Vector3> _clientDeltaHistory = new Dictionary<ushort, Vector3>();
    // Keeps track of whos apple counts need to be updated in the database, key is the network id, value is the name and apples
    private Dictionary<ushort, Tuple<string, ushort>> ApplePickers = new Dictionary<ushort, Tuple<string, ushort>>();
    // Keeps track of all the apple pickings and spawnings, is reset after it is sent out
    private AppleState _appleState = AppleState.InitialPacket();
    private float _dbUpdateTimer = 0;
    
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
        NetworkEntity2.EntityApplesUpdate += UpdateNetworkEntityNametag;
        
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
        
        InputPacket inputPacket = InputPacket.Deserialize(data, _clientInputHistory[(ushort)senderId]);
        InputCompressor.Inputs inputs = InputCompressor.DecompressInput(inputPacket.inputByte);

        Transform ownerTransform = NetworkedObjects[(ushort) senderId].transform;

        // EARLY RETURN if this packet is older than another input packet that we've received
        if (inputPacket.id <= _clientInputHistory[(ushort) senderId].id)
        {
            return;
        }
        
        multiplier = inputPacket.id - _clientInputHistory[(ushort) senderId].id;
        _clientInputHistory[(ushort) senderId] = inputPacket;

        // // Check it against the client input history
        // if (ClientInputHistory.ContainsKey((ushort) senderId))
        // {
        //     // Check to see if this input is newer than the newest one we know of
        //     if (inputPacket.id > ClientInputHistory[(ushort) senderId].packetId)
        //     {
        //         multiplier = inputPacket.id - ClientInputHistory[(ushort) senderId].packetId;
        //         
        //         // Put the record into the dictionary
        //         ClientInputRecord inputRecord;
        //         inputRecord.packetId = inputPacket.id;
        //         inputRecord.inputs = inputs;
        //         inputRecord.rotation = inputPacket.rotation;
        //         ClientInputHistory[(ushort) senderId] = inputRecord;
        //     }
        //     else
        //     {
        //         multiplier = 0;
        //         Debug.Log("Server received out of order client input packet");
        //     }
        // }
        // else
        // {
        //     multiplier = 1;
        //     
        //     // Put the record into the dictionary
        //     ClientInputRecord inputRecord;
        //     inputRecord.packetId = inputPacket.id;
        //     inputRecord.inputs = inputs;
        //     inputRecord.rotation = inputPacket.rotation;
        //     ClientInputHistory[(ushort) senderId] = inputRecord;
        // }
        
        
        // update state
        NetworkEntity2 entity = NetworkState.LatestEntityDict[(ushort)senderId];

        if (inputPacket.dirtyRotation)
        {
            entity.Rotation = inputPacket.rotation;
        }

        CharacterController cc = NetworkedObjects[(ushort) senderId].GetComponent<CharacterController>();
        Vector3 previousDelta = _clientDeltaHistory.ContainsKey((ushort) senderId)
            ? _clientDeltaHistory[(ushort) senderId]
            : Vector3.zero;
        // Calculate the new delta of their position with the inputs the client has sent us
        Vector3 delta = MovementLogic.CalculateDelta(inputs, entity.Rotation, previousDelta, cc.isGrounded, multiplier);
        
        // Move in that direction while simulating collisions
        cc.Move(delta);
        // Update the NetworkState to this new position
        entity.Position = ownerTransform.position;

        _clientDeltaHistory[(ushort) senderId] = delta;
        
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
                    ushort applesPicked = NetworkState.EntityPickedApple((ushort)senderId);
                    apple.Use();
                    ApplePickers[(ushort)senderId] = new Tuple<string, ushort>(EntityNames[(ushort)senderId], applesPicked);
                    UpdateNetworkEntityNametag((ushort)senderId, applesPicked);
                }
            }
        }
    }

    private void ServerOnPeerDisconncted(uint id)
    {
        StatePacket disconnectPacket = new StatePacket();
        disconnectPacket.packetType = StatePacket.PacketType.Disconnect;
        disconnectPacket.id = (ushort)id;
        Server.BroadcastBytes(disconnectPacket.Serialize(null));
        
        // Destroy the networked object
        Destroy(NetworkedObjects[(ushort)id]);
        NetworkedObjects.Remove((ushort) id);
        // Unregister from client input history
        _clientInputHistory.Remove((ushort) id);
        
        NetworkState.LatestEntityDict.Remove((ushort)id);
    }

    private void ServerOnPeerConnected(uint id, Client.ConnectData connectData)
    {
        // Get their player info using their db player id
        // Put it into their nametag
        // Wait to do pretty much everything until the info comes back
        PlayerInfo.GetPlayerInfo(connectData.hash, info =>
        {
            GameObject obj = Instantiate(PlayerPrefab);
            FollowState followState = obj.GetComponent<FollowState>();
            followState.Id = (ushort)id;
            followState.IsServer = true;
            NetworkedObjects[(ushort) id] = obj;
            
            obj.GetComponentInChildren<TextMesh>().text = info.username + ": " + info.apples;
            
            NetworkEntity2 entity = new NetworkEntity2()
            {
                id = (ushort) id,
                Position = obj.transform.position,
                Rotation = obj.transform.rotation
            };
            NetworkState.LatestEntityDict[(ushort)id] = entity;
            
            // Set the name and apples of this entity
            NetworkState.SetEntityNameAndApples((ushort)id, info.username, (ushort)info.apples);

            StatePacket connectPacket = new StatePacket();
            connectPacket.packetType = StatePacket.PacketType.Connect;
            connectPacket.id = (ushort)id;
            // Broadcast the connection to all peers except for the peer that is connecting
            // They will receive their Instantiation in their initial state packet
            Server.BroadcastBytesToEveryoneExcept(connectPacket.Serialize(null), id);

            StatePacket initialStatePacket = new StatePacket();
            initialStatePacket.packetType = StatePacket.PacketType.InitialState;
            initialStatePacket.id = currentPacketId;
            initialStatePacket.clientId = (ushort) id;
            byte[] fullNetworkStateBytes = NetworkState.Serialize(true);
            Server.BroadcastBytesTo(initialStatePacket.Serialize(fullNetworkStateBytes), id);
            
            // Send them the initial apple state, which says for every apple, if it is picked or not
            InitialAppleState initialAppleState;
            Server.BroadcastBytesTo(initialAppleState.Serialize(), id);
        
            _clientInputHistory[(ushort)id] = InputPacket.InitialPacket();
            EntityNames[(ushort) id] = info.username;
        });
    }

    public void StartClient(string host, Client.ConnectData connectData)
    {
        Client = new Client();
        Client.PacketReceived += ClientOnPacketReceived;
        
        NetworkEntity2.EntityApplesUpdate += UpdateNetworkEntityNametag;
        
        Client.Connect(host, Constants.DefaultPort, connectData);
    }

    private void UpdateNetworkEntityNametag(ushort id, ushort apples)
    {
        if (NetworkedObjects.ContainsKey(id))
            NetworkedObjects[id].GetComponentInChildren<TextMesh>().text = EntityNames[id] + ": " + apples;
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
                ushort lastClientId = StatePacket.GetLastClientPacketId(data);
                ServerPositionReceived?.Invoke(lastClientId, clientOwnedPosition);
                LerpT = 0;
                break;
            case StatePacket.PacketType.InitialState:
                Debug.Log("Initial packet");
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

                    EntityNames[pair.Key] = pair.Value.name;
                    obj.GetComponentInChildren<TextMesh>().text = pair.Value.name + ": " + pair.Value.apples;
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

                EntityNames[id] = NetworkState.LatestEntityDict[id].name;
                // Update their name tag
                obj.GetComponentInChildren<TextMesh>().text = NetworkState.LatestEntityDict[id].name + ": " + NetworkState.LatestEntityDict[id].apples;
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
            case StatePacket.PacketType.InitialAppleState:
                // Deserialize will automatically update all the trees
                InitialAppleState.Deserialize(data);
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
            _dbUpdateTimer += Time.deltaTime;

            // Update the database with the new apple values
            if (_dbUpdateTimer >= AppleDatabaseUpdateTime && ApplePickers.Count > 0)
            {
                _dbUpdateTimer = 0;
                DbAppleUpdate update;
                int count = ApplePickers.Count;
                update.usernames = new string[count];
                update.apples = new int[count];
                int i = 0;
                foreach (var pickers in ApplePickers.Values)
                {
                    update.usernames[i] = pickers.Item1;
                    update.apples[i] = pickers.Item2;
                    i++;
                }

                string json = JsonUtility.ToJson(update);
                WWWForm form = new WWWForm();
                form.AddField("json", json);
                        
                UnityWebRequest www = UnityWebRequest.Post(Constants.PHPServerHost + "/updateApples.php", form);
                www.SendWebRequest().completed += operation =>
                {
                    Debug.Log("Server update complete: " + www.downloadHandler.text);
                };
                
                ApplePickers.Clear();
            }
            
            if (_timer >= 1 / StateSendRate)
            {
                _timer = 0;

                // Serialize network state just once, instead of serializing the same data for every client
                byte[] stateBytes = NetworkState.Serialize();
                // Individually send each client the network state, because the lastClientPacketId
                // is unique
                foreach (var networkedObject in NetworkedObjects)
                {
                    StatePacket statePacket = new StatePacket();
                    statePacket.packetType = StatePacket.PacketType.State;
                    statePacket.id = currentPacketId;
                    statePacket.lastClientPacketId = _clientInputHistory[networkedObject.Key].id;
                    Server.BroadcastBytesTo(statePacket.Serialize(stateBytes), networkedObject.Key);
                }
                currentPacketId++;

                // Send the state to all network id's in networkedObjects, entries in this dictionary
                // are garunteed to have received an initial state
                // StatePacket statePacket = new StatePacket();
                // statePacket.packetType = StatePacket.PacketType.State;
                // statePacket.id = currentPacketId++;
                // Server.BroadcastBytesTo(statePacket.Serialize(), NetworkedObjects.Keys);
                
                // Tell everyone the apple state changes we have collected, then reset
                Server.BroadcastBytesTo(_appleState.Serialize(), NetworkedObjects.Keys);
                _appleState = AppleState.InitialPacket();
            }
        }
        else if (Client != null && _clientInitialized)
        {
            if (_timer >= 1 / InputSendRate)
            {
                _timer = 0;

                // Client side prediction
                PreClientInputSend?.Invoke(currentPacketId);
                
                // Grab the inputs recorder by Client input and send them
                InputCompressor.Inputs inputs = _clientOwnedObject.GetComponent<ClientInput>().ClientInputs;
                InputPacket inputPacket = InputPacket.ComposePacket(currentPacketId, inputs, _clientOwnedCamera.transform.rotation);

                Client.SendBytes(inputPacket.Serialize(_previousInputPacket));
                ++currentPacketId;
                
                // Keep this packet so we can tell if there was a rotation change next time we make a packet
                _previousInputPacket = inputPacket;
                
                PostClientInputSend?.Invoke(currentPacketId);

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
        public enum PacketType : byte {Connect, Disconnect, State, InitialState, AppleState, InitialAppleState}

        public PacketType packetType;
        public ushort id;

        // used so client can validate its client side predictions
        public ushort lastClientPacketId;
        // Only used for Initial state packet, it is the peer ID of the client
        public ushort clientId;

        // State data is passed in, to allow for the same state bytes to be sent for multiple serialize calls
        public byte[] Serialize(byte[] stateData)
        {
            byte[] data = new byte[1]; // The Stream will increase this as needed *lazy...*
            BitStream stream = BitStream.Create(data);
            stream.AutoIncreaseStream = true;

            stream.WriteByte((byte)packetType);
            stream.WriteUInt16(id);
            switch (packetType)
            {
                case PacketType.State:
                    stream.WriteUInt16(lastClientPacketId);
                    stream.WriteBytes(stateData, stateData.Length, true);
                    break;
                case PacketType.InitialState:
                    stream.WriteUInt16(clientId);
                    stream.WriteBytes(stateData, stateData.Length, true);
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

        // For state packets
        public static ushort GetLastClientPacketId(byte[] data)
        {
            return BitConverter.ToUInt16(data, 3);
        }

        // For initial packets
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
            if (packetType == PacketType.State)
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

        public static InputPacket Deserialize(byte[] data, InputPacket old)
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

    private struct InitialAppleState
    {
        // Encoding: packet type, number of trees, then each trees apple active bit mask
        public byte[] Serialize()
        {
            byte[] data = new byte[TreeBehavior.Trees.Length];
            BitStream stream = new BitStream(data);
            stream.AutoIncreaseStream = true;
            
            stream.WriteByte((byte)StatePacket.PacketType.InitialAppleState);
            stream.WriteByte((byte)TreeBehavior.Trees.Length);
            foreach (var tree in TreeBehavior.Trees)
            {
                stream.WriteByte(tree.GetAppleMask());
            }

            return stream.GetStreamData();
        }

        public static void Deserialize(byte[] data)
        {
            BitStream stream = new BitStream(data);
            
            StatePacket.PacketType type = (StatePacket.PacketType)stream.ReadByte();
            byte length = stream.ReadByte();

            for (int i = 0; i < length; i++)
            {
                byte mask = stream.ReadByte();
                TreeBehavior.Trees[i].SetApples(mask);
            }
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

    struct DbAppleUpdate
    {
        public string[] usernames;
        public int[] apples;
    }
}
