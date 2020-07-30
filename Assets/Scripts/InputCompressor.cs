using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class InputCompressor
{
    public struct Inputs
    {
        public bool W;
        public bool A;
        public bool S;
        public bool D;
        public bool Space;
    }
    
    public static byte CompressInput(Inputs inputs)
    {
        byte inputByte = (byte)(Convert.ToByte(inputs.W) |
                       Convert.ToByte(inputs.A) << 1 |
                       Convert.ToByte(inputs.S) << 2 |
                       Convert.ToByte(inputs.D) << 3 |
                       Convert.ToByte(inputs.Space) << 4);
        return inputByte;
    }

    public static Inputs DecompressInput(byte inputByte)
    {
        Inputs inputs;
        inputs.W = Convert.ToBoolean(inputByte & (1 << 0));
        inputs.A = Convert.ToBoolean(inputByte & (1 << 1));
        inputs.S = Convert.ToBoolean(inputByte & (1 << 2));
        inputs.D = Convert.ToBoolean(inputByte & (1 << 3));
        inputs.Space = Convert.ToBoolean(inputByte & (1 << 4));

        return inputs;
    }

    public static byte[] PositionToBytes(Vector3 position)
    {
        byte[] bytes = new byte[12];
        Buffer.BlockCopy(BitConverter.GetBytes(position.x), 0, bytes, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(position.y), 0, bytes, 4, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(position.z), 0, bytes, 8, 4);

        return bytes;
    }
    public static byte[] RotationToBytes(Quaternion rotation)
    {
        byte[] bytes = new byte[16];
        
        Buffer.BlockCopy(BitConverter.GetBytes(rotation.x), 0, bytes, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(rotation.y), 0, bytes, 4, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(rotation.z), 0, bytes, 8, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(rotation.w), 0, bytes, 12, 4);

        return bytes;
    }
    public static byte[] TransformToBytes(Transform transform)
    {
        byte[] bytes = new byte[28];
        Buffer.BlockCopy(PositionToBytes(transform.position), 0, bytes, 0, 12);
        Buffer.BlockCopy(RotationToBytes(transform.rotation), 0, bytes, 12, 16);
        return bytes;
    }
    
    public static Vector3 BytesToPosition(byte[] bytes)
    {
        Vector3 position = Vector3.zero;
        position.x = BitConverter.ToSingle(bytes, 0);
        position.y = BitConverter.ToSingle(bytes, 4);
        position.z = BitConverter.ToSingle(bytes, 8);
        return position;
    }
    public static Quaternion BytesToRotation(byte[] bytes, int offset = 0)
    {
        Quaternion rotation = Quaternion.identity;
        rotation.x = BitConverter.ToSingle(bytes, 0 + offset);
        rotation.y = BitConverter.ToSingle(bytes, 4 + offset);
        rotation.z = BitConverter.ToSingle(bytes, 8 + offset);
        rotation.w = BitConverter.ToSingle(bytes, 12 + offset);
        return rotation;
    }

    public static void BytesToTransform(byte[] bytes, ref Transform transform)
    {
        transform.position = BytesToPosition(bytes);
        transform.rotation = BytesToRotation(bytes, 12);
    }
}
