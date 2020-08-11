using System;
using System.Collections;
using System.Collections.Generic;
using CircularBuffer;
using UnityEngine;
using UnityEngine.UI;

public class GameUIController : MonoBehaviour
{
    public int BufferSize = 10;
    
    public Client Client;

    [SerializeField] private Text RTTText;
    [SerializeField] private Text UpText;
    [SerializeField] private Text DownText;

    //private CircularBuffer<int> UploadBuffer;
    //private CircularBuffer<int> UploadBuffer;

    private void Awake()
    {
        //UploadBuffer = new CircularBuffer<int>(BufferSize);
    }

    private void Update()
    {
        if (Client != null)
        {
            
        }
    }

    private void UpdateRTT(int rtt)
    {
        RTTText.text = "RTT: " + rtt + " ms";
    }
    private void UpdateUpText(int rtt)
    {
        RTTText.text = "RTT: " + rtt + " ms";
    }
}
