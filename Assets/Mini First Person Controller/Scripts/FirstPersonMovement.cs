using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class FirstPersonMovement : MonoBehaviour
{
    public float speed = 5;

    [Header("Running")]
    public bool canRun = true;
    public bool IsRunning { get; private set; }
    public float runSpeed = 9;
    public KeyCode runningKey = KeyCode.LeftShift;
    
    public bool creativeMode = false;

    [Header(("Creative mode settings"))] 
    public float flySpeed = 8f;
    public float runFlySpeed = 29;

    Rigidbody rigidbody;
    /// <summary> Functions to override movement speed. Will use the last added override. </summary>
    public List<System.Func<float>> speedOverrides = new List<System.Func<float>>();

    public TMP_InputField chatBox;

    void Awake()
    {
        // Get the rigidbody on this.
        rigidbody = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            creativeMode = !creativeMode;
        }
    }

    void FixedUpdate()
    {
        if(chatBox.isFocused)
            return;
        
        // Update IsRunning from input.
        IsRunning = canRun && Input.GetKey(runningKey);

        // Get targetMovingSpeed.
        float targetMovingSpeed = IsRunning ? runSpeed : speed;
        if (speedOverrides.Count > 0)
        {
            targetMovingSpeed = speedOverrides[speedOverrides.Count - 1]();
        }
        
        // Get target fly speed.
        float targetFlySpeed = IsRunning ? runFlySpeed : flySpeed;
        if (speedOverrides.Count > 0)
        {
            targetFlySpeed = speedOverrides[speedOverrides.Count - 1]();
        }

        // Get targetVelocity from input.
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        Vector3 velocity;

        if (creativeMode)
        {
            float y = 0f;

            if (Input.GetKey(KeyCode.Space))
            {
                y += 1f;
            }

            if (Input.GetKey(KeyCode.LeftControl))
            {
                y -= 1f;
            }

            velocity = transform.rotation * new Vector3(
                x * targetFlySpeed,
                y * targetFlySpeed,
                z * targetFlySpeed);

            rigidbody.useGravity = false;
        }
        else
        {
            velocity = transform.rotation * new Vector3(
                x * targetMovingSpeed,
                rigidbody.linearVelocity.y,
                z * targetMovingSpeed);

            rigidbody.useGravity = true;
        }
        
        // Apply movement.
        rigidbody.linearVelocity = velocity;
    }
}