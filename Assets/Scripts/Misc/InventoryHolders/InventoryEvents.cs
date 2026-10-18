using System;
using Misc.InventoryHolders;
using UnityEngine;

public static class InventoryEvents
{
    public static event Action<Inventory, IInventoryOwner> OnInventoryDisplayRequested;
    public static event Action<IInventoryOwner> OnInventoryClosed;

    public static void Open(IInventoryOwner owner)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (owner.Inventory == null)
            throw new InvalidOperationException("Initialize the owner's inventory before opening it.");

        OnInventoryDisplayRequested?.Invoke(owner.Inventory, owner);
    }

    public static void Close(IInventoryOwner owner)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));

        OnInventoryClosed?.Invoke(owner);
    }
}
