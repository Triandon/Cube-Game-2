using System;
using Core.Item;
using UnityEngine;

namespace Player
{
    public class PlayerEntity : MonoBehaviour
    {
        [Header("Vitals")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float health = 100f;
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float stamina = 100f;
        [SerializeField] private float maxEndurance = 100f;
        [SerializeField] private float endurance = 100f;
        
        [Header("Movement")]
        [SerializeField] private float maxWalkSpeed = 5;
        [SerializeField] private float walkSpeed = 5;
        [SerializeField] private float maxSprintSpeed = 8;
        [SerializeField] private float sprintSpeed = 8;
        [SerializeField] private float jumpStrength = 5f;
        [SerializeField] private bool isGrounded;

        [Header("World State")] [SerializeField]
        private Vector3 worldPosition;
        private Vector3 velocity;

        [Header("Held Item")]
        [SerializeField] private ItemStack heldItemStack = ItemStack.Empty;

        private HotBarUI hotBarUI;
        
        public float Health => health;
        public float MaxHealth => maxHealth;
        
        public float Stamina => stamina;
        public float MaxStamina => maxStamina;
        
        public float Endurance => endurance;
        public float MaxEndurance => maxEndurance;

        public float WalkSpeed => walkSpeed;
        public float MaxWalkSpeed => maxWalkSpeed;
        
        public float SprintSpeed => sprintSpeed;
        public float MaxSprintSpeed => maxSprintSpeed;

        
        public float JumpStrength => jumpStrength;

        public bool IsGrounded => isGrounded;
        public bool IsAlive => health > 0f;

        public Vector3 WorldPosition => worldPosition;
        public Vector3 Velocity => velocity;

        private void Start()
        {
            hotBarUI = FindAnyObjectByType<HotBarUI>();
        }

        public ItemStack GetHeldItemStack()
        {
            if (hotBarUI != null)
            {
                ItemStack selected = hotBarUI.GetSelectedStack();
                return selected ?? ItemStack.Empty;
            }

            return heldItemStack ?? ItemStack.Empty;
        }

        public void SetHeldItemStack(ItemStack stack)
        {
            heldItemStack = stack ?? ItemStack.Empty;
        }

        public void BindHotBar(HotBarUI hotbar)
        {
            hotBarUI = hotbar;
        }

        public void RefreshHeldItemFromHotBar()
        {
            if (hotBarUI == null)
            {
                return;
            }

            heldItemStack = hotBarUI.GetSelectedStack() ?? ItemStack.Empty;
        }
        
        public bool IsHoldingItem(Item item)
        {
            ItemStack stack = GetHeldItemStack();
            if (item == null || stack.IsEmpty)
            {
                return false;
            }

            return stack.Item == item;
        }

        public bool IsHoldingItemId(int itemId)
        {
            ItemStack stack = GetHeldItemStack();
            if (stack.IsEmpty)
            {
                return false;
            }

            return stack.itemId == itemId;
        }


        public void SetWorldState(Vector3 position, Vector3 currentVelocity, bool grounded)
        {
            worldPosition = position;
            velocity = currentVelocity;
            isGrounded = grounded;
        }

        public void Heal(float amount)
        {
            health = Mathf.Clamp(health + Mathf.Max(amount, 0f), 0f, maxHealth);
        }

        public void DamagePlayer(float amount)
        {
            health = Mathf.Clamp(health - Mathf.Max(amount, 0f), 0f, maxHealth);
        }

        public void UseStamina(float amount)
        {
            stamina = Mathf.Clamp(stamina - Mathf.Max(amount, 0f), 0f, maxStamina);
        }

        public void RestoreStamina(float amount)
        {
            stamina = Mathf.Clamp(stamina + Mathf.Max(amount, 0f), 0f, maxStamina);
        }

        public void UseEndurance(float amount)
        {
            endurance = Mathf.Clamp(endurance - Mathf.Max(amount, 0f), 0f, maxEndurance);
        }

        public void RestoreEndurance(float amount)
        {
            endurance = Mathf.Clamp(endurance + Mathf.Max(amount, 0f), 0f, maxEndurance);
        }

    }
}
