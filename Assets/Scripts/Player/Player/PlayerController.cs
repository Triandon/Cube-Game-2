using System;
using Core;
using UnityEngine;

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
    public float mouseSensitivity = 180f;
    public float maxLookAngle = 89f;

    public float playerWidth = 0.15f; //Radius
    public float playerHeight = 2f;
    
    private float horizontal;
    private float vertical;
    private float mouseHorizontal;
    private float mouseVertical;
    private Vector3 velocity;
    private float verticalMomentum = 0;
    private bool jumpRequest;
    private float cameraPitch;

    private void Start()
    {
        camera = GameObject.Find("Camera").transform;
        chunkManager = GameObject.Find("ChunkGen").GetComponent<ChunkManager>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        GetPlayerInputs();
        
        transform.Rotate(Vector3.up * mouseHorizontal);
        cameraPitch = Mathf.Clamp(cameraPitch - mouseVertical, -maxLookAngle, maxLookAngle);
        camera.localEulerAngles = new Vector3(cameraPitch, 0f, 0f);
    }

    private void FixedUpdate()
    {
        if (jumpRequest)
            Jump();
        
        CalculateVelocity();
    }

    private void CalculateVelocity()
    {
        // Affect vertical momentum with gravity
        if (!isGrounded || verticalMomentum > 0f)
            verticalMomentum += Time.fixedDeltaTime * gravity;
        
        float moveSpeed = isSprinting ? sprintSpeed : walkSpeed;
        Vector3 moveDirection = (transform.forward * vertical + transform.right * horizontal).normalized;
        Vector3 horizontalMove = moveDirection * (moveSpeed * Time.fixedDeltaTime);
        
        // Move horizontally in axis-separated steps so we never "step up" through walls.
        Vector3 xTarget = transform.position + new Vector3(horizontalMove.x, 0f, 0f);
        if (!IsSolidAtPlayerPosition(xTarget))
            transform.position = xTarget;

        Vector3 zTarget = transform.position + new Vector3(0f, 0f, horizontalMove.z);
        if (!IsSolidAtPlayerPosition(zTarget))
            transform.position = zTarget;
        
        // Vertical movment
        float verticalMove = verticalMomentum * Time.fixedDeltaTime;
        if (verticalMove > 0f)
        {
            float resolvedUp = checkUpSpeed(verticalMove);
            transform.position += Vector3.up * resolvedUp;
            if (resolvedUp == 0f)
                verticalMomentum = 0f;
            isGrounded = false;
        }
        else
        {
            float reslovedDown = checkDownSpeed(verticalMove);
            transform.position += Vector3.up * reslovedDown;
        }

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
        
        mouseHorizontal = Input.GetAxis("Mouse X") * mouseSensitivity * Time.fixedDeltaTime;
        mouseVertical = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.fixedDeltaTime;

        if (Input.GetButtonDown("Sprint"))
            isSprinting = true;

        if (Input.GetButtonUp("Sprint"))
            isSprinting = false;

        if (isGrounded && Input.GetButtonDown("Jump"))
            jumpRequest = true;
        
        
    }

    private float checkDownSpeed(float downSpeed)
    {
        Vector3 nextPos = transform.position + Vector3.up * downSpeed;
        if (IsSolidAtHeight(nextPos, 0f))
        {
            isGrounded = true;
            verticalMomentum = 0f;
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
        Vector3 nextPos = transform.position + Vector3.up * upSpeed;
        if (IsSolidAtHeight(nextPos, playerHeight))
        {
            return 0f;
        }
        else
        {
            return upSpeed;
        }
    }

    private bool IsSolidAtHeight(Vector3 pos, float heightOffset)
    {
        float y = pos.y + heightOffset;
        return chunkManager.CheckForVoxel(pos.x - playerWidth, y, pos.z - playerWidth) ||
               chunkManager.CheckForVoxel(pos.x + playerWidth, y, pos.z - playerWidth) ||
               chunkManager.CheckForVoxel(pos.x + playerWidth, y, pos.z + playerWidth) ||
               chunkManager.CheckForVoxel(pos.x - playerWidth, y, pos.z + playerWidth);
    }

    private bool IsSolidAtPlayerPosition(Vector3 pos)
    {
        return IsSolidAtHeight(pos, 0f) || IsSolidAtHeight(pos, 1f) || IsSolidAtHeight(pos, playerHeight - 0.05f);
    }
}
