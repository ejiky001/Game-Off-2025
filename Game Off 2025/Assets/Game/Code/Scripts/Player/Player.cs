using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using Unity.Netcode;

namespace Unity.Multiplayer.Center.NetcodeForGameObjects
{
    public class Player : NetworkBehaviour
    {
        public NetworkVariable<int> Health = new NetworkVariable<int>(10, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // --- Camera controls ---
        public float mouseSensitivity = 0.25f;
        private float verticalRotation = 0f;
        private Transform cameraTransform;

        // --- Player health ---
        [SerializeField] private int maxHealth = 10;
        [SerializeField] private int currentHealth;
        [SerializeField] public bool isAlive = true;
        [SerializeField] private float outOfCombatRegenDelay = 5f;
        [SerializeField] private float timeSinceLastDamage = 0f;
        [SerializeField] private float healthRegenRate = 5f;

        // --- Player ammo ---
        [SerializeField] private float maxAmmo = 100;
        [SerializeField] private float currentAmmo;

        // --- Shooting ---
        [SerializeField] private GameObject waterProjectilePrefab;
        [SerializeField] private Transform shootPoint;
        [SerializeField] private float shootForce = 5f;
        [SerializeField] private float fireRate = 0.1f;
        private Coroutine shootingCoroutine;

        // --- Movement ---
        private Rigidbody rb;
        public float moveSpeed = 10f;
        private Vector2 moveInput; // Stores the last received input (local or via RPC)
        private float horizontalRotationInput; // Stores the last received rotation input (via RPC)

        // --- Jumping ---
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

        // --- Input Actions ---
        private PlayerInput playerInput;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction jumpAction;
        private InputAction interactAction;
        private InputAction attackAction;

        // --- Interactable ---
        private bool onHydrant = false;
        private bool onPlayer = false;

        // --- Components ---
        public Camera playerCamera;
        public AudioListener audioListener;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.freezeRotation = true;

            if (playerCamera != null)
                cameraTransform = playerCamera.transform;

            playerHeight = GetComponent<CapsuleCollider>().height * transform.localScale.y;

            playerInput = GetComponent<PlayerInput>();
            playerInput.enabled = false;

            currentHealth = maxHealth;
            currentAmmo = maxAmmo;

            // Health regen should be server-side
            if (IsServer)
                StartCoroutine(Regen());

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsOwner)
            {
                playerInput.enabled = true;

                if (playerCamera != null) playerCamera.enabled = true;
                if (audioListener != null) audioListener.enabled = true;

                moveAction = playerInput.actions["Move"];
                lookAction = playerInput.actions["Look"];
                jumpAction = playerInput.actions["Jump"];
                interactAction = playerInput.actions["Interact"];
                attackAction = playerInput.actions["Attack"];

                jumpAction.performed += Jump;
                interactAction.performed += Interact;
                attackAction.performed += AttackStart;
                attackAction.canceled += AttackStop;
            }
            else
            {
                if (playerCamera != null) playerCamera.enabled = false;
                if (audioListener != null) audioListener.enabled = false;
            }

        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (jumpAction != null) jumpAction.performed -= Jump;
            if (interactAction != null) interactAction.performed -= Interact;
            if (attackAction != null)
            {
                attackAction.performed -= AttackStart;
                attackAction.canceled -= AttackStop;
            }
        }

        void Update()
        {
            if (!IsOwner) return;

            // --- Movement Input Handling (Client -> Server) ---
            Vector2 currentMoveInput = moveAction.ReadValue<Vector2>();

            // Only send RPC if input has changed
            if (currentMoveInput != moveInput)
            {
                moveInput = currentMoveInput;
                RequestMovementServerRpc(moveInput);
            }

            // --- Rotation Input Handling (Client -> Server) ---
            RotateCamera();

            // Ground check (client-side for responsiveness, though server is authoritative for physics)
            if (!isGrounded && groundCheckTimer <= 0f)
            {
                Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
                isGrounded = Physics.Raycast(rayOrigin, Vector3.down, raycastDistance, groundLayer);
            }
            else
            {
                groundCheckTimer -= Time.deltaTime;
            }

            if (Health.Value <= 0)
                isAlive = false;

        }

