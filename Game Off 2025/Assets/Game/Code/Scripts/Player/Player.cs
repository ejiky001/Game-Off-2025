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

        // --- Client Sensitivity Adjustment ---
        [Tooltip("Multiplier applied to client rotation input to overcome perceived network lag.")]
        [SerializeField] private float clientRotationMultiplier = 1.5f;

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
            // The GroundCheck and Rotation must run for the owner regardless of server status
            if (IsOwner)
            {
                // --- Movement Input Handling (Client -> Server) ---
                Vector2 currentMoveInput = moveAction.ReadValue<Vector2>();

                if (currentMoveInput != moveInput)
                {
                    moveInput = currentMoveInput;
                    RequestMovementServerRpc(moveInput);
                }

                // --- Rotation Input Handling (Client -> Server + Local Prediction) ---
                RotateCamera();
            }

            // --- ⚠️ FIX 2: Ground check runs on ALL instances to ensure Server authority works ---
            GroundCheck();

            if (Health.Value <= 0)
                isAlive = false;

        }

        void FixedUpdate()
        {
            if (!isAlive) return;

            // --- 🚀 Client-Side Prediction / Server Authoritative Execution ---
            if (IsServer)
            {
                AuthoritativeMove();
            }
            else if (IsOwner) // Client Owner
            {
                PredictiveMove();
            }

            ApplyJump();
        }

        // --- Network RPCs for Movement, Rotation, and Jump ---

        [ServerRpc]
        private void RequestMovementServerRpc(Vector2 input)
        {
            moveInput = input;
        }

        [ServerRpc]
        private void RequestRotationServerRpc(float rotationAngle)
        {
            ApplyRotation(rotationAngle);
        }

        [ServerRpc]
        private void RequestJumpServerRpc()
        {
            // Server checks isGrounded which is updated by the server's GroundCheck()
            if (isGrounded && isAlive)
            {
                isGrounded = false;
                groundCheckTimer = groundCheckInterval;
                rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
            }
        }

        // --- ⚠️ NEW SERVER RPC FOR REVIVAL ---
        [ServerRpc]
        private void RequestReviveServerRpc(NetworkObjectReference targetPlayerRef)
        {
            if (targetPlayerRef.TryGet(out NetworkObject targetNetObj))
            {
                Player targetPlayer = targetNetObj.GetComponent<Player>();
                if (targetPlayer != null && !targetPlayer.isAlive)
                {
                    Debug.Log($"[SERVER] Reviving player {targetPlayer.OwnerClientId}");

                    // Server authoritatively updates the target player's state
                    targetPlayer.Health.Value = targetPlayer.maxHealth / 2;
                    targetPlayer.currentHealth = targetPlayer.Health.Value; // Local sync
                    targetPlayer.isAlive = true;
                }
            }
        }

        // --- CLIENT RPC FOR SERVER RECONCILIATION ---
        [ClientRpc]
        private void SendServerPositionClientRpc(Vector3 serverPosition)
        {
            if (IsOwner && !IsServer)
            {
                if (Vector3.Distance(transform.position, serverPosition) > 0.1f)
                {
                    transform.position = serverPosition;
                }
            }
        }

        // --- Core Movement Functions ---

        void AuthoritativeMove()
        {
            Vector3 movement = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;
            Vector3 displacement = movement * moveSpeed * Time.fixedDeltaTime;

            rb.MovePosition(rb.position + displacement);
            SendServerPositionClientRpc(rb.position);
        }

        void PredictiveMove()
        {
            Vector3 movement = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;
            Vector3 displacement = movement * moveSpeed * Time.fixedDeltaTime;

            rb.MovePosition(rb.position + displacement);
        }

        private void ApplyRotation(float rotationAngle)
        {
            if (!IsServer) return;

            transform.Rotate(0f, rotationAngle, 0f);
        }

        void RotateCamera()
        {
            if (!IsOwner) return;

            Vector2 lookInput = lookAction.ReadValue<Vector2>();

            // 1. HORIZONTAL (Player/Body Rotation)
            float horizontalRotation = lookInput.x * mouseSensitivity;

            if (Mathf.Abs(horizontalRotation) > 0.0001f)
            {
                float finalRotation = horizontalRotation;

                if (!IsServer)
                {
                    finalRotation *= clientRotationMultiplier;

                    // ⚠️ FIX 1: Client-Side Rotation Prediction
                    // Apply the rotation locally and instantly for a smooth feel
                    transform.Rotate(0f, finalRotation, 0f);
                }

                // Send the rotation to the server for authoritative update
                RequestRotationServerRpc(finalRotation);
            }

            // 2. VERTICAL (Camera Rotation) - Always local to the owner
            verticalRotation -= lookInput.y * mouseSensitivity;
            verticalRotation = Mathf.Clamp(verticalRotation, -90f, 90f);

            if (cameraTransform != null)
                cameraTransform.localEulerAngles = new Vector3(verticalRotation, 0f, 0f);
        }

        // ⚠️ FIX 2: GroundCheck runs on ALL instances to update local state (P1) and isGrounded (P2)
        void GroundCheck()
        {
            if (!isGrounded && groundCheckTimer <= 0f)
            {
                Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
                bool wasGrounded = isGrounded;
                isGrounded = Physics.Raycast(rayOrigin, Vector3.down, raycastDistance, groundLayer);

                // If it just landed, reset timer
                if (isGrounded && !wasGrounded)
                {
                    groundCheckTimer = groundCheckInterval;
                }
            }
            else
            {
                groundCheckTimer -= Time.deltaTime;
            }
        }

        void Jump(InputAction.CallbackContext context)
        {
            if (isGrounded && isAlive)
            {
                // Client predicts jump locally
                isGrounded = false;
                groundCheckTimer = groundCheckInterval;
                rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);

                // Then requests authoritative jump from server
                RequestJumpServerRpc();
            }
        }

        void ApplyJump()
        {
            if (rb.linearVelocity.y < 0)
                rb.linearVelocity += Vector3.up * Physics.gravity.y * fallMultiplier * Time.fixedDeltaTime;
            else if (rb.linearVelocity.y > 0)
                rb.linearVelocity += Vector3.up * Physics.gravity.y * ascendMultiplier * Time.fixedDeltaTime;
        }

        #region Collision and Interaction
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
            if (!isAlive)
            {
                Debug.Log("You are dead and cannot interact");
                return;
            }

            if (onHydrant && currentAmmo < maxAmmo)
            {
                Debug.Log("At hydrant and reloading");
                currentAmmo = maxAmmo;
            }

            if (onPlayer)
            {
                Collider[] nearbyPlayers = Physics.OverlapSphere(transform.position, 2f, playerLayer);

                foreach (Collider col in nearbyPlayers)
                {
                    if (col.gameObject == this.gameObject) continue;

                    Player otherPlayer = col.GetComponent<Player>();
                    if (otherPlayer != null && !otherPlayer.isAlive)
                    {
                        // ⚠️ FIX 3: Request revival on the Server
                        Debug.Log($"Requesting revive for player {otherPlayer.OwnerClientId}");
                        RequestReviveServerRpc(otherPlayer.GetComponent<NetworkObject>());
                        break;
                    }
                }
            }
        }
        #endregion

        #region Damage and Health
        // ... (Damage and Health methods remain the same)
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
            while (true)
            {
                if (IsServer && isAlive && Health.Value < maxHealth)
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
        #endregion

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

                SpawnProjectileServerRpc(spawnPos, direction);

                yield return new WaitForSeconds(fireRate);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void SpawnProjectileServerRpc(Vector3 spawnPos, Vector3 direction)
        {
            GameObject projectile = Instantiate(waterProjectilePrefab, spawnPos, Quaternion.LookRotation(direction));
            NetworkObject netObj = projectile.GetComponent<NetworkObject>();

            netObj.SpawnWithOwnership(NetworkManager.ServerClientId);

            Rigidbody rbProj = projectile.GetComponent<Rigidbody>();
            rbProj.linearVelocity = Vector3.zero;
            rbProj.AddForce(direction.normalized * shootForce, ForceMode.Impulse);
        }
        #endregion
    }
}