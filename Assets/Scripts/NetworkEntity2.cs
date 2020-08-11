using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using BitStream = BitStreams.BitStream;
public class NetworkEntity2
{
    public ushort id;
    private NetworkedPosition _position;
    private NetworkedRotation _rotation;

    public NetworkEntity2()
    {
        id = 0;
        Position = Vector3.zero;
        Rotation = Quaternion.identity;
    }
    
    public Vector3 Position
    {
        get => _position.Position;
        set => _position.Position = value;
    }
    public Quaternion Rotation
    {
        get => _rotation.Rotation;
        set => _rotation.Rotation = value;
    }

    public void Serialize(BitStream stream, bool full)
    {
        stream.WriteUInt16(id);

        if (full)
        {
            _position.SetDirty();
            _rotation.SetDirty();
        }
        
        _position.Serialize(stream);
        _rotation.Serialize(stream);
    }

    public static NetworkEntity2 Deserialize(BitStream stream)
    {
        ushort id = stream.ReadUInt16();

        NetworkEntity2 baseEntity;
        baseEntity = NetworkState.LatestEntityDict.ContainsKey(id) ? NetworkState.LatestEntityDict[id] : new NetworkEntity2();
        NetworkEntity2 networkEntity = new NetworkEntity2
        {
            id = id,
            _position = NetworkedPosition.Deserialize(stream, baseEntity._position),
            _rotation = NetworkedRotation.Deserialize(stream, baseEntity._rotation)
        };

        return networkEntity;
    }

    public struct NetworkedPosition
    {
        private bool _dirty;
        private Vector3 _position;

        public Vector3 Position
        {
            get => _position;
            set
            {
                Vector3 oldPosition = _position;
                _position = value;
                if (oldPosition != _position)
                    _dirty = true;
            }
        }

        public void SetDirty()
        {
            _dirty = true;
        }

        public void Serialize(BitStream stream)
        {
            stream.WriteBit(_dirty);
            if (_dirty)
            {
                stream.WriteInt16(FloatQuantize.QuantizeFloat(_position.x, 64));
                stream.WriteInt16(FloatQuantize.QuantizeFloat(_position.y, 64));
                stream.WriteInt16(FloatQuantize.QuantizeFloat(_position.z, 64));
            }

            _dirty = false;
        }

        public static NetworkedPosition Deserialize(BitStream stream, NetworkedPosition basePosition)
        {
            NetworkedPosition networkedPosition;
            networkedPosition._dirty = false;
            
            bool dirtyBit = stream.ReadBit();
            if (dirtyBit)
            {
                float x = FloatQuantize.UnQunatizeFloat(stream.ReadInt16(), 64);
                float y = FloatQuantize.UnQunatizeFloat(stream.ReadInt16(), 64);
                float z = FloatQuantize.UnQunatizeFloat(stream.ReadInt16(), 64);
                networkedPosition._position = new Vector3(x, y, z);
            }
            else
            {
                networkedPosition._position = basePosition._position;
            }
            
            return networkedPosition;
        }
    }

    public struct NetworkedRotation
    {
        private bool _dirty;
        private Quaternion _rotation;
        
        public Quaternion Rotation
        {
            get => _rotation;
            set
            {
                Quaternion oldRotation = _rotation;
                _rotation = value;
                if (oldRotation != _rotation)
                    _dirty = true;
            }
        }
        
        public void SetDirty()
        {
            _dirty = true;
        }
        
        public void Serialize(BitStream stream)
        {
            stream.WriteBit(_dirty);
            if (_dirty)
            {
                stream.WriteInt16(FloatQuantize.QuantizeFloat(_rotation.x, 32767));
                stream.WriteInt16(FloatQuantize.QuantizeFloat(_rotation.y, 32767));
                stream.WriteInt16(FloatQuantize.QuantizeFloat(_rotation.z, 32767));
                stream.WriteInt16(FloatQuantize.QuantizeFloat(_rotation.w, 32767));
            }
            
            _dirty = false;
        }

        public static NetworkedRotation Deserialize(BitStream stream, NetworkedRotation baseRotation)
        {
            NetworkedRotation networkedRotation;
            networkedRotation._dirty = false;
            
            bool dirtyBit = stream.ReadBit();
            if (dirtyBit)
            {
                float x = FloatQuantize.UnQunatizeFloat(stream.ReadInt16(), 32767);
                float y = FloatQuantize.UnQunatizeFloat(stream.ReadInt16(), 32767);
                float z = FloatQuantize.UnQunatizeFloat(stream.ReadInt16(), 32767);
                float w = FloatQuantize.UnQunatizeFloat(stream.ReadInt16(), 32767);
                networkedRotation._rotation = new Quaternion(x, y, z, w);
            }
            else
            {
                networkedRotation._rotation = baseRotation._rotation;
            }
            
            return networkedRotation;
        }
    }
}
