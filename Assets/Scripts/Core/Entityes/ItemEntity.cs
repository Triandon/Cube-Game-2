using System;
using Core;
using Core.Block;
using Core.Item;
using UnityEngine;

public class ItemEntity : MonoBehaviour
{
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private ChunkManager chunkManager;
    
    public ItemStack stack { get; private set; }
    
    [Header("Custom Item Physics")]
    [SerializeField] private float itemRadius = 0.14f;
    [SerializeField] private float itemHeight = 0.28f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float bounceFactor = 0.35f;
    [SerializeField] private float wallBounceFactor = 0.2f;
    [SerializeField] private float groundFriction = 10f;
    [SerializeField] private float airDrag = 0.5f;
    [SerializeField] private float sleepThreshold = 0.03f;
    [SerializeField] private float randomSpin = 120f;

    private Vector3 velocity;
    private float spinSpeed;


    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        meshFilter = GetComponent<MeshFilter>();
        chunkManager = FindAnyObjectByType<ChunkManager>();
        spinSpeed = UnityEngine.Random.Range(-randomSpin, randomSpin);
    }

    public void Init(ItemStack stack)
    {
        this.stack = new ItemStack(stack.itemId, stack.count, stack.displayName, stack.composition);

        Block block = BlockRegistry.GetBlock((byte)stack.itemId);
        if(block == null) return;

        meshFilter.mesh = ItemMeshBuilder.BuildBlockItemMesh(block);
    }
    
    public void AddInitialImpulse(Vector3 impulse)
    {
        velocity += impulse;
    }
    
    private void FixedUpdate()
        {
            if (chunkManager == null)
                return;
    
            float dt = Time.fixedDeltaTime;
            velocity.y += gravity * dt;
            velocity *= 1f / (1f + airDrag * dt);
    
            MoveAxis(velocity.x * dt, 0);
            MoveAxis(velocity.y * dt, 1);
            MoveAxis(velocity.z * dt, 2);
    
            if (IsGrounded())
            {
                velocity.x = Mathf.MoveTowards(velocity.x, 0f, groundFriction * dt);
                velocity.z = Mathf.MoveTowards(velocity.z, 0f, groundFriction * dt);
            }
    
            if (velocity.sqrMagnitude < sleepThreshold * sleepThreshold && IsGrounded())
                velocity = Vector3.zero;
    
            transform.Rotate(Vector3.up, spinSpeed * dt, Space.World);
        }
    
        private void MoveAxis(float delta, int axis)
        {
            if (Mathf.Approximately(delta, 0f))
                return;
    
            Vector3 move = axis == 0 ? new Vector3(delta, 0f, 0f) : axis == 1 ? new Vector3(0f, delta, 0f) : new Vector3(0f, 0f, delta);
            Vector3 target = transform.position + move;
            if (!IsSolidAtItemPosition(target))
            {
                transform.position = target;
                return;
            }
    
            if (axis == 1)
                velocity.y = -velocity.y * bounceFactor;
            else if (axis == 0)
                velocity.x = -velocity.x * wallBounceFactor;
            else
                velocity.z = -velocity.z * wallBounceFactor;
        }
    
        private bool IsGrounded()
        {
            return IsSolidAtHeight(transform.position + Vector3.down * 0.02f, 0f);
        }
    
        private bool IsSolidAtItemPosition(Vector3 pos)
        {
            return IsSolidAtHeight(pos, 0f) || IsSolidAtHeight(pos, itemHeight);
        }

        private bool IsSolidAtHeight(Vector3 pos, float heightOffset)
        {
            float y = pos.y + heightOffset;
            return chunkManager.CheckForVoxel(pos.x - itemRadius, y, pos.z - itemRadius) ||
                   chunkManager.CheckForVoxel(pos.x + itemRadius, y, pos.z - itemRadius) ||
                   chunkManager.CheckForVoxel(pos.x + itemRadius, y, pos.z + itemRadius) ||
                   chunkManager.CheckForVoxel(pos.x - itemRadius, y, pos.z + itemRadius);
        }
}
