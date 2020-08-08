using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class GameController : MonoBehaviour
{
    public GameObject PlayerPrefab;
    
    private SceneController _sceneController;
    private DirtyStateTransport _transport;
    
    // Start is called before the first frame update
    void Awake()
    {
        MenuController.OnHostPressed += Host;
        MenuController.OnConnectPressed += Connect;

        _transport = gameObject.AddComponent<DirtyStateTransport>();
        _transport.PlayerPrefab = PlayerPrefab;
        
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
    
    // TODO: Move this stuff below, into the DeltaStateTransport

    private void Connect()
    {
        _transport.StartClient();
        _transport.Client.Connected += _sceneController.UnloadMenuUI;
    }

    private void Host()
    {
        _transport.StartServer();
        _sceneController.UnloadMenuUI();
    }
}
