using Core.Item;
using UnityEngine;

[SerializeField]
public class RecipeIngredient
{
    public ItemStackPredicate predicate = new ItemStackPredicate();
    public int consumeCount = 1;
    public bool consume = true;

    public bool Matches(ItemStack stack)
    {
        return predicate != null && predicate.Matches(stack);
    }
}
