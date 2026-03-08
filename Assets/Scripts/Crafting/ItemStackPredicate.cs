using System;
using Core.Item;
using UnityEngine;

[System.Serializable]
public class ItemStackPredicate
{
    public int requiredItemId;
    public int minCount;
    public CompositionLogic requiredComposition;
    public float compositionTolerance = 0.05f;
    public bool allowEmptyComposition = true;

    public bool Matches(ItemStack stack)
    {
        if (stack == null || stack.IsEmpty)
        {
            return false;
        }

        if (requiredItemId > 0 && stack.itemId != requiredItemId)
        {
            return false;
        }

        if (stack.count < minCount)
        {
            return false;
        }

        if (requiredComposition == null)
        {
            return true;
        }

        if (stack.composition == null)
        {
            return allowEmptyComposition;
        }

        return stack.composition.IsWithinTolerance(requiredComposition, compositionTolerance);
    }

}
