using UnityEngine;

public class GroundCheck : MonoBehaviour
{
    [Tooltip("Layers considered as ground.")]
    public LayerMask groundLayer;

    [Tooltip("Height of the ground check box (slightly below feet).")]
    public float extraHeight = 0.1f;

    [Tooltip("Width and depth of the ground check box.")]
    public Vector3 boxSize = new Vector3(0.5f, 0.1f, 0.5f);

    public bool isGrounded;

    private Collider col;

    void Awake()
    {
        col = GetComponent<Collider>();
        if (!col)
            Debug.LogError("GroundCheck requires a Collider on the same GameObject.");
    }

    public bool CheckGround()
    {
        if (!col) return false;

        // Position the check box at the bottom of the collider
        Vector3 center = col.bounds.center;
        center.y = col.bounds.min.y + extraHeight / 2f;

        // Check for overlaps
        isGrounded = Physics.CheckBox(center, boxSize / 2f, Quaternion.identity, groundLayer);
        return isGrounded;
    }

    void Update()
    {
        CheckGround();
    }

    // Visualize in editor
    void OnDrawGizmosSelected()
    {
        if (!col) return;
        Vector3 center = col.bounds.center;
        center.y = col.bounds.min.y + extraHeight / 2f;
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireCube(center, boxSize);
    }
}