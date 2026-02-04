using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;
    
    [Header("Movement")]
    public float walkSpeed = 6f;
    public float runSpeed = 12f;
    public float crouchSpeed = 3f;
    public float jumpPower = 7f;
    public float gravity = 10f;
    
    [Header("Camera/Look")]
    public float mouseSensitivity = 2f;
    public float maxLookUpAngle = 90f;
    public float maxLookDownAngle = 90f;
    
    [Header("Crouch")]
    public float defaultHeight = 2f;
    public float crouchHeight = 1f;

    private Vector3 moveDirection = Vector3.zero;
    private float rotationX = 0f;
    private CharacterController characterController;
    private bool isCrouching = false;
    private bool canMove = true;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        
        // Lock and hide cursor for first-person experience
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleMovement();
        HandleCamera();
        HandleCrouch();
    }

    void HandleMovement()
    {
        // Get movement input (WASD)
        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);

        bool isRunning = Input.GetKey(KeyCode.LeftShift) && !isCrouching;
        
        // Get input from WASD keys
        float verticalInput = canMove ? Input.GetAxis("Vertical") : 0f;  // W/S
        float horizontalInput = canMove ? Input.GetAxis("Horizontal") : 0f; // A/D
        
        float currentSpeed = isRunning ? runSpeed : walkSpeed;
        if (isCrouching)
            currentSpeed = crouchSpeed;
        
        float curSpeedX = verticalInput * currentSpeed;
        float curSpeedY = horizontalInput * currentSpeed;
        
        float movementDirectionY = moveDirection.y;
        moveDirection = (forward * curSpeedX) + (right * curSpeedY);
        moveDirection.y = movementDirectionY;

        // Jumping
        if (Input.GetButton("Jump") && canMove && characterController.isGrounded)
        {
            moveDirection.y = jumpPower;
        }

        // Apply gravity
        if (!characterController.isGrounded)
        {
            moveDirection.y -= gravity * Time.deltaTime;
        }

        // Move the character
        characterController.Move(moveDirection * Time.deltaTime);
    }

    void HandleCamera()
    {
        if (!canMove)
            return;

        // Get mouse input
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Rotate body left/right (yaw)
        transform.Rotate(0, mouseX, 0);

        // Rotate camera up/down (pitch)
        rotationX -= mouseY;
        rotationX = Mathf.Clamp(rotationX, -maxLookDownAngle, maxLookUpAngle);
        playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);

        // Unlock cursor with ESC key (for debugging)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void HandleCrouch()
    {
        if (Input.GetKeyDown(KeyCode.R) && characterController.isGrounded && canMove)
        {
            isCrouching = !isCrouching;
        }

        if (isCrouching)
        {
            characterController.height = crouchHeight;
        }
        else
        {
            characterController.height = defaultHeight;
        }
    }
}