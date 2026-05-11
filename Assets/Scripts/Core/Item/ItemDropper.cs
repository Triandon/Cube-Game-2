using System;
using Core.Item;
using UnityEngine;
using Random = UnityEngine.Random;

public class ItemDropper : MonoBehaviour
{
    public static ItemDropper Instance { get; private set; }

    [SerializeField] private GameObject itemEntityPrefab;
    [SerializeField] private GameObject itemEntityObjectListGO;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void DropItemStack(ItemStack stack, Vector3 basePos)
    {
        if (stack.IsEmpty) return;

        for (int i = 0; i < stack.count; i++)
        {
            Vector2 horizontalOffset = Random.insideUnitCircle * 0.25f;
            Vector3 spawnPos = basePos + new Vector3(horizontalOffset.x, 0.7f, horizontalOffset.y);

            GameObject go = Instantiate(
                itemEntityPrefab,
                spawnPos,
                Quaternion.identity,
                itemEntityObjectListGO.transform
            );

            ItemEntity itemEntity = go.GetComponent<ItemEntity>();
            itemEntity.Init(
                new ItemStack(stack.itemId, 1, stack.displayName, stack.composition));
            Vector3 popImpulse = Random.insideUnitSphere * 1.75f + Vector3.up * 2.2f;
            itemEntity.AddInitialImpulse(popImpulse);
        }
    }
}
