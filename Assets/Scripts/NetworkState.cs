using System;
using System.Collections;
using System.Collections.Generic;
using CircularBuffer;
using UnityEngine;
using BitStream = BitStreams.BitStream;

public static class NetworkState
{
    // private static readonly int StateBufferSize = 4;
    // private static CircularBuffer<Dictionary<ushort, NetworkEntity2>> StateBuffer { get; set; } = new CircularBuffer<Dictionary<ushort, NetworkEntity2>>(StateBufferSize);
    public static Dictionary<ushort, NetworkEntity2> LatestEntityDict { get; set; } = new Dictionary<ushort, NetworkEntity2>();
    public static Dictionary<ushort, NetworkEntity2> PreviousEntityDict { get; set; } = LatestEntityDict;

    public static byte[] Serialize(bool full = false)
    {
        byte[] data = new byte[1];
        BitStream stream = new BitStream(data);
        stream.AutoIncreaseStream = true;
        
        stream.WriteUInt16((ushort)LatestEntityDict.Count);
        foreach (var entity in LatestEntityDict.Values)
        {
            entity.Serialize(stream, full);
        }

        return stream.GetStreamData();
    }

    public static void Deserialize(BitStream stream)
    {
        Dictionary<ushort, NetworkEntity2> entityDict = new Dictionary<ushort, NetworkEntity2>();
        
        ushort length = stream.ReadUInt16();
        for (int i = 0; i < length; i++)
        { 
            NetworkEntity2 entity = NetworkEntity2.Deserialize(stream);
            entityDict[entity.id] = entity;
        }

        PreviousEntityDict = LatestEntityDict;
        LatestEntityDict = entityDict;
    }

    public static ushort EntityPickedApple(ushort id)
    {
        LatestEntityDict[id].apples++;
        LatestEntityDict[id].applesDirty = true;
        return LatestEntityDict[id].apples;
    }

    public static void SetEntityNameAndApples(ushort id, string name, ushort apples)
    {
        LatestEntityDict[id].name = name;
        LatestEntityDict[id].nameDirty = true;
        LatestEntityDict[id].apples = apples;
        LatestEntityDict[id].applesDirty = true;
    }
}
