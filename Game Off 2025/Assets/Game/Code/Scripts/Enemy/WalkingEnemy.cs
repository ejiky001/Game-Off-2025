using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Unity.Multiplayer.Center.NetcodeForGameObjects
{
    public class WalkingEnemy : NetworkBehaviour
    {
        [SerializeField] private float health = 5f;
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private int attackDamage = 1;

        [SerializeField] private float attackDelay = 3f; // seconds between attacks

        public LayerMask playerLayer;

        private Player targetPlayer;
        private Coroutine attackCoroutine;

        [SerializeField] private Collider detectionTrigger;  
        [SerializeField] private Rigidbody enemyRigidBody;

        public override void OnNetworkSpawn()
        {
            if (enemyRigidBody != null)
            {
                enemyRigidBody.isKinematic = true;
                enemyRigidBody.useGravity = false;
            }
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
                targetPlayer = player;
            }
           

            if (attackCoroutine == null)
                attackCoroutine = StartCoroutine(AttackLoop());
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
            yield return new WaitForSeconds(1f);

            while (targetPlayer != null)
            {
                Attack(targetPlayer);
                yield return new WaitForSeconds(attackDelay);
            }

            StopAttacking();
        }


        private void Attack(Player player)
        {
            if (player != null && player.isAlive)
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
            enemyRigidBody.isKinematic = false;   // allow physics temporarily
            enemyRigidBody.AddForce(force, ForceMode.Impulse);

            yield return new WaitForSeconds(duration);

            enemyRigidBody.linearVelocity = Vector3.zero;   // stop sliding
            enemyRigidBody.angularVelocity = Vector3.zero;
            enemyRigidBody.isKinematic = true;   // freeze movement again
        }


    }

}