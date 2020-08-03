using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CircularBuffer;
using UnityEngine;

public class DeltaStateTransport : MonoBehaviour
{
    private readonly float InputSendRate = 10;
    private readonly float StateSendRate = 5;
    private static readonly int CircularBufferSize = 10;

    public Server Server { get; private set; }
    public  Client Client { get; private set; }

    private static ushort currentPacketId = 1;
    private static ushort latestPacketReceived;

    // Buffer of 
    private static CircularBuffer<BufferedState> _statePackets = new CircularBuffer<BufferedState>(CircularBufferSize);

    // Server specific
    // List is used because we need to update all entries every frame anyways, and it has fast adding and removing
    private List<NetworkEntity> entities = new List<NetworkEntity>();

    private float _timer;
    
    // used to keep track the most recent state packet that clients have acknowledged receiving
    private Dictionary<uint, ushort> _peerAck = new Dictionary<uint, ushort>();
    
    // For interpolation
    private DiffState previousDiff;
    private DiffState latestDiff;
    private float lerpT = 0;
    
    // DEBUG CODE
    private Transform cubeTransform;
    private Transform sphereTransform;
    private void Start()
    {
        cubeTransform = GameObject.Find("Cube").transform;
        sphereTransform = GameObject.Find("Sphere").transform;
        
        BufferedState initialState;
        initialState.id = 0;
        initialState.diffStateBytes = new byte[1] {0};
        _statePackets.PushFront(initialState);
    }

    public void StartServer()
    {
        Server = new Server();
        Server.PeerConnected += ServerOnPeerConnected;
        Server.PeerDisconncted += ServerOnPeerDisconncted;
        Server.PacketReceived += ServerOnPacketReceived;
        
        Server.Listen(Constants.DefaultPort);
    }

    private void ServerOnPacketReceived(byte[] data, uint peerId)
    {
        InputPacket inputPacket = InputPacket.Deserialize(data);
        
        // Register the client's latest received state, so we can start sending diffs from that state
        _peerAck[peerId] = inputPacket.latestState;
        
        // Apply inputs to cube
        InputCompressor.Inputs inputs = InputCompressor.DecompressInput(inputPacket.inputByte);

        int horizontal = Convert.ToSByte(inputs.D) - Convert.ToSByte(inputs.A);
        int vertical = Convert.ToSByte(inputs.W) - Convert.ToSByte(inputs.S);
        
        cubeTransform.position += new Vector3(horizontal, 0, vertical) * (1 / InputSendRate);
    }

    public void StartClient()
    {
        Client = new Client();
        Client.PacketReceived += ClientOnPacketReceived;

        latestDiff = default(DiffState); // Dummy value, latest diff will be replaced by the next received state packet
        
        Client.Connect(Constants.DefaultHost, Constants.DefaultPort);
    }

    private void ClientOnPacketReceived(byte[] data)
    {
        previousDiff = latestDiff;

        byte[] currentState;
        
        StatePacket statePacket = StatePacket.Deserialize(data, out currentState);
        BufferedState bufferedState;
        bufferedState.id = statePacket.id;
        bufferedState.diffStateBytes = currentState;
        _statePackets.PushFront(bufferedState);

        latestDiff = statePacket.diffState;
        lerpT = 0;
    }

    // Register and unregister clients from the ack dictionary
    private void ServerOnPeerDisconncted(uint peerId)
    {
        _peerAck.Remove(peerId);
    }
    private void ServerOnPeerConnected(uint peerId)
    {
        _peerAck[peerId] = 0;
    }

