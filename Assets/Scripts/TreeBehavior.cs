using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TreeBehavior : MonoBehaviour
{
    // Called by trees when an apple is picked, first arg is tree id, second is apple id
    public static event Action<byte, byte> ApplePicked;
    public static TreeBehavior[] Trees { get; private set; } = new TreeBehavior[ushort.MaxValue];
    
    [SerializeField] private AppleBehavior[] apples;
    public byte _treeId;

    // Start is called before the first frame update
    void Start()
    {
        // Tree id is based on its sibling index, trees MUST be a child in the empty "Trees" parent
        _treeId = (byte)transform.GetSiblingIndex();
        // Register into the static tree array
        Trees[_treeId] = this;
        
        for (byte i = 0; i < apples.Length; i++)
        {
            apples[i].Id = i;
            apples[i].OnUsed += AppleOnUsed;
        }
    }

    private void AppleOnUsed(byte appleId)
    {
        apples[appleId].gameObject.SetActive(false);
        ApplePicked?.Invoke(_treeId, appleId);
    }

    public void HideApple(byte appleId)
    {
        apples[appleId].gameObject.SetActive(false);
    }
}
