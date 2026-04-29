using System;
using Core;
using TMPro;
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
    public float mouseSensitivity = 2;
    public float mouseSmoothing = 1.5f;
    public float maxLookAngle = 89f;

    public float playerWidth = 0.15f; //Radius
    public float playerHeight = 2f;
    
    [Header(("Ground settings"))]
    public float groundProbeDistance = 0.05f;
    public float groundedGraceTime = 0.08f;
    
    [Header(("Creative mode settings"))] 
    public float flySpeed = 8f;
    public float runFlySpeed = 29;
    public bool creativeMode = false;
    
    private float horizontal;
    private float vertical;
    private float flyVertical;
    private Vector2 lookVelocity;
    private Vector2 lookFrameVelocity;
    private float verticalMomentum = 0;
    private bool jumpRequest;
    private CursorLockMode lastLockState;
    private float lastGroundedTime;
    public TMP_InputField chatBox;

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
        HandleCameraRotation();
    }

    private void HandleCameraRotation()
    {
        CursorLockMode lockMode = Input.GetKey(KeyCode.LeftAlt) ? CursorLockMode.None : CursorLockMode.Locked;
        if (lastLockState != lockMode)
        {
            Cursor.lockState = lockMode;
            Cursor.visible = lockMode != CursorLockMode.Locked;
            lastLockState = lockMode;
        }

        if (lockMode == CursorLockMode.Locked)
        {
            // Get smooth velocity.
            Vector2 mouseDelta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
            Vector2 rawFrameVelocity = Vector2.Scale(mouseDelta, Vector2.one * mouseSensitivity);
            lookFrameVelocity = Vector2.Lerp(lookFrameVelocity, rawFrameVelocity, 1 / mouseSmoothing);
            lookVelocity += lookFrameVelocity;
            lookVelocity.y = Mathf.Clamp(lookVelocity.y, -maxLookAngle, maxLookAngle);

            // Rotate camera up-down and controller left-right from velocity.
            transform.localRotation = Quaternion.AngleAxis(lookVelocity.x, Vector3.up);
            camera.localRotation = Quaternion.AngleAxis(-lookVelocity.y, Vector3.right);
        }
    }

    private void FixedUpdate()
    {
        if (creativeMode)
        {
            CalculateCreativeVelocity();
            return;
        }
        
        if (jumpRequest)
            Jump();
        
        CalculateVelocity();
    }

    private void CalculateVelocity()
    {
        // Affect vertical momentum with gravity
        if (!isGrounded || verticalMomentum > 0f)
            verticalMomentum += Time.fixedDeltaTime * gravity;
        else if (verticalMomentum < 0f)
            verticalMomentum = 0f;
        
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
        flyVertical = 0f;

        if (Input.GetButton("Jump"))
            flyVertical += 1f;
        if (Input.GetKey(KeyCode.LeftControl))
            flyVertical -= 1f;
        
        if (Input.GetButtonDown("Sprint") && isGrounded)
            isSprinting = true;

        if (Input.GetButtonUp("Sprint"))
            isSprinting = false;

        if (isGrounded && Input.GetButtonDown("Jump"))
            jumpRequest = true;

        if (Input.GetKeyDown(KeyCode.C))
        {
            creativeMode = !creativeMode;
            verticalMomentum = 0f;
            jumpRequest = false;
            isGrounded = false;
        }
        
    }

    private void CalculateCreativeVelocity()
    {
        float speed = isSprinting ? runFlySpeed : flySpeed;
        Vector3 moveDirection =
            (transform.forward * vertical + transform.right * horizontal + Vector3.up * flyVertical).normalized;
        Vector3 move = moveDirection * (speed * Time.fixedDeltaTime);

        Vector3 xTarget = transform.position + new Vector3(move.x, 0f, 0f);
        if (!IsSolidAtPlayerPosition(xTarget))
            transform.position = xTarget;

        Vector3 yTarget = transform.position + new Vector3(0f, move.y, 0f);
        if (!IsSolidAtPlayerPosition(yTarget))
            transform.position = yTarget;

        Vector3 zTarget = transform.position + new Vector3(0f, 0f, move.z);
        if (!IsSolidAtPlayerPosition(zTarget))
            transform.position = zTarget;
    }

    private float checkDownSpeed(float downSpeed)
    {
        Vector3 nextPos = transform.position + Vector3.up * downSpeed;

        bool touchingGround = IsSolidAtHeight(nextPos, 0f);
        bool closeToGround = IsSolidAtHeight(nextPos + Vector3.down * groundProbeDistance, 0f);
        
        if (touchingGround)
        {
            isGrounded = true;
            lastGroundedTime = Time.time;
            verticalMomentum = 0f;
            return 0f;
        }

        if (downSpeed <= 0f && closeToGround)
        {
            isGrounded = true;
            lastGroundedTime = Time.time;
            verticalMomentum = 0;
            return 0f;
        }

        if (Time.time - lastGroundedTime <= groundedGraceTime)
        {
            isGrounded = true;
            return downSpeed;
        }

        isGrounded = false;
        return downSpeed;
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
