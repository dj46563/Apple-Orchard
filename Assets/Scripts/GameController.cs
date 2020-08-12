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

    public static Camera PlayerCamera { get; set; }
    [SerializeField] private Camera GameCamera;
    
    // Start is called before the first frame update
    void Awake()
    {
        MenuController.OnHostPressed += Host;
        MenuController.OnLocalConnectPressed += () => Connect(Constants.DefaultLocalHost);
        MenuController.OnRemoteConnectPressed += () => Connect(Constants.DefaultRemoteHost);

        _transport = gameObject.AddComponent<DirtyStateTransport>();
        _transport.PlayerPrefab = PlayerPrefab;
        
        _sceneController = new SceneController();
        
        if (Application.isBatchMode)
        {
            Host();
        }
        else if (Constants.AutoConnect)
        {
            Connect(Constants.DefaultRemoteHost);
        }
    }

    // TODO: Move this stuff below, into the DeltaStateTransport

    private void Connect(string host)
    {
        _transport.StartClient(host);
        
        _transport.Client.Connected += () =>
        {
            _sceneController.UnloadMenuUI();
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        };

        // Setup camera for this client
        PlayerCamera = GameCamera;
        
        // Load the game UI and hookup its reference to the client so it can display network debug info
        _sceneController.LoadGameUI();
        GameUIController.OnAwake += controller => controller.Client = _transport.Client;
    }

    private void Host()
    {
        _transport.StartServer();
        _sceneController.UnloadMenuUI();
    }
}
