using System;
using System.Collections;
using System.Collections.Generic;
using CircularBuffer;
using UnityEngine;
using Inputs = InputCompressor.Inputs;

public class ClientInput : MonoBehaviour
{
    private const float AcceptablePredictionErrorDistance = 0.01f;

    private Transform _cameraTransform;
    private Transform _transform;
    
    private Inputs _clientInputs;
    
    // Accessed by dirty state transport to send to the server what inputs the client has pressed
    public Inputs ClientInputs
    {
        get => _clientInputs;
    }

    private Vector3 oldPosition;
    private float lerpT = 0;

    private CharacterController _cc;
    private Vector3 _pastDelta = Vector3.zero;
    
    private LinkedList<InputState> _inputStates = new LinkedList<InputState>();

    private bool correctDesync = false;
    private Vector3 correction;

    // Records the predicted position made on a client for each state
    struct InputState
    {
        public ushort id;
        public Vector3 preMovePosition;
        public Vector3 delta;
    }

    private void Awake()
    {
        _transform = transform;
        // Initialize camera and match its rotation to the player
        _cameraTransform = GameController.PlayerCamera.transform;
        _cameraTransform.rotation = _transform.rotation;
        _cameraTransform.position = _transform.position;

        oldPosition = _transform.position;
        
        DirtyStateTransport.PreClientInputSend += DirtyStateTransportOnPreClientInputSend;
        DirtyStateTransport.PostClientInputSend += DirtyStateTransportOnPostClientInputSend;
        DirtyStateTransport.ServerPositionReceived += DirtyStateTransportOnServerPositionReceived;
    }

    private void DirtyStateTransportOnServerPositionReceived(ushort inputPacketId, Vector3 serverPosition)
    {
        // Look for a matching InputState, once a matching input state is found, set recentlyFound
        // When recently found is set, the next item's position will be compared to the server's
        // If it is different, there is a desync, set accumulate
        // If it is the same, we're good
        // When accumulate is set, we add up all of the deltas caused by the next inputs
        
        bool recentlyFound = false;
        bool found = false;
        int index = 0;
        int foundIndex = 0;
        foreach (var inputState in _inputStates)
        {
            if (recentlyFound)
            {
                recentlyFound = false;

                Vector3 quantizedPosition = new Vector3(
                    FloatQuantize.SimulateQuantize(inputState.preMovePosition.x, NetworkEntity2.PostionFactor),
                    FloatQuantize.SimulateQuantize(inputState.preMovePosition.y, NetworkEntity2.PostionFactor),
                    FloatQuantize.SimulateQuantize(inputState.preMovePosition.z, NetworkEntity2.PostionFactor));
                if ((serverPosition - quantizedPosition).magnitude > AcceptablePredictionErrorDistance)
                {
                    // Desync
                    correctDesync = true;
                    correction = serverPosition - inputState.preMovePosition;
                    Debug.Log("Desync mag: " + (serverPosition - inputState.preMovePosition).magnitude);
                }
            }

            if (!found)
            {
                if (inputState.id == inputPacketId)
                {
                    recentlyFound = true;
                    found = true;
                    foundIndex = index;
                }
            }

            index++;
        }

        // Remove input states that were before the matching input state
        if (found)
        {
            for (int i = 0; i < foundIndex; i++)
            {
                _inputStates.RemoveFirst();
            }
        }
    }

    private void DirtyStateTransportOnPreClientInputSend(ushort currentPacketId)
    {
        Vector3 delta = MovementLogic.CalculateDelta(_clientInputs, _cameraTransform.rotation, _pastDelta, _cc.isGrounded);
        _pastDelta = delta;

        // Record the packetId of we client input packet we are about to send, the keys we are sending, and the position
        // the client is at before they have been moved by these inputs
        InputState inputState;
        inputState.id = currentPacketId;
        inputState.delta = delta;
        // Store the value that would come back after the server compresses the float componenets
        inputState.preMovePosition = _transform.position;
        _inputStates.AddLast(inputState);

        oldPosition = _transform.position;

        if (correctDesync)
        {
            _cc.Move(delta + correction);
            correctDesync = false;
        }
        else
        {
            _cc.Move(delta);
        }

        lerpT = 0;
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

        // Update camera position
        //_cameraTransform.position = Vector3.MoveTowards(_cameraTransform.position, transform.position, MovementLogic.Speed * Time.deltaTime);
        //_cameraTransform.position += (transform.position - _cameraTransform.position).normalized * Time.deltaTime;
        lerpT += Time.deltaTime * DirtyStateTransport.InputSendRate;
        _cameraTransform.position = Vector3.Lerp(oldPosition + Vector3.up, transform.position + Vector3.up, lerpT);

        _cameraTransform.Rotate(-Vector3.right, Input.GetAxis("Mouse Y"), Space.Self);
        _cameraTransform.Rotate(Vector3.up, Input.GetAxis("Mouse X"), Space.World);
    }
}