    private void Update()
    {
        if (Client != null)
        {
            if (_statePackets.Size >= 3) // First one doesn't count, it is the base zero'd state
            {
                lerpT += Time.deltaTime * StateSendRate;

                cubeTransform.position = latestDiff.entities[0].position;
                cubeTransform.rotation = latestDiff.entities[0].rotation;
                sphereTransform.position = latestDiff.entities[1].position;
                sphereTransform.rotation = latestDiff.entities[1].rotation;
                
                // foreach (NetworkEntity entity in latestDiff.entities)
                // {
                //     NetworkEntity previousEntity = previousDiff.entities.Find(x => x.entityId == entity.entityId);
                //     cubeTransform.position = Vector3.Lerp(previousEntity.position, entity.position, lerpT);
                //     cubeTransform.rotation = Quaternion.Slerp(previousEntity.rotation, entity.rotation, lerpT);
                // }
            }
        }
    }

    private void LateUpdate()
    {
        Server?.PollEvents();
        Client?.PollEvents();
        
        // TODO: if server, apply client inputs

        _timer += Time.deltaTime;
        if (Server != null)
        {
            if (_timer >= 1 / StateSendRate)
            {
                _timer = 0;
                
                StatePacket currentState;
                currentState.id = currentPacketId;
                currentState.baseId = (ushort)(currentPacketId - 1); // can't be negative because currentPacketId starts at 1
                
                // DEBUG CODE
                NetworkEntity cubeEntity;
                cubeEntity.position = cubeTransform.position;
                cubeEntity.rotation = cubeTransform.rotation;
                cubeEntity.entityId = 1;
                NetworkEntity sphereEntity;
                sphereEntity.position = sphereTransform.position;
                sphereEntity.rotation = sphereTransform.rotation;
                sphereEntity.entityId = 2;
                currentState.diffState.entities = new List<NetworkEntity>() { cubeEntity, sphereEntity };
                // END DEBUG CODE
                
                // Note: serialize expects the latest packet to be in the ring buffer before serialization occurs
                BufferedState bufferedState;
                bufferedState.id = currentPacketId;
                // Store the absolute state in the ring buffer, not a delta state, hence the 0
                bufferedState.diffStateBytes = currentState.diffState.Serialize(0);
                _statePackets.PushFront(bufferedState);
                
                // Create a delta packet for each peer
                foreach (KeyValuePair<uint, ushort> peer in _peerAck)
                {
                    currentState.baseId = peer.Value;

                    uint peerId = peer.Key;
                    byte[] data = currentState.Serialize();
                    Server.BroadcastBytes(data, peerId);
                }
                
                currentPacketId++;
            }
        }

        if (Client != null)
        {
            if (_timer >= 1 / InputSendRate)
            {
                _timer = 0;
                
                InputPacket inputPacket = InputPacket.ComposePacket();
                Client.SendBytes(inputPacket.Serialize());
            }
            
        }
    }

    private void OnApplicationQuit()
    {
        Server?.Disconnect();
        Client?.Disconnect();
    }

    private struct BufferedState
    {
        public ushort id;
        public byte[] diffStateBytes;
    }
    
    // Packet that represents a difference in game state, and is sent to clients
    private struct StatePacket
    {
        public ushort id;
        public ushort baseId;
        public DiffState diffState;
        //private List<NetworkEntity> entities;

        public byte[] Serialize()
        {
            byte[] idBytes = BitConverter.GetBytes(id);
            byte[] baseIdBytes = BitConverter.GetBytes(baseId);
            //byte[] entitiesBytes = NetworkEntity.Serialize(entities);
            byte[] diffStateBytes = diffState.Serialize(baseId);
            
            byte[] bytes = new byte[idBytes.Length + baseIdBytes.Length + diffStateBytes.Length];
            Buffer.BlockCopy(idBytes, 0, bytes, 0, idBytes.Length);
            Buffer.BlockCopy(baseIdBytes, 0, bytes, idBytes.Length, baseIdBytes.Length);
            Buffer.BlockCopy(diffStateBytes, 0, bytes, idBytes.Length + baseIdBytes.Length, diffStateBytes.Length);

            return bytes;
        }

