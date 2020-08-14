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

    private string _username;
    private uint _playerId;
    
    // Start is called before the first frame update
    void Awake()
    {
        MenuController.OnHostPressed += Host;
        MenuController.OnLocalConnectPressed += () => Connect(Constants.DefaultLocalHost, _playerId);
        MenuController.OnRemoteConnectPressed += () => Connect(Constants.DefaultRemoteHost, _playerId);

        _transport = gameObject.AddComponent<DirtyStateTransport>();
        _transport.PlayerPrefab = PlayerPrefab;
        
        _sceneController = new SceneController();
        
        if (Application.isBatchMode)
        {
            Host();
        }
        else
        {
            _sceneController.LoadLoginUI();
            LoginUIController.OnLoginSuccess += (username, id) =>
            {
                _playerId = id;
                _username = username;
                
                _sceneController.UnloadLoginUI();
                if (Constants.AutoConnect)
                {
                    Connect(Constants.DefaultRemoteHost, _playerId);
                }
                else
                {
                    _sceneController.LoadMenuUI();
                }
            };
        }
    }

    // TODO: Move this stuff below, into the DeltaStateTransport

    private void Connect(string host, uint playerId)
    {
        _transport.StartClient(host, playerId);
        
        _transport.Client.Connected += () =>
        {
            _sceneController.UnloadMenuUI();
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            // Load the game UI and hookup its reference to the client so it can display network debug info
            _sceneController.LoadGameUI();
        };

        // Setup camera for this client
        PlayerCamera = GameCamera;
        
        GameUIController.OnAwake += controller => controller.Client = _transport.Client;
    }

    private void Host()
    {
        _transport.StartServer();
        _sceneController.UnloadMenuUI();
    }
}
