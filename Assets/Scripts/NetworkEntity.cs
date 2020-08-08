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
            Buffer.BlockCopy(QuantizeFloat(entity.position.x), 0, bytes, 2 + (i * EntitySize) + 2, 2);
            Buffer.BlockCopy(QuantizeFloat(entity.position.y), 0, bytes, 2 + (i * EntitySize) + 4, 2);
            Buffer.BlockCopy(QuantizeFloat(entity.position.z), 0, bytes, 2 + (i * EntitySize) + 6, 2);
            
            Buffer.BlockCopy(QuantizeFloat(entity.rotation.x), 0, bytes, 2 + (i * EntitySize) + 8, 2);
            Buffer.BlockCopy(QuantizeFloat(entity.rotation.y), 0, bytes, 2 + (i * EntitySize) + 10, 2);
            Buffer.BlockCopy(QuantizeFloat(entity.rotation.z), 0, bytes, 2 + (i * EntitySize) + 12, 2);
            Buffer.BlockCopy(QuantizeFloat(entity.rotation.w), 0, bytes, 2 + (i * EntitySize) + 14, 2);
            
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
                UnQunatizeFloat(bytes, 2 + (i * EntitySize) + 2 + offset), 
                UnQunatizeFloat(bytes, 2 + (i * EntitySize) + 4 + offset), 
                UnQunatizeFloat(bytes, 2 + (i * EntitySize) + 6 + offset));
            entity.rotation = new Quaternion(
                UnQunatizeFloat(bytes, 2 + (i * EntitySize) + 8 + offset), 
                UnQunatizeFloat(bytes, 2 + (i * EntitySize) + 10 + offset), 
                UnQunatizeFloat(bytes, 2 + (i * EntitySize) + 12 + offset), 
                UnQunatizeFloat(bytes, 2 + (i * EntitySize) + 14 + offset));

            entities.Add(entity);
        }

        return entities;
    }

    // Quantize floats to the nearest 1/64 of a unit, with a max absolute value of about 511 units
    private static byte[] QuantizeFloat(float f)
    {
        short quantized = (short)(f * 64);
        return BitConverter.GetBytes(quantized);
    }

    private static float UnQunatizeFloat(byte[] bytes, int offset)
    {
        short quantized = BitConverter.ToInt16(bytes, offset);
        return (float)quantized / 64f;
    }
}