        void FixedUpdate()
        {
            // The Server must execute movement for all players
            if (!IsServer || !isAlive) return;

            Move();
            ApplyJump();

            // P1 (Host) is the owner and server, so it uses its input. 
            // P2 (Client) has its moveInput updated via RPC.
        }

        // --- Network RPCs for Movement and Rotation ---

        [ServerRpc]
        private void RequestMovementServerRpc(Vector2 input)
        {
            // Server updates its internal input variable
            moveInput = input;
        }

        [ServerRpc]
        private void RequestRotationServerRpc(float horizontalInput)
        {
            // Server applies the rotation authoritatively
            ApplyRotation(horizontalInput);
        }

        [ServerRpc]
        private void RequestJumpServerRpc()
        {
            // Server performs the authoritative jump
            if (isGrounded && isAlive)
            {
                isGrounded = false;
                groundCheckTimer = groundCheckInterval;
                rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
            }
        }

        // --- Core Movement Functions (Server-Only Execution) ---

        void Move()
        {
            // This runs on the Server using the last received moveInput
            Vector3 movement = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;
            Vector3 displacement = movement * moveSpeed * Time.fixedDeltaTime;

            // rb.MovePosition is an authoritative server operation
            rb.MovePosition(rb.position + displacement);
        }

        private void ApplyRotation(float horizontalRotation)
        {
            // Only the Server should authoritatively change the Networked Transform's rotation
            if (!IsServer) return;

            // Apply the rotation requested by the owner
            transform.Rotate(0f, horizontalRotation, 0f);
        }

        void RotateCamera()
        {
            if (!IsOwner) return;

            Vector2 lookInput = lookAction.ReadValue<Vector2>();

            // 1. HORIZONTAL (Player/Body Rotation)
            float horizontalRotation = lookInput.x * mouseSensitivity;

            // Client sends the rotation amount to the server for authoritative application
            if (Mathf.Abs(horizontalRotation) > 0.0001f)
            {
                RequestRotationServerRpc(horizontalRotation);
            }

            // 2. VERTICAL (Camera Rotation) - Stays local to the owner
            verticalRotation -= lookInput.y * mouseSensitivity;
            verticalRotation = Mathf.Clamp(verticalRotation, -90f, 90f);

            if (cameraTransform != null)
                cameraTransform.localEulerAngles = new Vector3(verticalRotation, 0f, 0f);
        }

        void Jump(InputAction.CallbackContext context)
        {
            // Only the owner handles input, then requests the jump on the server
            if (isGrounded && isAlive)
            {
                RequestJumpServerRpc();
            }
        }

        void ApplyJump()
        {
            // Only runs on the Server (in FixedUpdate)
            if (rb.linearVelocity.y < 0)
                rb.linearVelocity += Vector3.up * Physics.gravity.y * fallMultiplier * Time.fixedDeltaTime;
            else if (rb.linearVelocity.y > 0)
                rb.linearVelocity += Vector3.up * Physics.gravity.y * ascendMultiplier * Time.fixedDeltaTime;
        }

        // --- Collision and Interaction ---

        private void OnTriggerEnter(Collider other)
        {
            if (((1 << other.gameObject.layer) & HydrantLayer) != 0)
                onHydrant = true;
            if (((1 << other.gameObject.layer) & playerLayer) != 0)
                onPlayer = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (((1 << other.gameObject.layer) & HydrantLayer) != 0)
                onHydrant = false;
            if (((1 << other.gameObject.layer) & playerLayer) != 0)
                onPlayer = false;
        }

