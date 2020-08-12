using System;
using System.Collections;
using System.Collections.Generic;
using CircularBuffer;
using UnityEngine;
using UnityEngine.UI;

public class GameUIController : MonoBehaviour
{
    public static event Action<GameUIController> OnAwake;

    public int BufferSize = 10;
    
    public Client Client;

    [SerializeField] private Text RTTText;
    [SerializeField] private Text UpText;
    [SerializeField] private Text DownText;

    private uint upCount = 0;
    private uint downCount = 0;

    private void Awake()
    {
        OnAwake?.Invoke(this);
    }

    private void Start()
    {
        if (Client != null)
        {
            Client.PacketSent += bytes => upCount += (uint)bytes.Length;
            Client.PacketReceived += bytes => downCount += (uint) bytes.Length;
            
            StartCoroutine(UpdateEverySecond());
        }
    }

    private void Update()
    {
        if (Client != null)
        {
            RTTText.text = "RTT: " + Client.GetRTT() + " ms";
        }
    }

    private IEnumerator UpdateEverySecond()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            
            UpText.text = "Up: " + upCount + " bytes/s";
            DownText.text = "Down: " + downCount + " bytes/s";

            upCount = 0;
            downCount = 0;
        }
    }
}
