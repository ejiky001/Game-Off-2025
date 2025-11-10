using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using Unity.Netcode;

namespace Unity.Multiplayer.Center.NetcodeForGameObjects
{
    public class Player : NetworkBehaviour
    {
        // --- Networked Data ---
        public NetworkVariable<int> Health = new NetworkVariable<int>(10, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<Quaternion> NetworkRotation = new NetworkVariable<Quaternion>(
            Quaternion.identity,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // **FIX 1: CHANGED isAlive to NetworkVariable<bool> IsAlive**
        public NetworkVariable<bool> IsAlive = new NetworkVariable<bool>(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // --- Camera and Look (References for PlayerLook.cs) ---
        public Camera playerCamera;
        public AudioListener audioListener;
        private PlayerLook playerLook;
        public float mouseSensitivity = 0.25f; // Used by PlayerLook

        // --- Rotation Variables ---
        private float clientHorizontalRotationDelta;
        private float serverHorizontalRotationDelta;

        // --- Player health/ammo ---
        [SerializeField] private int maxHealth = 10;
        [SerializeField] private int currentHealth;
        [SerializeField] private float outOfCombatRegenDelay = 5f;
        [SerializeField] private float timeSinceLastDamage = 0f;
        [SerializeField] private float healthRegenRate = 5f;
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
        private Vector2 moveInput;

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

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation.None; // Predictive input

            playerLook = GetComponent<PlayerLook>();

            playerHeight = GetComponent<CapsuleCollider>().height * transform.localScale.y;

            playerInput = GetComponent<PlayerInput>();
            playerInput.enabled = false;

            currentHealth = maxHealth;
            currentAmmo = maxAmmo;

            StartCoroutine(Regen());
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Register state change listener for all clients
            IsAlive.OnValueChanged += OnIsAliveValueChanged;
            NetworkRotation.OnValueChanged += OnNetworkRotationChanged;

            // Apply initial state based on current value
            OnIsAliveValueChanged(false, IsAlive.Value);

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

                // Pass the input action to the separate look script
                if (playerLook != null)
                {
                    playerLook.Initialize(lookAction);
                }

                // Initial registration of input actions
                EnableInputActions();

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else // Remote Client
            {
                if (playerCamera != null) playerCamera.enabled = false;
                if (audioListener != null) audioListener.enabled = false;

                // Add interpolation for remote players
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            // Deregister listeners
            if (IsAlive != null) IsAlive.OnValueChanged -= OnIsAliveValueChanged;
            if (NetworkRotation != null) NetworkRotation.OnValueChanged -= OnNetworkRotationChanged;

            // Clean up input actions if this was the owner
            if (IsOwner)
            {
                DisableInputActions();
            }
        }

        void Update()
        {
            // Only update input if player is alive
            if (IsOwner && IsAlive.Value)
            {
                // Movement Input Handling (Client -> Server)
                Vector2 currentMoveInput = moveAction.ReadValue<Vector2>();

                if (currentMoveInput != moveInput)
                {
                    moveInput = currentMoveInput;
                    RequestMovementServerRpc(moveInput);
                }
            }

            GroundCheck();

            // Authoritative death check (Server-only)
            if (IsServer && Health.Value <= 0 && IsAlive.Value)
            {
                // Set IsAlive to false when health hits zero
                IsAlive.Value = false;
            }
        }

        void FixedUpdate()
        {
            // Only allow movement physics if player is alive
            if (!IsAlive.Value) return;

            if (IsServer)
            {
                AuthoritativeMoveAndRotate();
            }
            else if (IsOwner) // Client Owner
            {
                PredictiveMoveAndRotate();
            }

            ApplyJump();
        }

        void LateUpdate()
        {
            // Remote client interpolation for smooth look
            if (!IsOwner && !IsServer)
            {
                transform.rotation = Quaternion.Lerp(transform.rotation, NetworkRotation.Value, Time.deltaTime * 10f);
            }
        }

        // --- NEW: SERVER-SIDE TELEPORTATION FOR SPAWNING ---
        /// <summary>
        /// Called by a separate Spawning Manager in the Game Scene (Server-side only)
        /// to place the player at their designated spawn point immediately after scene load.
        /// </summary>
        public void ServerTeleport(Vector3 newPosition)
        {
            if (!IsServer) return;

            Debug.Log($"[SERVER] Teleporting player {OwnerClientId} to {newPosition}");

            // 1. Set the position authoritatively
            transform.position = newPosition;

            // 2. Reset physics movement (important after scene change/teleport)
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // --- Public function for PlayerLook.cs to call ---
        public void AccumulateRotationDelta(float delta)
        {
            clientHorizontalRotationDelta += delta;
        }

        // --- Network RPCs for Movement and Rotation ---

        [ServerRpc]
        private void RequestMovementServerRpc(Vector2 input)
        {
            moveInput = input;
        }

        [ServerRpc]
        public void RequestRotationDeltaServerRpc(float rotationDelta)
        {
            serverHorizontalRotationDelta += rotationDelta;
        }

        [ServerRpc]
        private void RequestJumpServerRpc()
        {
            if (isGrounded && IsAlive.Value)
            {
                isGrounded = false;
                groundCheckTimer = groundCheckInterval;
                rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
            }
        }

        // --- State Change Handlers ---

        /// <summary>
        /// Runs on all clients when the IsAlive state changes (Death/Revive).
        /// </summary>
        private void OnIsAliveValueChanged(bool oldState, bool newState)
        {
            Debug.Log($"Player {OwnerClientId} IsAlive changed from {oldState} to {newState}");

            // Disable/Enable the Rigidbody and Collider for physical death state
            rb.isKinematic = !newState; // Use kinematic if dead to stop physics movement

            if (IsOwner)
            {
                if (newState) // Player is now alive
                {
                    EnableInputActions();
                    // Clear the input field for movement to prevent ghost movement
                    moveInput = Vector2.zero;
                    RequestMovementServerRpc(moveInput); // Notify server of zero input
                }
                else // Player has died
                {
                    DisableInputActions();
                }
            }

            // Optionally, handle visuals (e.g., enable ragdoll, change shader, hide gun model) here
        }

        // --- MODIFIED REVIVE RPC ---
        [ServerRpc]
        private void RequestReviveServerRpc(NetworkObjectReference targetPlayerRef)
        {
            if (targetPlayerRef.TryGet(out NetworkObject targetNetObj))
            {
                Player targetPlayer = targetNetObj.GetComponent<Player>();
                if (targetPlayer != null && !targetPlayer.IsAlive.Value)
                {
                    Debug.Log($"[SERVER] Reviving player {targetPlayer.OwnerClientId}");

                    // Restore health state
                    targetPlayer.Health.Value = targetPlayer.maxHealth / 2;
                    targetPlayer.currentHealth = targetPlayer.Health.Value;

                    // Set IsAlive to true, triggering OnIsAliveValueChanged on all clients
                    targetPlayer.IsAlive.Value = true;
                }
            }
        }

        [ClientRpc]
        private void SendServerStateClientRpc(Vector3 serverPosition, Quaternion serverRotation)
        {
            if (IsOwner && !IsServer)
            {
                // Positional correction
                if (Vector3.Distance(transform.position, serverPosition) > 0.1f)
                {
                    transform.position = serverPosition;
                }

                // Rotational correction
                if (Quaternion.Angle(transform.rotation, serverRotation) > 1.0f)
                {
                    transform.rotation = serverRotation;
                }
            }
        }

        // --- Core Movement Functions ---

        void AuthoritativeMoveAndRotate()
        {
            // Apply authoritative rotation
            Quaternion deltaRotation = Quaternion.Euler(0f, serverHorizontalRotationDelta, 0f);
            rb.MoveRotation(rb.rotation * deltaRotation);
            serverHorizontalRotationDelta = 0f;

            // Sync rotation state for remote clients
            NetworkRotation.Value = rb.rotation;

            // Apply authoritative movement
            Vector3 movement = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;
            Vector3 displacement = movement * moveSpeed * Time.fixedDeltaTime;

            rb.MovePosition(rb.position + displacement);

            SendServerStateClientRpc(rb.position, rb.rotation);
        }

        void PredictiveMoveAndRotate()
        {
            // Apply predicted rotation
            Quaternion deltaRotation = Quaternion.Euler(0f, clientHorizontalRotationDelta, 0f);
            rb.MoveRotation(rb.rotation * deltaRotation);
            clientHorizontalRotationDelta = 0f;

            // Apply predicted movement
            Vector3 movement = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;
            Vector3 displacement = movement * moveSpeed * Time.fixedDeltaTime;

            rb.MovePosition(rb.position + displacement);
        }

        void GroundCheck()
        {
            if (!isGrounded && groundCheckTimer <= 0f)
            {
                Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
                bool wasGrounded = isGrounded;
                isGrounded = Physics.Raycast(rayOrigin, Vector3.down, raycastDistance, groundLayer);

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
            if (!IsAlive.Value) return; // Only jump if alive

            if (isGrounded)
            {
                isGrounded = false;
                groundCheckTimer = groundCheckInterval;
                rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);

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

        private void OnNetworkRotationChanged(Quaternion oldRotation, Quaternion newRotation)
        {
            // Handler for remote clients to read the new rotation state
        }

        // --- INPUT Helper Methods ---
        private void EnableInputActions()
        {
            // Re-register inputs using -= then += to ensure they are active without duplication
            if (jumpAction != null) { jumpAction.performed -= Jump; jumpAction.performed += Jump; }
            if (interactAction != null) { interactAction.performed -= Interact; interactAction.performed += Interact; }
            if (attackAction != null)
            {
                attackAction.performed -= AttackStart; attackAction.performed += AttackStart;
                attackAction.canceled -= AttackStop; attackAction.canceled += AttackStop;
            }
        }

        private void DisableInputActions()
        {
            if (jumpAction != null) jumpAction.performed -= Jump;
            if (interactAction != null) interactAction.performed -= Interact;
            if (attackAction != null)
            {
                attackAction.performed -= AttackStart;
                attackAction.canceled -= AttackStop;
                // Stop shooting immediately
                if (shootingCoroutine != null)
                {
                    StopCoroutine(shootingCoroutine);
                    shootingCoroutine = null;
                }
            }
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
            if (!IsAlive.Value)
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
                    if (otherPlayer != null && !otherPlayer.IsAlive.Value)
                    {
                        Debug.Log($"Requesting revive for player {otherPlayer.OwnerClientId}");
                        RequestReviveServerRpc(otherPlayer.GetComponent<NetworkObject>());
                        break;
                    }
                }
            }
        }
        #endregion

        #region Damage and Health
        [ServerRpc(RequireOwnership = false)]
        public void TakeDamageServerRpc(int amount)
        {
            if (!IsAlive.Value) return;

            Debug.Log($"[SERVER] {OwnerClientId} took {amount} damage");

            Health.Value -= amount;
            currentHealth = Health.Value;
            timeSinceLastDamage = 0f;

            if (Health.Value <= 0)
            {
                Health.Value = 0;
                currentHealth = 0;
                IsAlive.Value = false;
                Debug.Log($"{OwnerClientId} has died.");
            }
        }

        public void ApplyDamage(int amount)
        {
            if (!IsServer) return;
            if (!IsAlive.Value) return;

            Debug.Log($"[SERVER] Applying {amount} damage to {OwnerClientId}");

            Health.Value -= amount;
            currentHealth = Health.Value;
            timeSinceLastDamage = 0f;

            if (Health.Value <= 0)
            {
                Health.Value = 0;
                currentHealth = 0;
                IsAlive.Value = false;
                Debug.Log($"{OwnerClientId} has died.");
            }
        }

        private IEnumerator Regen()
        {
            while (true)
            {
                if (IsServer && IsAlive.Value && Health.Value < maxHealth)
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
            if (!IsOwner || !IsAlive.Value || currentAmmo <= 0) return;

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
                    shootingCoroutine = null;
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