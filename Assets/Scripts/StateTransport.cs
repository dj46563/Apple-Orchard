using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using CircularBuffer;


// Client sends inputs, server sends state
// No Delta compression
public class StateTransport : MonoBehaviour
{
    public GameObject cube; // TEMP
    
    private Server _server;
    private Client _client;

    private ushort currentPacketId = 0;
    private ushort latestPacketRecevied;
    
    private static IFormatter formatter = new BinaryFormatter();

    private float _timer = 0;
    private readonly float InputSendRate = 10; // Inputs are sent 10 times a second
    private readonly float StateSendRate = 5; // States are sent 5 times a second

    private CircularBuffer<StatePacket> statePacketBuffer = new CircularBuffer<StatePacket>(Constants.StateBufferSize);
    private float slerpT = 0;

    private void Awake()
    {
        cube = GameObject.Find("Cube"); // TEMP
    }

    public void SetServer(Server server)
    {
        _server = server;
        
        _server.PacketReceived += InputReceived;
    }
    public void SetClient(Client client)
    {
        _client = client;
        
        _client.PacketReceived += StateReceived ;
    }

    private void StateReceived(byte[] data)
    {
        StatePacket statePacket = StatePacket.Deserialize(data);
        ushort id = statePacket.id;

        if (latestPacketRecevied < id)
        {
            latestPacketRecevied = id;
            statePacketBuffer.PushFront(statePacket);
            slerpT = 0;
            
            //cube.transform.position = statePacket.position;
            cube.transform.rotation = statePacket.rotation;
        }
        
    }

    private void LateUpdate()
    {
        _timer += Time.deltaTime;
        if (_client != null && _client.IsConnected)
        {
            if (_timer >= 1 / InputSendRate)
            {
                SendInputs();
                _timer = 0;
            }
        }
        else if (_server != null)
        {
            if (_timer >= 1 / StateSendRate)
            {
                SendState();
                _timer = 0;
            }
        }
    }

    private void Update()
    {
        if (_client != null && _client.IsConnected)
        {
            if (statePacketBuffer.Size >= 2)
            {
                slerpT += Time.deltaTime * StateSendRate;
                
                StatePacket recent = statePacketBuffer.Back();
                StatePacket previous = statePacketBuffer.Front();

                cube.transform.position = Vector3.Slerp(recent.position, previous.position, slerpT);
            }
        }
    }

    private void InputReceived(byte[] data)
    {
        InputPacket inputPacket = InputPacket.Deserialize(data);

        if (inputPacket.id > latestPacketRecevied)
        {
            latestPacketRecevied = inputPacket.id;
            
            InputCompressor.Inputs inputs = InputCompressor.DecompressInput(inputPacket.inputByte);

            int horizontal = Convert.ToSByte(inputs.D) - Convert.ToSByte(inputs.A);
            int vertical = Convert.ToSByte(inputs.W) - Convert.ToSByte(inputs.S);
        
            cube.transform.position += new Vector3(horizontal, 0, vertical) * (1 / InputSendRate);
        }
    }

    private void SendInputs()
    {
        InputCompressor.Inputs inputs;
        inputs.W = Input.GetKey(KeyCode.W);
        inputs.A = Input.GetKey(KeyCode.A);
        inputs.S = Input.GetKey(KeyCode.S);
        inputs.D = Input.GetKey(KeyCode.D);
        inputs.Space = Input.GetKey(KeyCode.Space);

        InputPacket inputPacket;
        inputPacket.id = currentPacketId++;
        inputPacket.inputByte = InputCompressor.CompressInput(inputs);
        
        _client.SendBytes(SerializeStruct(inputPacket));
    }

    public void SendState()
    {
        StatePacket statePacket;
        statePacket.id = currentPacketId++;
        statePacket.position = cube.transform.position;
        statePacket.rotation = cube.transform.rotation;
        
        _server.BroadcastBytes(SerializeStruct(statePacket));
    }

    private struct StatePacket : IByteSerializable
    {
        public ushort id;
        public Vector3 position;
        public Quaternion rotation;
        
        public byte[] Serialize()
        {
            byte[] bytes = new byte[30];
            Buffer.BlockCopy(BitConverter.GetBytes(id), 0, bytes, 0, 2);
            Buffer.BlockCopy(InputCompressor.PositionToBytes(position), 0, bytes, 2, 12);
            Buffer.BlockCopy(InputCompressor.RotationToBytes(rotation), 0, bytes, 14, 16);
            return bytes;
        }
        public static StatePacket Deserialize(byte[] data)
        {
            StatePacket returnPacket;
            returnPacket.id = BitConverter.ToUInt16(data, 0);
            returnPacket.position = InputCompressor.BytesToPosition(data, 2);
            returnPacket.rotation = InputCompressor.BytesToRotation(data, 14);
            return returnPacket;
        }
    }

    private struct InputPacket : IByteSerializable
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
            byte[] returnBytes;
            using (MemoryStream stream = new MemoryStream())
            {
                var buffer = BitConverter.GetBytes(id);
                stream.Write(buffer, 0, buffer.Length);
                
                buffer = new byte[] {inputByte};
                stream.Write(buffer, 0, buffer.Length);
                returnBytes = stream.ToArray();
            }

            return returnBytes;
        }
    }

    private byte[] SerializeStruct(IByteSerializable item)
    {
        return item.Serialize();
    }

    private interface IByteSerializable
    {
        byte[] Serialize();
        
    }
}
