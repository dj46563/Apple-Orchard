using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Used to disable on renderers on an object
public class DisableRendering : MonoBehaviour
{
    public void Disable()
    {
        GetComponent<Renderer>().enabled = false;
        foreach (var renderer in GetComponentsInChildren<Renderer>())
        {
            renderer.enabled = false;
        }
    }

    public void Enable()
    {
        GetComponent<Renderer>().enabled = true;
        foreach (var renderer in GetComponentsInChildren<Renderer>())
        {
            renderer.enabled = true;
        }
    }
}
