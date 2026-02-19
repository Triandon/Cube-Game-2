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
            Vector3 spawnPos =
                basePos +
                Random.insideUnitSphere * 0.3f +
                Vector3.up * 0.5f;

            GameObject go = Instantiate(
                itemEntityPrefab,
                spawnPos,
                Quaternion.identity,
                itemEntityObjectListGO.transform
            );

            go.GetComponent<ItemEntity>().Init(
                new ItemStack(stack.itemId, 1, stack.displayName, stack.composition)
            );
        }
    }
}
