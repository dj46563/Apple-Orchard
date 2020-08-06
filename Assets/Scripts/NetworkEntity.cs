using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct NetworkEntity
{
    private static readonly int EntitySize = 30;
    
    public ushort entityId;
    public Vector3 position;
    public Quaternion rotation;

    public static byte[] Serialize(ICollection<NetworkEntity> entities)
    {
        ushort count = (ushort)entities.Count;
        byte[] bytes = new byte[2 + EntitySize * count];
        
        Buffer.BlockCopy(BitConverter.GetBytes(count), 0, bytes, 0, 2);

        int i = 0;
        foreach (NetworkEntity entity in entities) // Use a foreach in-case a O(1) index-able collection is not given
        {
            Buffer.BlockCopy(BitConverter.GetBytes(entity.entityId), 0, bytes, 2 + (i * 30) + 0, 2);
            
            Buffer.BlockCopy(BitConverter.GetBytes(entity.position.x), 0, bytes, 2 + (i * 30) + 2, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(entity.position.y), 0, bytes, 2 + (i * 30) + 6, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(entity.position.z), 0, bytes, 2 + (i * 30) + 10, 4);
            
            Buffer.BlockCopy(BitConverter.GetBytes(entity.rotation.x), 0, bytes, 2 + (i * 30) + 14, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(entity.rotation.y), 0, bytes, 2 + (i * 30) + 18, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(entity.rotation.z), 0, bytes, 2 + (i * 30) + 22, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(entity.rotation.w), 0, bytes, 2 + (i * 30) + 26, 4);
            
            i++;
        }

        return bytes;
    }

    public static ICollection<NetworkEntity> Deserialize(byte[] bytes, int offset = 0)
    {
        ushort length = BitConverter.ToUInt16(bytes, offset);
        List<NetworkEntity> entities = new List<NetworkEntity>();

        for (int i = 0; i < length; i++)
        {
            NetworkEntity entity;
            entity.entityId = bytes[2 + (i * 30) + offset];
            entity.position = new Vector3(
                BitConverter.ToSingle(bytes, 2 + (i * EntitySize) + 2 + offset), 
                BitConverter.ToSingle(bytes, 2 + (i * EntitySize) + 6 + offset), 
                BitConverter.ToSingle(bytes, 2 + (i * EntitySize) + 10 + offset));
            entity.rotation = new Quaternion(
                BitConverter.ToSingle(bytes, 2 + (i * EntitySize) + 14 + offset), 
                BitConverter.ToSingle(bytes, 2 + (i * EntitySize) + 18 + offset), 
                BitConverter.ToSingle(bytes, 2 + (i * EntitySize) + 22 + offset), 
                BitConverter.ToSingle(bytes, 2 + (i * EntitySize) + 26 + offset));

            entities.Add(entity);
        }

        return entities;
    }
}