using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class GameController : MonoBehaviour
{
    private SceneController _sceneController;
    private DeltaStateTransport _deltaStateTransport;
    
    // Start is called before the first frame update
    void Awake()
    {
        MenuController.OnHostPressed += Host;
        MenuController.OnConnectPressed += Connect;

        _deltaStateTransport = gameObject.AddComponent<DeltaStateTransport>();
        
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
        _deltaStateTransport.StartClient();
        _deltaStateTransport.Client.Connected += _sceneController.UnloadMenuUI;
    }

    private void Host()
    {
        _deltaStateTransport.StartServer();
        _sceneController.UnloadMenuUI();
    }
}
