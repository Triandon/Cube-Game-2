using System;
using Core;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerControllerClass : MonoBehaviour
{
    public bool isGrounded;
    public bool isSprinting;
    
    private Transform camera;
    private ChunkManager chunkManager;

    public float walkSpeed = 3f;
    public float sprintSpeed = 6f;
    public float jumpForce = 5f;
    public float gravity = -9.81f;

    public float playerWidth = 0.15f; //Radius
    
    private float horizontal;
    private float vertical;
    private float mouseHorizontal;
    private float mouseVertical;
    private Vector3 velocity;
    private float verticalMomentum = 0;
    private bool jumpRequest;

    private void Start()
    {
        camera = GameObject.Find("Camera").transform;
        chunkManager = GameObject.Find("ChunkGen").GetComponent<ChunkManager>();
    }

    private void Update()
    {
        GetPlayerInputs();
    }

    private void FixedUpdate()
    {
        CalculateVelocity();
        if (jumpRequest)
            Jump();
        
        transform.Rotate(Vector3.up * mouseHorizontal);
        camera.Rotate(Vector3.right * -mouseVertical);
        transform.Translate(velocity, Space.World);
    }

    private void CalculateVelocity()
    {
        // Affect vertical momentium with g
        if (verticalMomentum > gravity)
            verticalMomentum += Time.fixedDeltaTime * gravity;
        
        // if were spriting, use spritn
        if (isSprinting)
            velocity = ((transform.forward * vertical) + (transform.right * horizontal)) * Time.fixedDeltaTime *
                       sprintSpeed;
        else
            velocity = ((transform.forward * vertical) + (transform.right * horizontal)) * Time.fixedDeltaTime *
                       walkSpeed;
        
        // Aply vertical momentum (fall / jump)

        velocity += Vector3.up * verticalMomentum * Time.fixedDeltaTime;

        if ((velocity.z > 0 && front) || (velocity.z < 0 && back))
            velocity.z = 0;
        
        if ((velocity.x > 0 && right) || (velocity.x < 0 && left))
            velocity.x = 0;

        if (velocity.y < 0)
            velocity.y = checkDownSpeed(velocity.y);
        else if (velocity.y > 0)
            velocity.y = checkUpSpeed(velocity.y);
    }

    private void Jump()
    {
        verticalMomentum = jumpForce;
        isGrounded = false;
        jumpRequest = false;
    }

    private void GetPlayerInputs()
    {
        horizontal = Input.GetAxis("Horizontal");
        vertical = Input.GetAxis("Vertical");
        mouseHorizontal = Input.GetAxis("Mouse X");
        mouseVertical = Input.GetAxis("Mouse Y");

        if (Input.GetButtonDown("Sprint"))
            isSprinting = true;

        if (Input.GetButtonDown("Sprint"))
            isSprinting = false;

        if (isGrounded && Input.GetButtonUp("Jump"))
            jumpRequest = true;
        
        
    }

    private float checkDownSpeed(float downSpeed)
    {
        if (chunkManager.CheckForVoxel(transform.position.x - playerWidth, transform.position.y + downSpeed, transform.position.z - playerWidth) ||
            chunkManager.CheckForVoxel(transform.position.x + playerWidth, transform.position.y + downSpeed, transform.position.z - playerWidth) ||
            chunkManager.CheckForVoxel(transform.position.x + playerWidth, transform.position.y + downSpeed, transform.position.z + playerWidth) ||
            chunkManager.CheckForVoxel(transform.position.x - playerWidth, transform.position.y + downSpeed, transform.position.z + playerWidth))
        {
            isGrounded = true;
            return 0f;
        }
        else
        {
            isGrounded = false;
            return downSpeed;
        }
    }
    
    private float checkUpSpeed(float upSpeed)
    {
        if (chunkManager.CheckForVoxel(transform.position.x - playerWidth, transform.position.y + 2f + upSpeed, transform.position.z - playerWidth) ||
            chunkManager.CheckForVoxel(transform.position.x + playerWidth, transform.position.y + 2f + upSpeed, transform.position.z - playerWidth) ||
            chunkManager.CheckForVoxel(transform.position.x + playerWidth, transform.position.y + 2f + upSpeed, transform.position.z + playerWidth) ||
            chunkManager.CheckForVoxel(transform.position.x - playerWidth, transform.position.y + 2f + upSpeed, transform.position.z + playerWidth))
        {
            return 0f;
        }
        else
        {
            return upSpeed;
        }
    }

    public bool front
    {
        get
        {
            if(
                chunkManager.CheckForVoxel(transform.position.x, transform.position.y, transform.position.z + playerWidth) ||
                    chunkManager.CheckForVoxel(transform.position.x, transform.position.y + 1f, transform.position.z + playerWidth)
                )
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
    
    public bool back
    {
        get
        {
            if(
                chunkManager.CheckForVoxel(transform.position.x, transform.position.y, transform.position.z - playerWidth) ||
                chunkManager.CheckForVoxel(transform.position.x, transform.position.y + 1f, transform.position.z - playerWidth)
            )
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
    public bool left
    {
        get
        {
            if(
                chunkManager.CheckForVoxel(transform.position.x - playerWidth, transform.position.y, transform.position.z) ||
                chunkManager.CheckForVoxel(transform.position.x - playerWidth, transform.position.y + 1f, transform.position.z)
            )
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
    public bool right
    {
        get
        {
            if(
                chunkManager.CheckForVoxel(transform.position.x + playerWidth, transform.position.y, transform.position.z) ||
                chunkManager.CheckForVoxel(transform.position.x + playerWidth, transform.position.y + 1f, transform.position.z)
            )
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
