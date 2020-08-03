using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using ENet;
using UnityEngine;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Encoders;
using EventType = ENet.EventType;

public class Server
{
    public event Action<byte[], uint> PacketReceived;
    public event Action<uint> PeerConnected;
    public event Action<uint> PeerDisconncted;
    
    private Host _server = new Host();
    private Address _address = new Address();
    private ENet.Event _netEvent;

    private Dictionary<uint, Peer> _peerDict = new Dictionary<uint, Peer>();

    public void Listen(ushort port, string host = null)
    {
        if (host != null)
            _address.SetHost(host);
        _address.Port = port;
        _server.Create(_address, Constants.DefaultPeerLimit, Constants.ChannelLimit);
        
        Debug.Log("Server created on port: " + port);
    }

    public int BroadcastBytes(byte[] data)
    {
        var encoded = LZ4Pickler.Pickle(data);
        
        Packet packet = default(Packet);
        packet.Create(encoded);
        _server.Broadcast(0, ref packet);
        
        return encoded.Length;
    }
    public int BroadcastBytes(byte[] data, uint peerId)
    {
        var encoded = LZ4Pickler.Pickle(data);
        
        Packet packet = default(Packet);
        packet.Create(encoded);
        _server.Broadcast(0, ref packet, new Peer[] { _peerDict[peerId] });
        
        Debug.Log("Sent " + encoded.Length + " bytes");
        
        return encoded.Length;
    }

    public void PollEvents()
    {
        bool polled = false;

        while (!polled)
        {
            if (_server.CheckEvents(out _netEvent) <= 0)
            {
                if (_server.Service(0, out _netEvent) <= 0)
                    return;

                polled = true;
            }

            switch (_netEvent.Type)
            {
                case EventType.None:
                    break;
                case EventType.Connect:
                    PeerConnected?.Invoke(_netEvent.Peer.ID);
                    _peerDict[_netEvent.Peer.ID] = _netEvent.Peer;
                    Debug.Log("Client connected - ID: " + _netEvent.Peer.ID + ", IP: " + _netEvent.Peer.IP);
                    break;
                case EventType.Disconnect:
                    PeerDisconncted?.Invoke(_netEvent.Peer.ID);
                    _peerDict.Remove(_netEvent.Peer.ID);
                    Debug.Log("Client disconnected - ID: " + _netEvent.Peer.ID + ", IP: " + _netEvent.Peer.IP);
                    break;
                case EventType.Receive:
                    byte[] buffer = new byte[_netEvent.Packet.Length];
                    _netEvent.Packet.CopyTo(buffer);
                    var decoded = LZ4Pickler.Unpickle(buffer);
                    PacketReceived?.Invoke(decoded, _netEvent.Peer.ID);
                    break;
                case EventType.Timeout:
                    Debug.Log("Client timed out - IP: " + _netEvent.Peer.IP);
                    break;
                default:
                    break;
            }
        }
    }

    public void Disconnect()
    {
        _server.Flush();
        _server.Dispose();
    }
}
