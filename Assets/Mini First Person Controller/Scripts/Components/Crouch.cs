using UnityEngine;

public class Crouch : MonoBehaviour
{
    public KeyCode key = KeyCode.LeftControl;

    [Header("Slow Movement")]
    [Tooltip("Movement to slow down when crouched.")]
    public FirstPersonMovement movement;
    [Tooltip("Movement speed when crouched.")]
    public float movementSpeed = 2;

    [Header("Low Head")]
    [Tooltip("Head to lower when crouched.")]
    public Transform headToLower;
    [HideInInspector]
    public float? defaultHeadYLocalPosition;
    public float crouchYHeadPosition = 1;
    
    [Tooltip("Collider to lower when crouched.")]
    public BoxCollider colliderToLower;
    [HideInInspector]
    public float? defaultColliderHeight;

    public bool IsCrouched { get; private set; }
    public event System.Action CrouchStart, CrouchEnd;

    [HideInInspector] public Vector3? defaultColliderSize;
    [HideInInspector] public Vector3? defaultColliderCenter;
    
    void Reset()
    {
        // Try to get components.
        movement = GetComponentInParent<FirstPersonMovement>();
        headToLower = movement.GetComponentInChildren<Camera>().transform;
        colliderToLower = movement.GetComponentInChildren<BoxCollider>();
    }

    void LateUpdate()
    {
        if(movement.creativeMode)
            return;
        
        if (Input.GetKey(key))
        {
            // Enforce a low head.
            if (headToLower)
            {
                // If we don't have the defaultHeadYLocalPosition, get it now.
                if (!defaultHeadYLocalPosition.HasValue)
                {
                    defaultHeadYLocalPosition = headToLower.localPosition.y;
                }

                // Lower the head.
                headToLower.localPosition = new Vector3(headToLower.localPosition.x, crouchYHeadPosition, headToLower.localPosition.z);
            }

            // Enforce a low colliderToLower.
            if (colliderToLower)
            {
                if (!defaultColliderSize.HasValue)
                {
                    defaultColliderSize = colliderToLower.size;
                    defaultColliderCenter = colliderToLower.center;
                }

                float loweringAmount = defaultHeadYLocalPosition.HasValue
                    ? defaultHeadYLocalPosition.Value - crouchYHeadPosition
                    : defaultColliderSize.Value.y * 0.5f;

                Vector3 newSize = defaultColliderSize.Value;
                newSize.y = Mathf.Max(defaultColliderSize.Value.y - loweringAmount, 0.1f);
                colliderToLower.size = newSize;

                colliderToLower.center = defaultColliderCenter.Value
                                         - Vector3.up * (loweringAmount * 0.5f);
            }


            // Set IsCrouched state.
            if (!IsCrouched)
            {
                IsCrouched = true;
                SetSpeedOverrideActive(true);
                CrouchStart?.Invoke();
            }
        }
        else
        {
            if (IsCrouched)
            {
                // Rise the head back up.
                if (headToLower)
                {
                    headToLower.localPosition = new Vector3(headToLower.localPosition.x, defaultHeadYLocalPosition.Value, headToLower.localPosition.z);
                }

                // Reset the colliderToLower's height.
                if (colliderToLower)
                {
                    colliderToLower.size = defaultColliderSize.Value;
                    colliderToLower.center = defaultColliderCenter.Value;
                }


                // Reset IsCrouched.
                IsCrouched = false;
                SetSpeedOverrideActive(false);
                CrouchEnd?.Invoke();
            }
        }
    }


    #region Speed override.
    void SetSpeedOverrideActive(bool state)
    {
        // Stop if there is no movement component.
        if(!movement)
        {
            return;
        }

        // Update SpeedOverride.
        if (state)
        {
            // Try to add the SpeedOverride to the movement component.
            if (!movement.speedOverrides.Contains(SpeedOverride))
            {
                movement.speedOverrides.Add(SpeedOverride);
            }
        }
        else
        {
            // Try to remove the SpeedOverride from the movement component.
            if (movement.speedOverrides.Contains(SpeedOverride))
            {
                movement.speedOverrides.Remove(SpeedOverride);
            }
        }
    }

    float SpeedOverride() => movementSpeed;
    #endregion
}
