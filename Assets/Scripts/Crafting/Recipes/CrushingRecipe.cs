using System;
using Core.Item;
using Crafting;
using UnityEngine;

[SerializeField]
public class CrushingRecipe : IProcessRecipe
{
    public string Id { get; }
    public ProcessType ProcessType { get; }

    private readonly RecipeIngredient input;
    private readonly Func<ProcessContext, ItemStack> outputFactory;

    public CrushingRecipe(string id, ProcessType processType, RecipeIngredient input,
        Func<ProcessContext, ItemStack> outputFactory)
    {
        Id = id;
        ProcessType = processType;
        this.input = input;
        this.outputFactory = outputFactory;
    }
    
    public bool Matches(ProcessContext context)
    {
        if (context == null || context.ProcessType != ProcessType)
        {
            return false;
        }

        if (context.InputSlots == null || context.InputSlots.Length == 0)
        {
            return false;
        }

        return input != null && input.Matches(context.InputSlots[0]);

    }

    public ItemStack CreateOutput(ProcessContext context)
    {
        return outputFactory != null ? outputFactory(context) : ItemStack.Empty;
    }
}