        public static StatePacket Deserialize(byte[] bytes, out byte[] deltaApplied)
        {
            StatePacket statePacket;
            statePacket.id = BitConverter.ToUInt16(bytes, 0);
            statePacket.baseId = BitConverter.ToUInt16(bytes, 2);
            statePacket.diffState = DiffState.Deserialize(bytes, statePacket.baseId, 4, out deltaApplied);

            return statePacket;
        }
    }

    private struct DiffState
    {
        public List<NetworkEntity> entities;

        // Created a delta serialization from a previous state with id baseId
        public byte[] Serialize(ushort baseId)
        {
            byte[] delta;
            byte[] thisDiffState = NetworkEntity.Serialize(entities);
            // If base is 0, then there is base to create a delta
            if (baseId != 0 && (currentPacketId - baseId) < _statePackets.Capacity)
            {
                // Do some math to figure out which position has the base state we want, since server is guaranteed to have all states
                byte[] baseDiffState = _statePackets[currentPacketId - baseId].diffStateBytes;
                delta = XORBytes(baseDiffState, thisDiffState);
            }
            else
            {
                delta = thisDiffState;
            }
            return delta;
        }

        public static DiffState Deserialize(byte[] bytes, ushort baseId, int offset, out byte[] deltaApplied)
        {
            DiffState diffState;
            byte[] offsetBytes = bytes.AsSpan().Slice(offset).ToArray();

            deltaApplied = null;
            
            // Only xor with a base state if the offset isn't 0, 0 means no actual base state and there is no diff
            if (baseId != 0)
            {
                // Search in circular buffer for base packet, need to search because client may be missing some packets
                for (int i = 0; i < _statePackets.Size; i++)
                {
                    if (_statePackets[i].id == baseId)
                    {
                        byte[] baseBytes = _statePackets[i].diffStateBytes;
                        deltaApplied = XORBytes(baseBytes, offsetBytes);
                    }
                }
            }
            else
            {
                deltaApplied = offsetBytes;
            }

            if (deltaApplied != null)
            {
                diffState.entities = NetworkEntity.Deserialize(deltaApplied).ToList();
                return diffState;
            }
            else
            {
                throw new Exception("BaseID " + baseId + " does not exist in buffer");
            }
        }
        
        private static byte[] XORBytes(byte[] arr1, byte[] arr2)
        {
            byte[] delta = new byte[arr2.Length];
            if (arr2.Length > arr1.Length)
                Buffer.BlockCopy(arr2, arr1.Length - 1, delta, arr1.Length - 1, arr2.Length - arr1.Length);
            for (int i = 0; i < arr1.Length; i++)
            {
                delta[i] = (byte)(arr1[i] ^ arr2[i]);
            }

            return delta;
        }
    }

    private struct InputPacket
    {
        public ushort id;
        public ushort latestState;
        public byte inputByte;

        public static InputPacket Deserialize(byte[] data)
        {
            InputPacket returnPacket;
            returnPacket.id = BitConverter.ToUInt16(data, 0);
            returnPacket.latestState = BitConverter.ToUInt16(data, 2);
            returnPacket.inputByte = data[4];
            return returnPacket;
        }

        public byte[] Serialize()
        {
            byte[] bytes = new byte[5];
            Buffer.BlockCopy(BitConverter.GetBytes(id), 0, bytes, 0, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(latestState), 0, bytes, 2, 2);
            bytes[4] = inputByte;
            return bytes;
        }

        public static InputPacket ComposePacket()
        {
            InputCompressor.Inputs inputs;
            inputs.W = Input.GetKey(KeyCode.W);
            inputs.A = Input.GetKey(KeyCode.A);
            inputs.S = Input.GetKey(KeyCode.S);
            inputs.D = Input.GetKey(KeyCode.D);
            inputs.Space = Input.GetKey(KeyCode.Space);

            InputPacket inputPacket;
            inputPacket.id = currentPacketId++;
            inputPacket.latestState = _statePackets[0].id;
            inputPacket.inputByte = InputCompressor.CompressInput(inputs);
            return inputPacket;
        }
    }
}
