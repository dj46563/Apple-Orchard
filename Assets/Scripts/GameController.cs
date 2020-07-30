using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class GameController : MonoBehaviour
{
    public bool IsServer { get; private set; }

    private Server _server;
    private Client _client;

    private SceneController _sceneController;
    
    // Start is called before the first frame update
    void Awake()
    {
        MenuController.OnHostPressed += Host;
        MenuController.OnConnectPressed += Connect;
        
        if (Application.isBatchMode)
        {
            Host();
        }
        else 
        {
            _sceneController = new SceneController();

            if (Constants.AutoConnect)
            {
                Connect();
            }
        }
    }

    private void LateUpdate()
    {
        _server?.PollEvents();
        _client?.PollEvents();
    }

    private void Connect()
    {
        IsServer = false;
        _client = new Client();
        _client.Connected += _sceneController.UnloadMenuUI;
        _client.Connect(Constants.DefaultHost, Constants.DefaultPort);

        StateTransport _stateTransport = gameObject.AddComponent<StateTransport>();
        _stateTransport.SetClient(_client);
    }

    private void Host()
    {
        IsServer = true;
        _server = new Server();
        _server.Listen(Constants.DefaultPort);
        _sceneController.UnloadMenuUI();
        
        StateTransport _stateTransport = gameObject.AddComponent<StateTransport>();
        _stateTransport.SetServer(_server);
    }
}
