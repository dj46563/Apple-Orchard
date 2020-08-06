using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct NetworkEntity
{
    private static readonly int EntitySize = 16;
    
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
            Buffer.BlockCopy(BitConverter.GetBytes(entity.entityId), 0, bytes, 2 + (i * EntitySize) + 0, 2);
            
            // Floats are offset by 2 and only copy 2 bytes because I am cutting off the first 2 bytes (compressed short floats)
            Buffer.BlockCopy(BitConverter.GetBytes(entity.position.x), 2, bytes, 2 + (i * EntitySize) + 2, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(entity.position.y), 2, bytes, 2 + (i * EntitySize) + 4, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(entity.position.z), 2, bytes, 2 + (i * EntitySize) + 6, 2);
            
            Buffer.BlockCopy(BitConverter.GetBytes(entity.rotation.x), 2, bytes, 2 + (i * EntitySize) + 8, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(entity.rotation.y), 2, bytes, 2 + (i * EntitySize) + 10, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(entity.rotation.z), 2, bytes, 2 + (i * EntitySize) + 12, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(entity.rotation.w), 2, bytes, 2 + (i * EntitySize) + 14, 2);
            
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
            entity.entityId = bytes[2 + (i * EntitySize) + offset];
            entity.position = new Vector3(
                ShortFloatToFloat(bytes, 2 + (i * EntitySize) + 2 + offset), 
                ShortFloatToFloat(bytes, 2 + (i * EntitySize) + 4 + offset), 
                ShortFloatToFloat(bytes, 2 + (i * EntitySize) + 6 + offset));
            entity.rotation = new Quaternion(
                ShortFloatToFloat(bytes, 2 + (i * EntitySize) + 8 + offset), 
                ShortFloatToFloat(bytes, 2 + (i * EntitySize) + 10 + offset), 
                ShortFloatToFloat(bytes, 2 + (i * EntitySize) + 12 + offset), 
                ShortFloatToFloat(bytes, 2 + (i * EntitySize) + 14 + offset));

            entities.Add(entity);
        }

        return entities;
    }

    // Not used at the moment to avoid extra memory allocations
    private static byte[] FloatToShortFloat(float f)
    {
        byte[] full = BitConverter.GetBytes(f);
        return new[] { full[2], full[3] };
    }

    private static float ShortFloatToFloat(byte[] bytes, int start)
    {
        byte[] floatBytes = new byte[4];
        floatBytes[2] = bytes[start];
        floatBytes[3] = bytes[start + 1];
        return BitConverter.ToSingle(floatBytes, 0);
    }
}