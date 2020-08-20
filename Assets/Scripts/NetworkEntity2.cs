using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using BitStream = BitStreams.BitStream;
public class NetworkEntity2
{
    public static readonly int PostionFactor = 64;
    public static readonly int RotationFactor = 32767;
    
    public static event Action<ushort, ushort> EntityApplesUpdate;
    
    public ushort id;
    private NetworkedPosition _position;
    private NetworkedRotation _rotation;
    // TODO: Create dirty property templated class
    public bool nameDirty;
    public string name;
    public bool applesDirty;
    public ushort apples;

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
        stream.SetEncoding(Encoding.UTF8);
        
        stream.WriteUInt16(id);

        if (full)
        {
            _position.SetDirty();
            _rotation.SetDirty();
            nameDirty = true;
            applesDirty = true;
        }
        
        _position.Serialize(stream);
        _rotation.Serialize(stream);
        
        stream.WriteBit(nameDirty);
        if (nameDirty)
        {
            stream.WriteByte((byte)name.Length);
            stream.WriteString(name);
            nameDirty = false;
        }
        stream.WriteBit(applesDirty);
        if (applesDirty)
        {
            stream.WriteUInt16(apples);
            applesDirty = false;
        }   
    }

    public static NetworkEntity2 Deserialize(BitStream stream)
    {
        ushort id = stream.ReadUInt16();
        stream.SetEncoding(Encoding.UTF8);

        NetworkEntity2 baseEntity;
        baseEntity = NetworkState.LatestEntityDict.ContainsKey(id) ? NetworkState.LatestEntityDict[id] : new NetworkEntity2();
        NetworkEntity2 networkEntity = new NetworkEntity2
        {
            id = id,
            _position = NetworkedPosition.Deserialize(stream, baseEntity._position),
            _rotation = NetworkedRotation.Deserialize(stream, baseEntity._rotation)
        };

        bool nameDirty = stream.ReadBit();
        networkEntity.nameDirty = nameDirty;
        if (nameDirty)
        {
            byte length = stream.ReadByte();
            string name = stream.ReadString(length);
            networkEntity.name = name;
        }

        bool applesDirty = stream.ReadBit();
        networkEntity.applesDirty = applesDirty;
        if (applesDirty)
        {
            ushort apples = stream.ReadUInt16();
            networkEntity.apples = apples;
            EntityApplesUpdate?.Invoke(id, apples);
        }
            

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
                stream.WriteInt16(FloatQuantize.QuantizeFloat(_position.x, PostionFactor));
                stream.WriteInt16(FloatQuantize.QuantizeFloat(_position.y, PostionFactor));
                stream.WriteInt16(FloatQuantize.QuantizeFloat(_position.z, PostionFactor));
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
                float x = FloatQuantize.UnQunatizeFloat(stream.ReadInt16(), PostionFactor);
                float y = FloatQuantize.UnQunatizeFloat(stream.ReadInt16(), PostionFactor);
                float z = FloatQuantize.UnQunatizeFloat(stream.ReadInt16(), PostionFactor);
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
                stream.WriteInt16(FloatQuantize.QuantizeFloat(_rotation.x, RotationFactor));
                stream.WriteInt16(FloatQuantize.QuantizeFloat(_rotation.y, RotationFactor));
                stream.WriteInt16(FloatQuantize.QuantizeFloat(_rotation.z, RotationFactor));
                stream.WriteInt16(FloatQuantize.QuantizeFloat(_rotation.w, RotationFactor));
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
                float x = FloatQuantize.UnQunatizeFloat(stream.ReadInt16(), RotationFactor);
                float y = FloatQuantize.UnQunatizeFloat(stream.ReadInt16(), RotationFactor);
                float z = FloatQuantize.UnQunatizeFloat(stream.ReadInt16(), RotationFactor);
                float w = FloatQuantize.UnQunatizeFloat(stream.ReadInt16(), RotationFactor);
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
