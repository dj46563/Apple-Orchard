using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ENet;
using UnityEngine;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Encoders;
using UnityEngine.Networking;
using EventType = ENet.EventType;

public class Server
{
    public event Action<byte[], uint> PacketReceived;
    public event Action<uint, Client.ConnectData> PeerConnected;
    public event Action<uint> PeerDisconncted;
    
    private Host _server = new Host();
    private Address _address = new Address();
    private ENet.Event _netEvent;

    private Dictionary<uint, Peer> _peerDict = new Dictionary<uint, Peer>();
    private HashSet<uint> _peersWithoutConnectionData = new HashSet<uint>();
    
    // Caches the values of _peerDict, is used whenever a packet is broadcasted, so we cache the operation
    private Peer[] _connectedPeerCache = new Peer[0];

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
        Packet packet = default(Packet);
        packet.Create(data);
        _server.Broadcast(0, ref packet, _connectedPeerCache);
        
        //Debug.Log("All. Raw: " + data.Length);
        
        return data.Length;
    }
    public int BroadcastBytesTo(byte[] data, uint peerId)
    {
        Packet packet = default(Packet);
        packet.Create(data, PacketFlags.Reliable);
        _server.Broadcast(0, ref packet, new Peer[] { _peerDict[peerId] });
        
        return data.Length;
    }
    
    public int BroadcastBytesTo(byte[] data, IEnumerable<ushort> peerIds)
    {
        Packet packet = default(Packet);
        foreach (var peerId in peerIds)
        {
            packet.Create(data, PacketFlags.Reliable);
            _server.Broadcast(0, ref packet, new Peer[] { _peerDict[peerId] });
            packet.Dispose();
        }
        
        return data.Length;
    }
    
    public int BroadcastBytesToEveryoneExcept(byte[] data, uint peerId)
    {
        Packet packet = default(Packet);
        packet.Create(data);
        _server.Broadcast(0, ref packet, _peerDict[peerId]);
        
        return data.Length;
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
                    _peersWithoutConnectionData.Add(_netEvent.Peer.ID);
                    Debug.Log("Client connected (pre data) - ID: " + _netEvent.Peer.ID + ", IP: " + _netEvent.Peer.IP);
                    break;
                case EventType.Disconnect:
                    _peerDict.Remove(_netEvent.Peer.ID);
                    UpdateConnctedPeerChache();
                    PeerDisconncted?.Invoke(_netEvent.Peer.ID);
                    Debug.Log("Client disconnected - ID: " + _netEvent.Peer.ID + ", IP: " + _netEvent.Peer.IP);
                    break;
                case EventType.Receive:
                    byte[] data = new byte[_netEvent.Packet.Length];
                    _netEvent.Packet.CopyTo(data);
                    
                    // If we haven't gotten the connection data packet from this client yet,
                    // we can assume that this is the connection data packet
                    if (_peersWithoutConnectionData.Contains(_netEvent.Peer.ID))
                    { 
                        Client.ConnectData connectData = Client.ConnectData.Deserialize(data);
                        PeerConnected?.Invoke(_netEvent.Peer.ID, connectData);

                        _peersWithoutConnectionData.Remove(_netEvent.Peer.ID);
                        _peerDict[_netEvent.Peer.ID] = _netEvent.Peer;
                        UpdateConnctedPeerChache();
                    }
                    else
                    {
                        PacketReceived?.Invoke(data, _netEvent.Peer.ID);
                    }
                    break;
                case EventType.Timeout:
                    Debug.Log("Client timed out - IP: " + _netEvent.Peer.IP);
                    PeerDisconncted?.Invoke(_netEvent.Peer.ID);
                    _peerDict.Remove(_netEvent.Peer.ID);
                    UpdateConnctedPeerChache();
                    break;
                default:
                    break;
            }
        }
    }

    // Used whenever a key is added or removed to _peerDict
    private void UpdateConnctedPeerChache()
    {
        _connectedPeerCache = _peerDict.Values.ToArray();
    }

    public void Disconnect()
    {
        _server.Flush();
        _server.Dispose();
    }
}
