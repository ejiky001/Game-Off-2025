using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Unity.Multiplayer.Center.NetcodeForGameObjects
{
    public class RollingEnemy : NetworkBehaviour
    {
        [SerializeField] private float health = 5f;
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private int attackDamage = 1;

        [SerializeField] private float attackDelay = 5f; // seconds between attacks

        public LayerMask playerLayer;

        private Player targetPlayer;
        private Coroutine attackCoroutine;

        [SerializeField] private Collider detectionTrigger;
        [SerializeField] private Rigidbody enemyRigidBody;

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

        public void HandleTriggerEnter(Collider other)
        {
            Debug.Log($"[ENEMY] Trigger hit: {other.gameObject.name}, layer={other.gameObject.layer}");

            if (!IsServer) return;
            if (((1 << other.gameObject.layer) & playerLayer) == 0) return;

            var netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj != null && netObj.TryGetComponent(out Player player))
            {
                bool acquireNewTarget = targetPlayer == null || !targetPlayer.IsAlive.Value;

                if (acquireNewTarget)
                {
                    targetPlayer = player;

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


        private IEnumerator AttackLoop()
        {
            yield return new WaitForSeconds(1f); // Initial delay before the first attack

            while (targetPlayer != null)
            {
                // Check if the current target is still valid (alive and in range)
                if (targetPlayer.IsAlive.Value)
                {
                    Attack(targetPlayer);
                }
                else
                {
                    // Target died, stop the loop and nullify target
                    StopAttacking();
                    yield break;
                }

                yield return new WaitForSeconds(attackDelay);

                // Re-evaluate the target's presence/state here if needed, 
                // but relying on HandleTriggerExit for range and the check above for state is simpler.
            }

            StopAttacking();
        }


        private void Attack(Player player)
        {
            if (player != null && player.IsAlive.Value)
            {
                player.TakeDamageServerRpc(attackDamage);
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
            Destroy(gameObject);
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