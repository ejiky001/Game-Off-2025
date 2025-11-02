using UnityEngine;
using UnityEngine.InputSystem; 
using System.Collections;

public class Player : MonoBehaviour
{
    // Camera controls
    public float mouseSensitivity = 0.25f;
    private float verticalRotation = 0f;
    private Transform cameraTransform;

    //Player health
    [SerializeField]
    private int maxHealth = 10;
    [SerializeField]
    private int currentHealth;
    [SerializeField]
    public bool isAlive = true;
    [SerializeField]
    private float outOfCombatRegenDelay = 5f;
    [SerializeField]
    private float timeSinceLastDamage = 0f;
    [SerializeField]
    private float healthRegenRate = 5f; // seconds per health point


    //player ammof
    [SerializeField]
    private int maxAmmo = 100;
    [SerializeField]
    private int currentAmmo;

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
    public LayerMask playerLayer;
    public LayerMask HydrantLayer;

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
    private InputAction attackAction;

    //interactable
    private bool onHydrant = false;
    private bool onPlayer = false;


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
        attackAction = playerInput.actions["Attack"];
        

        //hidden mouse if needed -- comment before build if needed
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        //set health
        currentHealth = maxHealth;

        //set ammo
        currentAmmo = maxAmmo;

        StartCoroutine(Regen());


    }

    void OnEnable()
    {
        jumpAction.performed += Jump;
        interactAction.performed += Interact;

    }

    void OnDisable()
    {
        jumpAction.performed -= Jump;
        interactAction.performed -= Interact;

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
        if(currentHealth <= 0)
        {
            isAlive = false;
        }

    }

    void FixedUpdate()
    {
        if (isAlive)
        {
            Move();
            ApplyJump();
        }
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

    void Jump(InputAction.CallbackContext context)
    {
        if (isGrounded && isAlive)
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
    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & HydrantLayer) != 0)
        {
            onHydrant = true;
        }
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            onPlayer = true;
        }

    }
    private void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & HydrantLayer) != 0)
        {
            onHydrant = false;
        }
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            onPlayer = false;
        }
    }


    
    void Interact(InputAction.CallbackContext context)
    {
        if (onHydrant && currentAmmo < maxAmmo && isAlive)
        {
            Debug.Log("At hydrant and reloading");
            //reload ammo
            currentAmmo = maxAmmo;
        }


        if (onPlayer && isAlive)
        {
            //find players nearby
            Collider[] nearbyPlayers = Physics.OverlapSphere(transform.position, 2f, playerLayer);

            foreach (Collider col in nearbyPlayers)
            {
                //ignore your own collider
                if (col.gameObject == this.gameObject)
                {
                    continue;
                }

                Player otherPlayer = col.GetComponent<Player>();
                if (otherPlayer != null && !otherPlayer.isAlive)
                {
                    Debug.Log("Reviving player");
                    //revive player with half hp
                    otherPlayer.currentHealth = otherPlayer.maxHealth / 2;
                    otherPlayer.isAlive = true;
                    break; //only revive one player
                }

            }
        }
        else if(!isAlive)
        {
            Debug.Log("You are dead and cannot interact");
        }

    }
    public void TakeDamage(int amount)
    {
        if (!isAlive) return;
        currentHealth -= amount;
        timeSinceLastDamage = 0f; //reset regen timer
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            isAlive = false;
            Debug.Log("Player has died.");
        }
    }
    private IEnumerator Regen()
    {
        while (true)
        {
            //regen if alive and missing hp
            if (isAlive && currentHealth < maxHealth)
            {
                //time since last dmg
                timeSinceLastDamage += Time.deltaTime;

                //wait until out of combat delay
                if (timeSinceLastDamage >= outOfCombatRegenDelay)
                {
                    currentHealth += 1;
                    currentHealth = Mathf.Min(currentHealth, maxHealth);
                    yield return new WaitForSeconds(healthRegenRate);
                }
               
            }
            yield return null;
        }

    }
    void Shoot()
    {
    }
}
