using TMPro;
using UnityEngine;

public class Jump : MonoBehaviour
{
    Rigidbody rigidbody;
    public float jumpStrength = 2;
    public event System.Action Jumped;

    [SerializeField, Tooltip("Prevents jumping when the transform is in mid-air.")]
    GroundCheck groundCheck;

    private FirstPersonMovement movement;
    
    public TMP_InputField chatBox;

    void Awake()
    {
        // Get rigidbody.
        rigidbody = GetComponent<Rigidbody>();
        groundCheck = GetComponent<GroundCheck>();
        movement = GetComponent<FirstPersonMovement>();
    }

    void LateUpdate()
    {
        if (chatBox.isFocused)
            return;
        
        if(movement.creativeMode)
            return;
        
        if (!chatBox.isFocused)
        {
            // Jump when the Jump button is pressed and we are on the ground.
            if (Input.GetButtonDown("Jump") && groundCheck.isGrounded)
            {
                rigidbody.AddForce(Vector3.up * 100 * jumpStrength);
                Jumped?.Invoke();
            }
        }
    }
}
