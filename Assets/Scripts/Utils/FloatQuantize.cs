using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class FloatQuantize
{
    public static short QuantizeFloat(float f, int factor)
    {
        short quantized = (short)(f * factor);
        return quantized;
    }

    public static float UnQunatizeFloat(short quantized, int factor)
    {
        return (float)quantized / (float)factor;
    }
}
