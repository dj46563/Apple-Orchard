using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class EntityBitConverter
{
    public struct Entity
    {
        public ushort id; 
        public Vector3 position;
        public Quaternion rotation;
    }
    
    public static byte[] EntitiesToBytes(Entity[] entities)
    {
        byte[] bytes = new byte[1 + (30 * entities.Length)]; // Extra byte for array length

        Buffer.BlockCopy(BitConverter.GetBytes((byte)entities.Length), 0, bytes, 0, 1);
        
        for (int i = 0; i < entities.Length; i++)
        {
            Entity entity = entities[i];
            
            Buffer.BlockCopy(BitConverter.GetBytes(entity.id), 0, bytes, 1 + (i * 30) + 0, 2);
            
            Buffer.BlockCopy(BitConverter.GetBytes(entity.position.x), 0, bytes, 1 + (i * 30) + 2, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(entity.position.y), 0, bytes, 1 + (i * 30) + 6, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(entity.position.z), 0, bytes, 1 + (i * 30) + 10, 4);
            
            Buffer.BlockCopy(BitConverter.GetBytes(entity.rotation.x), 0, bytes, 1 + (i * 30) + 14, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(entity.rotation.y), 0, bytes, 1 + (i * 30) + 18, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(entity.rotation.z), 0, bytes, 1 + (i * 30) + 22, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(entity.rotation.w), 0, bytes, 1 + (i * 30) + 26, 4);
        }

        return bytes;
    }

    public static Entity[] BytesToEntites(byte[] bytes, int offset = 0)
    {
        byte length = bytes[0 + offset];
        Entity[] entities = new Entity[length];

        for (int i = 0; i < length; i++)
        {
            ushort id = bytes[1 + (i * 30) + offset];
            Vector3 position = new Vector3(
                BitConverter.ToSingle(bytes, 1 + (i * 30) + 2 + offset), 
                BitConverter.ToSingle(bytes, 1 + (i * 30) + 6 + offset), 
                BitConverter.ToSingle(bytes, 1 + (i * 30) + 10 + offset));
            Quaternion rotation = new Quaternion(
                BitConverter.ToSingle(bytes, 1 + (i * 30) + 14 + offset), 
                BitConverter.ToSingle(bytes, 1 + (i * 30) + 18 + offset), 
                BitConverter.ToSingle(bytes, 1 + (i * 30) + 22 + offset), 
                BitConverter.ToSingle(bytes, 1 + (i * 30) + 26 + offset));

            entities[i].id = id;
            entities[i].position = position;
            entities[i].rotation = rotation;
        }

        return entities;
    }
}
