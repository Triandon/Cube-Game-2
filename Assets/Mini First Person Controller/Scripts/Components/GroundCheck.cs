using UnityEngine;

[ExecuteInEditMode]
public class GroundCheck : MonoBehaviour
{
    [Tooltip("Maximum distance from the ground.")]
    public float distanceThreshold = .15f;

    [Tooltip("Whether this transform is grounded now.")]
    public bool isGrounded = true;
    /// <summary>
    /// Called when the ground is touched again.
    /// </summary>
    public event System.Action Grounded;

    private BoxCollider playerCollider;
    private LayerMask groundMask;
    public float extraHeight = 0.1f;


    void Awake()
    {
        playerCollider = GetComponent<BoxCollider>();
        // Set the layer mask to Default (or whatever layer your ground uses)
        groundMask = LayerMask.GetMask("Default");
    }

    void LateUpdate()
    {
        if (!playerCollider) return;

        // Calculate box position and size
        Vector3 boxCenter = playerCollider.bounds.center;
        Vector3 boxHalfExtents = new Vector3(playerCollider.size.x / 2, extraHeight / 2, playerCollider.size.z / 2);

        // Move box just below the player
        boxCenter.y = playerCollider.bounds.min.y - extraHeight / 2;

        // Check for overlaps
        Collider[] hits = Physics.OverlapBox(boxCenter, boxHalfExtents, Quaternion.identity, groundMask, QueryTriggerInteraction.Ignore);
        bool isGroundedNow = hits.Length > 0;

        // Call event if we just hit the ground
        if (isGroundedNow && !isGrounded)
        {
            Grounded?.Invoke();
        }

        isGrounded = isGroundedNow;
    }

    void OnDrawGizmosSelected()
    {
        if (!playerCollider) playerCollider = GetComponent<BoxCollider>();
        if (!playerCollider) return;

        Vector3 boxCenter = playerCollider.bounds.center;
        Vector3 boxSize = new Vector3(playerCollider.size.x, extraHeight, playerCollider.size.z);
        boxCenter.y = playerCollider.bounds.min.y - extraHeight / 2;

        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireCube(boxCenter, boxSize);
    }
}
