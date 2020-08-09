using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowState : MonoBehaviour
{
    public ushort? Id;
    private bool _isOwner = false;

    public void SetOwner()
    {
        _isOwner = true;
    }

    private void Update()
    {
        // Only move non owned entities
        if (!_isOwner && Id != null && NetworkState.PreviousEntityDict.ContainsKey(Id.Value))
        {
            // Interpolate position and rotation
            Vector3 previousPosition = NetworkState.PreviousEntityDict[Id.Value].Position;
            Vector3 latestPosition = NetworkState.LatestEntityDict[Id.Value].Position;
            
            Quaternion previousRotation = NetworkState.PreviousEntityDict[Id.Value].Rotation;
            Quaternion latestRotation = NetworkState.LatestEntityDict[Id.Value].Rotation;

            transform.position = Vector3.Lerp(previousPosition, latestPosition, DirtyStateTransport.LerpT);
            transform.rotation = Quaternion.Slerp(previousRotation, latestRotation, DirtyStateTransport.LerpT);
        }
            
    }
}
