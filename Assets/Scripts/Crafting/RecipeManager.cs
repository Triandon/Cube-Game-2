using System.Collections.Generic;
using Core.Item;
using UnityEngine;

namespace Crafting
{
    public class RecipeManager
    {
        private static readonly Dictionary<ProcessType, List<IProcessRecipe>> recipesByType = new();

        public static void RegisterRecipe(IProcessRecipe recipe)
        {
            if (recipe == null)
                return;

            if (!recipesByType.TryGetValue(recipe.ProcessType, out var recipes))
            {
                recipes = new List<IProcessRecipe>();
                recipesByType[recipe.ProcessType] = recipes;
            }
            
            recipes.Add(recipe);
            Debug.Log($"Registered recipe: {recipesByType} " + recipes + recipe);
        }

        public static IProcessRecipe FindMatch(ProcessContext context)
        {
            if (context == null)
            {
                return null;
            }

            if (!recipesByType.TryGetValue(context.ProcessType, out var recipes))
            {
                return null;
            }

            foreach (var recipe in recipes)
            {
                if (recipe.Matches(context))
                {
                    return recipe;
                }
            }

            return null;
        }
        
        public static ItemStack TryCreateOutput(ProcessContext context)
        {
            var recipe = FindMatch(context);
            return recipe == null ? ItemStack.Empty : recipe.CreateOutput(context);
        }

        public static void ClearAllRecipes()
        {
            recipesByType.Clear();
        }


    }
}
