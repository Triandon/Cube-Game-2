using Core.Crafting;
using Core.Item;
using Crafting;
using UnityEngine;

public static class RecipeDataBase
{
    static RecipeDataBase()
    {
        RegisterRecipes();
    }
    
    public static void RegisterRecipes()
    {
        RecipeManager.RegisterRecipe(new CraftingGridRecipe(
            id: "deadgrass_to_sandstone",
            patternWidth: 2,
            patternHeight: 2,
            pattern: new[]
            {
                MakeIngredient(9), MakeIngredient(9),
                MakeIngredient(9), MakeIngredient(9)
            },
            outputFactory: _ => new ItemStack(8, 2, "SandStoneBlock_Item")
        ));
        
        RecipeManager.RegisterRecipe(new CraftingGridRecipe(
            id: "sandstone_to_deadgrass",
            patternWidth: 1,
            patternHeight: 1,
            pattern: new[]
            {
                MakeIngredient(8)
            },
            outputFactory: _ => new ItemStack(9, 12)
        ));
        
        RecipeManager.RegisterRecipe(new CrushingRecipe(
            id: "deadgrass_to_sandstone_crushing",
            processType: ProcessType.Crushing,
            input: MakeIngredient(9),
            totalCrushingTime: 4,
            outputFactory: _ => new ItemStack(8, 1)
            ));
        
        RecipeManager.RegisterRecipe(new CraftingGridRecipe(
            id: "wood_to_logs",
            patternWidth: 1,
            patternHeight: 1,
            pattern: new[]
            {
                MakeIngredient(6)
            },
            outputFactory: _ => new ItemStack(27, 2)
        ));
        
        RecipeManager.RegisterRecipe(new CraftingGridRecipe(
            id: "logs_to_sticks",
            patternWidth: 1,
            patternHeight: 1,
            pattern: new[]
            {
                MakeIngredient(27)
            },
            outputFactory: _ => new ItemStack(28, 2)
        ));
        
        RecipeManager.RegisterRecipe(new CraftingGridRecipe(
            id: "wood_to_planks",
            patternWidth: 2,
            patternHeight: 2,
            pattern: new[]
            {
                MakeIngredient(6), MakeIngredient(6),
                MakeIngredient(6), MakeIngredient(6)
            },
            outputFactory: _ => new ItemStack(29, 8)
        ));

        
        Debug.Log("Blocks registered (static)");
    }
    
    private static RecipeIngredient MakeIngredient(int itemId)
    {
        return new RecipeIngredient
        {
            consume = true,
            consumeCount = 1,
            predicate = new ItemStackPredicate
            {
                requiredItemId = itemId,
                minCount = 1,
                allowEmptyComposition = true
            }
        };
    }
    
    public static void Init(){}

}
