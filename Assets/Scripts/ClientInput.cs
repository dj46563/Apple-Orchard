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

    private Vector3 _delta;
    // Accessed by dirty state transport to send to the server what inputs the client has pressed
    public Inputs ClientInputs
    {
        get => _clientInputs;
    }

    private Vector3 _targetPosition;
    private float lerpT = 0;

    private CharacterController _cc;
    
    // Stores a history of the positions we predicted for the owner for recent
    // server states
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

        _targetPosition = _transform.position;
        
        DirtyStateTransport.PreClientInputSend += DirtyStateTransportOnPreClientInputSend;
        DirtyStateTransport.PostClientInputSend += DirtyStateTransportOnPostClientInputSend;
        DirtyStateTransport.ServerPositionReceived += DirtyStateTransportOnServerPositionReceived;
        
        _inputStates = new CircularBuffer<InputState>(InputStateBufferSize);
    }

    private void DirtyStateTransportOnServerPositionReceived(ushort stateId, Vector3 serverPosition)
    {
        // Search for the owner's predicted state in the circular buffer that has a matching stateId
        // Calculate the distance between what the server says the position should be and the actual
        // position and decide if the distance is big enough to declare a desync
        int bufferSize = _inputStates.Size;
        for (int i = 0; i < bufferSize; i++)
        {
            if (_inputStates[i].id == stateId)
            {
                float distance = Vector3.Distance(serverPosition, _inputStates[i].position);
                bool desync = distance > AcceptablePredictionErrorDistance;
                
                // TODO: Correct the desync
                if (desync)
                    Debug.Log("Desync! Client: " + _inputStates[i].position + ", Server: " + serverPosition + ", Distance: " + distance);

                break;
            }
        }
    }

    private void DirtyStateTransportOnPreClientInputSend(ushort stateId)
    {
        // Change the direction that the client moves
        int horizontal = (_clientInputs.D ? 1 : 0) - (_clientInputs.A ? 1 : 0);
        int vertical = (_clientInputs.W ? 1 : 0) - (_clientInputs.S ? 1 : 0);
        _delta = _transform.rotation * new Vector3(horizontal, 0, vertical) / DirtyStateTransport.InputSendRate;
        _targetPosition = _transform.position + _delta;
        lerpT = 0;

        // Record the position of the player and the state we received from the server into a circular buffer
        // Once the server state (stateId) changes, it goes into a new spot in the buffer
        // and the position stored in the circular buffer will represent where we predicted our client
        // should be based on the inputs we've captured
        InputState inputState;
        // Add 1 to the state id, this is because we are translating from state Id, so it is a new state
        inputState.id = stateId;
        inputState.position = transform.position;
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

        // Move in the direction decided by DirtyStateTransportOnPreClientInputSend
        // It is a combination of all the keys the owner has pressed for this input window
        // And the direction they were facing when the function was called
        lerpT += Time.deltaTime * DirtyStateTransport.InputSendRate;
        _cc.Move(Vector3.Lerp(_targetPosition - _delta, _targetPosition, lerpT) - _transform.position);
        
        // Collect inputs for DirtyStateTransportOnPreClientInputSend
        if (Input.GetKey(KeyCode.W))
            _clientInputs.W = true;
        if (Input.GetKey(KeyCode.A))
            _clientInputs.A = true;
        if (Input.GetKey(KeyCode.S))
            _clientInputs.S = true;
        if (Input.GetKey(KeyCode.D))
            _clientInputs.D = true;
        if (Input.GetKey(KeyCode.Space))
            _clientInputs.Space = true;
        if (Input.GetKey(KeyCode.E))
            _clientInputs.E = true;
        
        
        // Rotate the player model based on mouse movement
        _transform.Rotate(Vector3.up, Input.GetAxis("Mouse X"), Space.World);
        
        // Update camera position
        _cameraTransform.position = _transform.position + Vector3.up;
        _cameraTransform.Rotate(-Vector3.right, Input.GetAxis("Mouse Y"), Space.Self);
        _cameraTransform.Rotate(Vector3.up, Input.GetAxis("Mouse X"), Space.World);
    }
}
