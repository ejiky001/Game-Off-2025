using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Unity.Multiplayer.Center.NetcodeForGameObjects
{
    public class WaterHose : NetworkBehaviour
    {
        [Header("Damage & Force Settings")]
        [SerializeField] private float damagePerSecond = 1f;
        [SerializeField] private float pushForce = 2f;

        [Header("Layer Settings")]
        public LayerMask playerLayer;
        public LayerMask enemyLayer;
        public LayerMask boxLayer;
        public LayerMask buttonLayer;

        private ParticleSystem ps;
        private readonly List<ParticleCollisionEvent> collisionEvents = new List<ParticleCollisionEvent>();

        // Tracks which objects are being hit
        private readonly Dictionary<GameObject, float> hitTimers = new Dictionary<GameObject, float>();

        void Start()
        {
            ps = GetComponent<ParticleSystem>();
        }

        void Update()
        {
            // Damage tick — applies 1 DPS
            List<GameObject> keys = new List<GameObject>(hitTimers.Keys);

            foreach (GameObject obj in keys)
            {
                if (obj == null)
                {
                    hitTimers.Remove(obj);
                    continue;
                }

                hitTimers[obj] -= Time.deltaTime;
                if (hitTimers[obj] <= 0f)
                {
                    hitTimers[obj] = 1f; // reset timer

                    int layer = obj.layer;


                    // dmg Enemy
                    if (((1 << layer) & enemyLayer) != 0 && obj.TryGetComponent<WalkingEnemy>(out WalkingEnemy enemy))
                    {
                        // Ensure this logic runs only on the server to prevent race conditions
                        if (IsServer)
                        {
                            enemy.TakeDamage(damagePerSecond);
                        }
                    }
                }
            }
        }

        void OnParticleCollision(GameObject other)
        {
            if (!IsServer) return; // Only run authoritative logic on the server

            int count = ps.GetCollisionEvents(other, collisionEvents);

            if (count == 0) return;

            Vector3 hitPoint = collisionEvents[0].intersection;
            Vector3 hitNormal = collisionEvents[0].normal;
            int otherLayer = other.layer;

            // push Player, Enemy, Box
            if (((1 << otherLayer) & (playerLayer | enemyLayer | boxLayer)) != 0)
            {
                Rigidbody rb = other.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.AddForceAtPosition(-hitNormal * pushForce, hitPoint, ForceMode.Impulse);
                }
            }

            // dmg timer
            if (((1 << otherLayer) & (enemyLayer)) != 0)
            {
                if (!hitTimers.ContainsKey(other))
                {
                    hitTimers.Add(other, 1f);
                }
                else
                {
                    // refresh timer each collision so damage keeps ticking while being hit
                    hitTimers[other] = Mathf.Min(hitTimers[other], 1f);
                }
            }

            // button toggle
            if (((1 << otherLayer) & buttonLayer) != 0)
            {
                // Note: WaterHose can affect the generic 'Button' and the colored ones (BlueButton, RedButton, etc.).
                // If you want the water to press ALL buttons, use the base 'Button' component.
                if (other.TryGetComponent<Button>(out Button button))
                {
                    // FIX: The 'pressed' variable was removed. Use the NetworkVariable property instead.
                    // This must be set on the server (checked by IsServer check at top of method).
                    if (button.IsPressed.Value == false)
                    {
                        button.IsPressed.Value = true;
                    }
                }
            }
        }

        void LateUpdate()
        {
            // Clean up objects that aren’t being hit anymore
            List<GameObject> toRemove = new List<GameObject>();
            foreach (var pair in hitTimers)
            {
                if (pair.Key == null) toRemove.Add(pair.Key);
            }
            foreach (var obj in toRemove)
            {
                hitTimers.Remove(obj);
            }
        }
    }
}