using UnityEngine;
using Unity.Netcode;
using System.Collections;

namespace Unity.Multiplayer.Center.NetcodeForGameObjects
{
    public class EnemyProjectile : NetworkBehaviour
    {
        [Header("Projectile Properties")]
        [SerializeField] private float lifetime = 5f; // Projectile disappears after 5 seconds
        [SerializeField] private int damageToPlayer = 1;

        // Set this layer mask in the Inspector on the projectile prefab
        [Header("Targeting")]
        [SerializeField] private LayerMask playerLayer;

        private NetworkObject netObj;
        private Rigidbody rb;

        private void Awake()
        {
            netObj = GetComponent<NetworkObject>();
            rb = GetComponent<Rigidbody>();

            // REQUIREMENT: Ensure no gravity on the Rigidbody component
            if (rb != null)
            {
                rb.useGravity = false;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            // Only the server should manage the despawn timer
            if (IsServer)
            {
                StartCoroutine(AutoDespawn());
            }
        }

        private IEnumerator AutoDespawn()
        {
            // Waits for 5 seconds
            yield return new WaitForSeconds(lifetime);
            SafeDespawn();
        }

        private void OnCollisionEnter(Collision collision)
        {
            GameObject other = collision.gameObject;
            int otherLayer = other.layer;

            if (IsServer)
            {
                // REQUIREMENT: Only interact with the Player layer.
                if (((1 << otherLayer) & playerLayer.value) != 0)
                {
                    // Apply damage only to the Player
                    if (other.TryGetComponent(out Player player))
                    {
                        // Assuming Player has a TakeDamageServerRpc
                        player.TakeDamageServerRpc(damageToPlayer);
                    }
                }

                // The projectile despawns immediately upon collision with anything.
            }

            // Always despawn and destroy the local object on hit
            SafeDespawn();
        }

        /// <summary>
        /// Despawns network object on server and destroys local instance on all clients.
        /// </summary>
        private void SafeDespawn()
        {
            if (IsServer && netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(destroy: true);
            }
            else if (!IsServer && gameObject != null)
            {
                // If this is a client-side visual, destroy it locally 
                // (Server's despawn should handle this, but this is a safety net)
                Destroy(gameObject);
            }
        }
    }
}