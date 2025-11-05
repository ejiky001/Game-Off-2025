using UnityEngine;
using Unity.Netcode;
using System.Collections;

namespace Unity.Multiplayer.Center.NetcodeForGameObjects
{
    public class WaterProjectile : NetworkBehaviour
    {
        [Header("Hose Settings")]
        [SerializeField] private float lifetime = 3f;
        [SerializeField] private float damageToEnemy = 0.5f;
        [SerializeField] private float pushForceToEnemyOrBox = 0.5f;
        [SerializeField] private float pushForceToPlayer = 10f;

        [Header("Layer Masks")]
        [SerializeField] private LayerMask enemyLayer;
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private LayerMask boxLayer;
        [SerializeField] private LayerMask buttonLayer;
        [SerializeField] private LayerMask waterLayer;

        private NetworkObject netObj;
        private Rigidbody rb;

        private void Awake()
        {
            netObj = GetComponent<NetworkObject>();
            rb = GetComponent<Rigidbody>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            Debug.Log($"[WaterProjectile] OnNetworkSpawn. IsServer={IsServer} IsOwner={IsOwner} IsSpawned={(netObj != null ? netObj.IsSpawned : false)} NetId={(netObj != null ? netObj.NetworkObjectId : ulong.MaxValue)}");
            StartCoroutine(AutoDespawn());
        }

        private IEnumerator AutoDespawn()
        {
            yield return new WaitForSeconds(lifetime);
            Debug.Log("[WaterProjectile] AutoDespawn triggered");
            SafeDespawn();
        }

        private void OnCollisionEnter(Collision collision)
        {
            // We still want the server to apply authoritative logic.
            // But always clean up the local visual afterwards.
            GameObject other = collision.gameObject;
            int otherLayer = other.layer;

            // Ignore water-to-water collisions
            if (((1 << otherLayer) & waterLayer.value) != 0)
            {
                return;
            }

            // Run server-only authoritative effects
            if (IsServer)
            {
                // BUTTON: press
                if (((1 << otherLayer) & buttonLayer.value) != 0 && other.TryGetComponent(out Button button))
                {
                    button.pressed = true;
                }

                // PLAYER: push (no damage)
                if (((1 << otherLayer) & playerLayer.value) != 0)
                {
                    if (other.TryGetComponent(out Rigidbody playerRb))
                    {
                        Vector3 pushDir = collision.contacts.Length > 0
                            ? -collision.contacts[0].normal
                            : transform.forward;
                        playerRb.AddForce(pushDir.normalized * pushForceToPlayer, ForceMode.VelocityChange);
                    }
                }

                // ENEMY: damage + push
                if (((1 << otherLayer) & enemyLayer.value) != 0)
                {
                    if (other.TryGetComponent(out WalkingEnemy enemy))
                    {
                        enemy.TakeDamage(damageToEnemy);
                    }

                    if (other.TryGetComponent(out Rigidbody enemyRb))
                    {
                        Vector3 pushDir = collision.contacts.Length > 0
                            ? -collision.contacts[0].normal
                            : transform.forward;
                        enemyRb.AddForce(pushDir.normalized * pushForceToEnemyOrBox, ForceMode.VelocityChange);
                    }
                }

                // BOX: push
                if (((1 << otherLayer) & boxLayer.value) != 0)
                {
                    if (other.TryGetComponent(out Rigidbody boxRb))
                    {
                        Vector3 pushDir = collision.contacts.Length > 0
                            ? -collision.contacts[0].normal
                            : transform.forward;
                        boxRb.AddForce(pushDir.normalized * pushForceToEnemyOrBox, ForceMode.VelocityChange);
                    }
                }
            }
            else
            {
                // For debugging: indicate a non-server instance detected collision
                Debug.Log($"[WaterProjectile] Collision on non-server instance with {other.name}. IsServer={IsServer}. We'll still destroy local visual.");
            }

            // Always attempt to remove networked state (server) and always remove the local object immediately.
            SafeDespawn();
        }

        /// <summary>
        /// Despawns network object on server (if spawned) and always destroys the local instance.
        /// This ensures no ghost visuals remain on clients even if the projectile wasn't network-spawned.
        /// </summary>
        private void SafeDespawn()
        {
            // If this is a networked object on the server, despawn it so clients know it's gone.
            if (IsServer && netObj != null && netObj.IsSpawned)
            {
                Debug.Log($"[WaterProjectile] Server despawning NetworkObject id:{netObj.NetworkObjectId}");
                try
                {
                    netObj.Despawn();
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[WaterProjectile] Despawn threw: {ex.Message}");
                }
            }

            // Destroy local GameObject immediately (host and clients). This ensures visuals are removed.
            // Destroy is safe to call even if netObj.Despawn() removed the network object already.
            if (gameObject != null)
            {
                // Use DestroyImmediate when in editor stop? No — use regular Destroy.
                Destroy(gameObject);
            }
        }
    }
}
