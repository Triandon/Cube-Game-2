using UnityEngine;

[RequireComponent(typeof(Animator))]
public class VoxelWanderAI : MonoBehaviour
{
    public float moveSpeed = 2f;
    public float turnSpeed = 180f;
    public float decisionTime = 5f;
    public float checkDistance = 2f;

    [Header("Idle Settings")]
    public float minIdleTime = 2f;
    public float maxIdleTime = 6f;
    public float idleChance = 0.3f; // 30% chance to idle

    private float timer;
    private float idleTimer;
    private float currentIdleDuration;
    private bool isIdling;

    private Vector3 currentDirection;
    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
        PickNewDirection();
    }

    void Update()
    {
        if (isIdling)
        {
            idleTimer += Time.deltaTime;

            if (idleTimer >= currentIdleDuration)
            {
                isIdling = false;
                PickNewDirection();
            }

            return;
        }

        timer += Time.deltaTime;

        if (timer >= decisionTime)
        {
            timer = 0f;

            // Randomly decide to idle
            if (Random.value < idleChance)
            {
                StartIdle();
                return;
            }

            PickNewDirection();
        }

        bool blocked = IsBlocked();

        if (blocked)
        {
            PickNewDirection();
        }

        // Rotate
        Quaternion targetRot = Quaternion.LookRotation(currentDirection);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRot,
            turnSpeed * Time.deltaTime
        );

        // Move
        transform.position += transform.forward * moveSpeed * Time.deltaTime;

        animator.SetFloat("speed", 1f);
    }

    void StartIdle()
    {
        isIdling = true;
        idleTimer = 0f;
        currentIdleDuration = Random.Range(minIdleTime, maxIdleTime);

        animator.SetFloat("speed", 0f);
    }

    void PickNewDirection()
    {
        Vector2 rand = Random.insideUnitCircle.normalized;
        currentDirection = new Vector3(rand.x, 0, rand.y);
    }

    bool IsBlocked()
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;

        if (Physics.Raycast(origin, transform.forward, checkDistance))
            return true;

        return false;
    }
}
