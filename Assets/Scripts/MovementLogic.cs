using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovementLogic
{
    public static readonly float Speed = 2f;
    private static readonly float GravityAccel = 0.75f;
    private static readonly float JumpInitialVel = 5f;
    
    public static Vector3 CalculateDelta(InputCompressor.Inputs inputs, Quaternion rotation, Vector3 pastDelta, bool grounded, float multiplier = 1f)
    {
        int horizontal = (inputs.D ? 1 : 0) - (inputs.A ? 1 : 0);
        int vertical = (inputs.W ? 1 : 0) - (inputs.S ? 1 : 0);

        float yDelta;
        if (!grounded)
            yDelta = (pastDelta.y * DirtyStateTransport.InputSendRate) - GravityAccel;
        else
            yDelta = 0;


        if (inputs.Space && grounded)
            yDelta = JumpInitialVel;
        
        Quaternion yRotation = Quaternion.Euler(0, rotation.eulerAngles.y, 0);

        Vector3 XAndZ = yRotation * new Vector3(horizontal, 0, vertical).normalized * (Speed * multiplier);
        Vector3 Y = new Vector3(0, yDelta, 0);
        
        return (XAndZ + Y) / DirtyStateTransport.InputSendRate;
    }
}
