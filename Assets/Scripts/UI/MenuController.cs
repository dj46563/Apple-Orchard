using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MenuController : MonoBehaviour
{
    [SerializeField] private Text statusText;

    public static event Action OnHostPressed;
    public static event Action OnLocalConnectPressed;
    public static event Action OnRemoteConnectPressed;

    public void HostPressed()
    {
        OnHostPressed?.Invoke();
    }

    public void LocalConnectPressed()
    {
        OnLocalConnectPressed?.Invoke();
    }

    public void RemoteConnectPressed()
    {
        OnRemoteConnectPressed?.Invoke();
    }
}
