using System.Collections;

using Unity.Netcode;

using UnityEngine;



namespace Unity.Multiplayer.Center.NetcodeForGameObjects

{

    

    public class ShootingEnemy : NetworkBehaviour

    {

        [Header("General Settings")]

        [SerializeField] private float health = 5f;



        [Header("AI & Targeting")]

        [SerializeField] private LayerMask playerLayer;

        [SerializeField] private Collider detectionTrigger;



        // Firing rate is 3 seconds

        [SerializeField] private float fireRate = 3f;



        [Header("Projectile Settings (NEW)")]

        [Tooltip("Prefab of the EnemyProjectile script with a NetworkObject and Rigidbody.")]

        [SerializeField] private GameObject projectilePrefab;

        [Tooltip("Empty GameObject indicating where the projectile spawns.")]

        [SerializeField] private Transform shootingPosition;

        [SerializeField] private float projectileSpeed = 15f;



        // Movement components are kept for knockback/positioning flexibility

        [SerializeField] private float moveSpeed = 2f;

        [SerializeField] private Rigidbody enemyRigidBody;



        private Player targetPlayer;

        private Coroutine attackCoroutine;



        public override void OnNetworkSpawn()

        {

           

        }



        private void Awake()

        {

            if (detectionTrigger != null)

            {

                var triggerRelay = detectionTrigger.gameObject.AddComponent<TriggerRelay>();

                triggerRelay.Init(this);

            }

        }



        // --- Targeting Logic (Handles when to start/stop the AttackLoop) ---



        public void HandleTriggerEnter(Collider other)

        {

            if (!IsServer) return;

            if (((1 << other.gameObject.layer) & playerLayer) == 0) return;



            var netObj = other.GetComponentInParent<NetworkObject>();

            if (netObj != null && netObj.TryGetComponent(out Player player))

            {

                bool acquireNewTarget = targetPlayer == null || !targetPlayer.IsAlive.Value;



                if (acquireNewTarget)

                {

                    targetPlayer = player;

                    // Start the attack loop if a valid target is found

                    if (attackCoroutine == null)

                        attackCoroutine = StartCoroutine(AttackLoop());

                }

            }

        }



        public void HandleTriggerExit(Collider other)

        {

            if (!IsServer) return;



            var netObj = other.GetComponentInParent<NetworkObject>();

            if (netObj != null && netObj.TryGetComponent(out Player player))

            {

                if (player == targetPlayer)

                {

                    StopAttacking();

                }

            }

        }



        // --- Firing Loop (3-second interval) ---



        private IEnumerator AttackLoop()

        {

            yield return new WaitForSeconds(1f); // Initial delay before the first shot



            while (targetPlayer != null)

            {

                if (targetPlayer.IsAlive.Value)

                {

                    FireProjectile();

                }

                else

                {

                    // Target died, stop shooting

                    StopAttacking();

                    yield break;

                }



                // Wait 3 seconds before the next shot

                yield return new WaitForSeconds(fireRate);

            }



            StopAttacking();

        }



        private void FireProjectile()

        {

            if (!IsServer || projectilePrefab == null || shootingPosition == null) return;



            // 1. Calculate direction towards the target

            Vector3 directionToTarget = (targetPlayer.transform.position - shootingPosition.position).normalized;

            Quaternion rotation = Quaternion.LookRotation(directionToTarget);



            // 2. Instantiate and Network Spawn

            GameObject projectileObject = Instantiate(projectilePrefab, shootingPosition.position, rotation);



            if (projectileObject.TryGetComponent(out NetworkObject netObject))

            {

                netObject.Spawn();

            }



            // 3. Apply non-gravity velocity

            if (projectileObject.TryGetComponent(out Rigidbody rb))

            {

                rb.linearVelocity = directionToTarget * projectileSpeed;

            }

        }



        private void StopAttacking()

        {

            if (attackCoroutine != null)

            {

                StopCoroutine(attackCoroutine);

                attackCoroutine = null;

            }

            targetPlayer = null;

        }



        // --- Damage and Knockback Logic (Kept from original) ---



        public void TakeDamage(float amount)

        {

            if (!IsServer) return;



            health -= amount;

            if (health <= 0)

                Die();

        }



        private void Die()

        {

            if (!IsServer) return;

            NetworkObject.Despawn(destroy: true);

        }



        public void Knockback(Vector3 force, float duration = 0.05f)

        {

            StopAllCoroutines();

            StartCoroutine(KnockbackRoutine(force, duration));

        }



        private IEnumerator KnockbackRoutine(Vector3 force, float duration)

        {

            if (enemyRigidBody != null)
            {
                // Clear existing movement before applying force
                enemyRigidBody.linearVelocity = Vector3.zero;
                enemyRigidBody.angularVelocity = Vector3.zero;

                enemyRigidBody.AddForce(force, ForceMode.Impulse);
            }

            yield return new WaitForSeconds(duration);

            if (enemyRigidBody != null)
            {
                // Stop the momentum after the knockback duration
                enemyRigidBody.linearVelocity = Vector3.zero;
                enemyRigidBody.angularVelocity = Vector3.zero;
            }
        }

    }

}