using UnityEngine;
using UnityEngine.InputSystem; // New Input System namespace

public class Player : MonoBehaviour
{
    // Camera controls
    public float mouseSensitivity = 0.5f;
    private float verticalRotation = 0f;
    private Transform cameraTransform;

    // Player movement
    private Rigidbody rb;
    public float moveSpeed = 10f;
    private Vector2 moveInput;

    // Jumping
    public float jumpForce = 10f;
    public float fallMultiplier = 2.5f;
    public float ascendMultiplier = 2f;
    private bool isGrounded = true;
    public LayerMask groundLayer;
    private float groundCheckTimer = 0f;
    private float groundCheckInterval = 0.2f;
    private float playerHeight;
    public float raycastDistance = 1.2f;

    // Input Actions
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction interactAction;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        cameraTransform = Camera.main.transform;

        // Raycast setup
        playerHeight = GetComponent<CapsuleCollider>().height * transform.localScale.y;

        // Setup PlayerInput
        playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions["Move"];
        lookAction = playerInput.actions["Look"];
        jumpAction = playerInput.actions["Jump"];
        interactAction = playerInput.actions["Interact"];

        //hidden mouse if needed -- comment before build if needed
        Cursor.lockState = CursorLockMode.Locked; 
        Cursor.visible = false;

    }

    void OnEnable()
    {
        jumpAction.performed += OnJump;
    }

    void OnDisable()
    {
        jumpAction.performed -= OnJump;
    }

    void Update()
    {
        moveInput = moveAction.ReadValue<Vector2>();
        RotateCamera();

        // Ground check with timer
        if (!isGrounded && groundCheckTimer <= 0f)
        {
            Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
            isGrounded = Physics.Raycast(rayOrigin, Vector3.down, raycastDistance, groundLayer);
        }
        else
        {
            groundCheckTimer -= Time.deltaTime;
        }
    }

    void FixedUpdate()
    {
        Move();
        ApplyJump();
    }

    void Move()
    {
        Vector3 movement = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;
        Vector3 targetVelocity = movement * moveSpeed;

        Vector3 velocity = rb.linearVelocity;
        velocity.x = targetVelocity.x;
        velocity.z = targetVelocity.z;
        rb.linearVelocity = velocity;

        if (isGrounded && moveInput == Vector2.zero)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }
    }

    void RotateCamera()
    {
        Vector2 lookInput = lookAction.ReadValue<Vector2>();

        float horizontalRotation = lookInput.x * mouseSensitivity;
        transform.Rotate(0f, horizontalRotation, 0f);

        verticalRotation -= lookInput.y * mouseSensitivity;
        verticalRotation = Mathf.Clamp(verticalRotation, -90f, 90f);

        cameraTransform.localEulerAngles = new Vector3(verticalRotation, 0f, 0f);
    }

    void OnJump(InputAction.CallbackContext context)
    {
        if (isGrounded)
        {
            isGrounded = false;
            groundCheckTimer = groundCheckInterval;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
        }
    }

    void ApplyJump()
    {
        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * fallMultiplier * Time.fixedDeltaTime;
        }
        else if (rb.linearVelocity.y > 0)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * ascendMultiplier * Time.fixedDeltaTime;
        }
    }
}
