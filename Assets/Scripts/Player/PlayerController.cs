using System;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float movementSpeed = 5f;
    public float jumpFactor = 5f;

    private Rigidbody rigidbody;
    private Vector3 moverDirection;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rigidbody = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        float horizontal = 0f;
        float vertical = 0f;

        if (Input.GetKey(KeyCode.W)) vertical += 1f;
        if (Input.GetKey(KeyCode.S)) vertical -= 1f;
        if (Input.GetKey(KeyCode.A)) horizontal -= 1f;
        if (Input.GetKey(KeyCode.D)) horizontal += 1f;

        moverDirection = (transform.forward * vertical + transform.right * horizontal).normalized;

        // Jump
        if (Input.GetKeyDown(KeyCode.Space) && IsGrounded())
        {
            rigidbody.AddForce(Vector3.up * jumpFactor, ForceMode.Impulse);
        }
    }

    private void FixedUpdate()
    {
        Vector3 velocity = moverDirection * movementSpeed;
        velocity.y = rigidbody.linearVelocity.y;
        rigidbody.linearVelocity = velocity;
    }

    private bool IsGrounded()
    {
        // Simple ground check
        return Physics.Raycast(transform.position, Vector3.down, 1.1f);
    }
}