        void Interact(InputAction.CallbackContext context)
        {
            if (onHydrant && currentAmmo < maxAmmo && isAlive)
            {
                Debug.Log("At hydrant and reloading");
                currentAmmo = maxAmmo;
            }

            if (onPlayer && isAlive)
            {
                Collider[] nearbyPlayers = Physics.OverlapSphere(transform.position, 2f, playerLayer);

                foreach (Collider col in nearbyPlayers)
                {
                    if (col.gameObject == this.gameObject) continue;

                    Player otherPlayer = col.GetComponent<Player>();
                    if (otherPlayer != null && !otherPlayer.isAlive)
                    {
                        Debug.Log("Reviving player");
                        otherPlayer.currentHealth = otherPlayer.maxHealth / 2;
                        otherPlayer.isAlive = true;
                        break;
                    }
                }
            }
            else if (!isAlive)
            {
                Debug.Log("You are dead and cannot interact");
            }
        }

        // --- Damage and Health ---

        [ServerRpc(RequireOwnership = false)]
        public void TakeDamageServerRpc(int amount)
        {
            if (!isAlive) return;

            Debug.Log($"[SERVER] {OwnerClientId} took {amount} damage");

            Health.Value -= amount;        // decrement NETWORK health
            currentHealth = Health.Value; // sync local inspector value
            timeSinceLastDamage = 0f;

            if (Health.Value <= 0)
            {
                Health.Value = 0;
                currentHealth = 0;
                isAlive = false;
                Debug.Log($"{OwnerClientId} has died.");
            }
        }

        public void ApplyDamage(int amount)
        {
            // Only run on server
            if (!IsServer) return;

            if (!isAlive) return;

            Debug.Log($"[SERVER] Applying {amount} damage to {OwnerClientId}");

            Health.Value -= amount;
            currentHealth = Health.Value;
            timeSinceLastDamage = 0f;

            if (Health.Value <= 0)
            {
                Health.Value = 0;
                currentHealth = 0;
                isAlive = false;
                Debug.Log($"{OwnerClientId} has died.");
            }
        }

        private IEnumerator Regen()
        {
            // MUST be Server-side to write to the NetworkVariable
            if (!IsServer) yield break;

            while (true)
            {
                if (isAlive && Health.Value < maxHealth)
                {
                    timeSinceLastDamage += Time.deltaTime;

                    if (timeSinceLastDamage >= outOfCombatRegenDelay)
                    {
                        Health.Value++;
                        currentHealth = Health.Value;
                        yield return new WaitForSeconds(healthRegenRate);
                    }
                }

                yield return null;
            }
        }


        #region Shooting System (Authoritative Server Version)

        private void AttackStart(InputAction.CallbackContext ctx)
        {
            if (!IsOwner || !isAlive || currentAmmo <= 0) return;

            if (shootingCoroutine == null)
                shootingCoroutine = StartCoroutine(ShootWater());
        }

        private void AttackStop(InputAction.CallbackContext ctx)
        {
            if (!IsOwner) return;

            if (shootingCoroutine != null)
            {
                StopCoroutine(shootingCoroutine);
                shootingCoroutine = null;
            }
        }

        private IEnumerator ShootWater()
        {
            while (true)
            {
                if (currentAmmo <= 0)
                {
                    currentAmmo = 0;
                    yield break;
                }

                currentAmmo -= 1;

                Vector3 direction = shootPoint.forward;
                direction += new Vector3(
                    Random.Range(-0.05f, 0.05f),
                    Random.Range(-0.05f, 0.05f),
                    Random.Range(-0.02f, 0.02f)
                );

                Vector3 spawnPos = shootPoint.position + shootPoint.forward * 0.3f;

                // Call the server to spawn the projectile
                SpawnProjectileServerRpc(spawnPos, direction);

                yield return new WaitForSeconds(fireRate);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void SpawnProjectileServerRpc(Vector3 spawnPos, Vector3 direction)
        {
            GameObject projectile = Instantiate(waterProjectilePrefab, spawnPos, Quaternion.LookRotation(direction));
            NetworkObject netObj = projectile.GetComponent<NetworkObject>();

            // Force SERVER to own it 
            netObj.SpawnWithOwnership(NetworkManager.ServerClientId);

            Rigidbody rbProj = projectile.GetComponent<Rigidbody>();
            rbProj.linearVelocity = Vector3.zero;
            rbProj.AddForce(direction.normalized * shootForce, ForceMode.Impulse);
        }

        #endregion

    }
}