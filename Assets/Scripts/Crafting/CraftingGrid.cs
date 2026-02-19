using System;
using System.Collections.Generic;
using Core.Item;
using UnityEngine;

namespace Crafting
{
    [SerializeField]
    public class CraftingGrid
    {
        public int Width { get; }
        public int Height { get; }

        private readonly ItemStack[] slots;

        public CraftingGrid(int width, int height, ItemStack[] existing = null)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Crafting grid dimensions must be positive!"); 
            }

            Width = width;
            Height = height;
            slots = new ItemStack[Width * Height];

            for (int i = 0; i < slots.Length; i++)
            {
                if (existing != null && i < existing.Length && existing[i] != null)
                {
                    slots[i] = existing[i];
                }
                else
                {
                    slots[i] = ItemStack.Empty;
                }
            }
        }

        public ItemStack GetItemStack(int x, int y)
        {
            return slots[ToIndex(x, y)];
        }

        public void SetItemStack(int x, int y, ItemStack stack)
        {
            slots[ToIndex(x, y)] = stack ?? ItemStack.Empty;
        }

        public IEnumerable<(int x, int y, ItemStack stack)> Enumerable()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    yield return (x, y, GetItemStack(x, y));
                }
            }
        }

        private int ToIndex(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                throw new ArgumentOutOfRangeException($"Grid index out of range ({x},{y})");
            }

            return y * Width + x;
        }
    }
}
