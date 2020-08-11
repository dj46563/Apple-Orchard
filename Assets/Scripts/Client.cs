using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;
using ENet;
using K4os.Compression.LZ4;
using EventType = ENet.EventType;

public class Client
{
    public event Action Connected;
    public event Action Disconnected;
    public event Action TimedOut;
    public event Action<byte[]> PacketReceived;
    public event Action<byte[]> PacketSent;

    public bool IsConnected { get; private set; } = false;
    
    private Host _client = new Host();
    private ENet.Event _netEvent;
    private Peer _peer;
    private Address _address = new Address();

    private ushort _packetCount = 0;

    public void Connect(string host, ushort port)
    {
        _address.Port = port;
        _address.SetHost(host);
        _client.Create();
        _peer = _client.Connect(_address, Constants.ChannelLimit);
    }

    public void SendBytes(byte[] data)
    {
        Packet packet = default(Packet);
        packet.Create(data);
        _peer.Send(0, ref packet);
        
        PacketSent?.Invoke(data);
    }

    public void PollEvents()
    {
        bool polled = false;

        while (!polled)
        {
            if (_client.CheckEvents(out _netEvent) <= 0)
            {
                if (_client.Service(0, out _netEvent) <= 0)
                    return;
                polled = true;
            }

            switch (_netEvent.Type)
            {
                case EventType.None:
                    break;
                case EventType.Connect:
                    Connected?.Invoke();
                    IsConnected = true;
                    Debug.Log("Client connected to server");
                    break;
                case EventType.Disconnect:
                    Disconnected?.Invoke();
                    IsConnected = false;
                    Debug.Log("Client disconnected from server");
                    break;
                case EventType.Receive:
                    byte[] data = new byte[_netEvent.Packet.Length];
                    _netEvent.Packet.CopyTo(data);
                    PacketReceived?.Invoke(data);
                    break;
                case EventType.Timeout:
                    TimedOut?.Invoke();
                    IsConnected = false;
                    Debug.Log("Client timed out");
                    break;
                default:
                    break;
            }
        }
    }

    public void Disconnect()
    {
        _client.Flush();
        _peer.DisconnectNow(0);
        //while (_peer.State == PeerState.Disconnecting && _peer.State != PeerState.Disconnected) { }
        _client.Dispose();
    }

    public float GetRTT()
    {
        return _peer.RoundTripTime;
    }
}
