using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MenuController : MonoBehaviour
{
    [SerializeField] private Text statusText;

    public static event Action OnHostPressed;
    public static event Action OnConnectPressed;

    public void HostPressed()
    {
        OnHostPressed?.Invoke();
    }

    public void ConnectPressed()
    {
        OnConnectPressed?.Invoke();
    }
}
