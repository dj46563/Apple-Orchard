using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowState : MonoBehaviour
{
    public ushort? Id;
    public bool IsServer = false;
    private bool _isOwner = false;
    private CharacterController _cc;
    private Transform _cameraTransform;
    private Transform _nametagTransform;
    private bool _cameraNotNull;
    private bool _nametagNotNull;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();

        _cameraTransform = Camera.main.transform;
        _nametagTransform = GetComponentInChildren<TextMesh>().transform;
        _cameraNotNull = _cameraTransform != null;
        _nametagNotNull = _nametagTransform != null;
    }

    public void SetOwner()
    {
        _isOwner = true;
    }

    private void Update()
    {
        if (IsServer)
        {
            transform.position = NetworkState.LatestEntityDict[Id.Value].Position;
            transform.rotation = LockRotationToY(NetworkState.LatestEntityDict[Id.Value].Rotation);
        }
        // Only move non owned entities
        else if (!_isOwner && Id != null && NetworkState.PreviousEntityDict.ContainsKey(Id.Value))
        {
            // Interpolate position and rotation
            Vector3 previousPosition = NetworkState.PreviousEntityDict[Id.Value].Position;
            Vector3 latestPosition = NetworkState.LatestEntityDict[Id.Value].Position;
            
            Quaternion previousRotation = LockRotationToY(NetworkState.PreviousEntityDict[Id.Value].Rotation);
            Quaternion latestRotation = LockRotationToY(NetworkState.LatestEntityDict[Id.Value].Rotation);
            
            transform.position = Vector3.Lerp(previousPosition, latestPosition, DirtyStateTransport.LerpT);
            transform.rotation = Quaternion.Slerp(previousRotation, latestRotation, DirtyStateTransport.LerpT);
        }
        
        // Always have the nametag face the camera
        if (_cameraNotNull && _nametagNotNull)
        {
            _nametagTransform.forward = -(_cameraTransform.position - _nametagTransform.position);
        }
    }

    private Quaternion LockRotationToY(Quaternion rotation)
    {
        return Quaternion.Euler(0, rotation.eulerAngles.y, 0);
    }
}
