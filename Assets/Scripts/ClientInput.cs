using System;
using System.Collections;
using System.Collections.Generic;
using CircularBuffer;
using UnityEngine;
using Inputs = InputCompressor.Inputs;

public class ClientInput : MonoBehaviour
{
    private const int InputStateBufferSize = 10;
    private const float AcceptablePredictionErrorDistance = 0.02f;

    private Transform _cameraTransform;
    private Transform _transform;
    
    private Inputs _clientInputs;
    // Accessed by dirty state transport to send to the server what inputs the client has pressed
    public Inputs ClientInputs
    {
        get => _clientInputs;
    }


    private CharacterController _cc;
    private Vector3 targetPostion;
    // An approximation of dirty state's lerpT, reset every time a packet is sent
    private float lerpT = 0;
    private Vector3 previousTargetPosition;
    private Vector3 lerpedTargetPostion;
    
    private CircularBuffer<InputState> _inputStates;

    // Records the predicted position made on a client for each state
    struct InputState
    {
        public ushort id;
        public Vector3 position;
    }

    private void Awake()
    {
        _transform = transform;
        // Initialize camera and match its rotation to the player
        _cameraTransform = GameController.PlayerCamera.transform;
        _cameraTransform.rotation = _transform.rotation;
        
        DirtyStateTransport.PreClientInputSend += DirtyStateTransportOnPreClientInputSend;
        DirtyStateTransport.PostClientInputSend += DirtyStateTransportOnPostClientInputSend;
        DirtyStateTransport.ServerPositionReceived += DirtyStateTransportOnServerPositionReceived;
        
        _inputStates = new CircularBuffer<InputState>(InputStateBufferSize);
        targetPostion = transform.position;
        previousTargetPosition = _transform.position;
    }

    private void DirtyStateTransportOnServerPositionReceived(ushort stateId, Vector3 serverPosition)
    {
        int bufferSize = _inputStates.Size;
        for (int i = 0; i < bufferSize; i++)
        {
            // TEMP: MINUS 1?
            if (_inputStates[i].id == stateId - 1)
            {
                float distance = Vector3.Distance(serverPosition, _inputStates[i].position);
                bool desync = distance > AcceptablePredictionErrorDistance;
                
                // TODO: Correct the desync
                if (desync)
                    Debug.Log("Desync! Client: " + _inputStates[i].position + ", Server: " + serverPosition + ", Distance: " + distance);
                // else
                //     Debug.Log("Sync!");

                break;
            }
        }
    }

    private void DirtyStateTransportOnPreClientInputSend(ushort stateId)
    {
        lerpT = 0;
        previousTargetPosition = targetPostion;

        // Change the direction that the client moves
        int horizontal = (_clientInputs.D ? 1 : 0) - (_clientInputs.A ? 1 : 0);
        int vertical = (_clientInputs.W ? 1 : 0) - (_clientInputs.S ? 1 : 0);
        targetPostion += _transform.rotation * new Vector3(horizontal, 0, vertical) * (1 / DirtyStateTransport.InputSendRate);
        
        // Record position
        InputState inputState;
        inputState.id = stateId;
        inputState.position = targetPostion;
        // It is possible the server state hasn't changed, in this case, overwrite the input state record
        if (!_inputStates.IsEmpty && _inputStates.Front().id == stateId)
            _inputStates[0] = inputState;
        else
            _inputStates.PushFront(inputState);
    }

    private void DirtyStateTransportOnPostClientInputSend(ushort obj)
    {
        _clientInputs.ClearInputs();
    }
    
    void Start()
    {
        _clientInputs.ClearInputs();
        
        // Get the CC, if there is no CC, add one
        _cc = GetComponent<CharacterController>();
        if (_cc == null)
            _cc = gameObject.AddComponent<CharacterController>();
    }


    // Update is called once per frame
    void Update()
    {
        if (!Application.isFocused)
            return;
        
        // Will get to 0 in the time it takes for another input packet to be sent
        lerpT += Time.deltaTime * DirtyStateTransport.InputSendRate;
        // Calculate a position between the old target position and the new one
        lerpedTargetPostion = Vector3.Lerp(previousTargetPosition, targetPostion, lerpT);
        
        // Move to the lerped target position (CC.Move needs a delta)
        _cc.Move(lerpedTargetPostion - transform.position);
        
        if (Input.GetKey(KeyCode.W))
            _clientInputs.W = true;
        if (Input.GetKey(KeyCode.A))
            _clientInputs.A = true;
        if (Input.GetKey(KeyCode.S))
            _clientInputs.S = true;
        if (Input.GetKey(KeyCode.D))
            _clientInputs.D = true;
        
        
        _transform.Rotate(Vector3.up, Input.GetAxis("Mouse X"), Space.World);
        
        // Update camera position
        _cameraTransform.position = _transform.position;
        _cameraTransform.Rotate(-Vector3.right, Input.GetAxis("Mouse Y"), Space.Self);
        _cameraTransform.Rotate(Vector3.up, Input.GetAxis("Mouse X"), Space.World);
    }
}
