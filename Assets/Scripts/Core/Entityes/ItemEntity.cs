using System;
using Core.Block;
using Core.Item;
using UnityEngine;

public class ItemEntity : MonoBehaviour
{
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    
    public ItemStack stack { get; private set; }

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        meshFilter = GetComponent<MeshFilter>();
    }

    public void Init(ItemStack stack)
    {
        this.stack = new ItemStack(stack.itemId, stack.count, stack.displayName);

        Block block = BlockRegistry.GetBlock((byte)stack.itemId);
        if(block == null) return;

        meshFilter.mesh = ItemMeshBuilder.BuildBlockItemMesh(block);
    }
    
}
