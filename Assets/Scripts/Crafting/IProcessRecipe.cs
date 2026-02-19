using Core.Item;
using Crafting;
using UnityEngine;

public interface IProcessRecipe
{
    string Id { get; }
    ProcessType ProcessType { get; }

    bool Matches(ProcessContext context);
    ItemStack CreateOutput(ProcessContext context);
}
