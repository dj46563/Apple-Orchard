using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AppleBehavior : MonoBehaviour
{
    public byte Id;

    public event Action<byte> OnUsed;

    public void Use()
    {
        OnUsed?.Invoke(Id);
    }
}
