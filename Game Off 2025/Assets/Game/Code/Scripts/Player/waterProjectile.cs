using UnityEngine;
using Unity.Netcode;

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

        private void Awake()
        {
            netObj = GetComponent<NetworkObject>();

            // Ensure water-to-water collisions are ignored immediately
            int myLayerIndex = gameObject.layer;
            for (int i = 0; i < 32; i++)
            {
                if ((waterLayer.value & (1 << i)) != 0)
                {
                    Physics.IgnoreLayerCollision(myLayerIndex, i, true);
                }
            }
        }

        private void Start()
        {
            // Lifetime safety
            StartCoroutine(AutoDespawn());
        }

        private System.Collections.IEnumerator AutoDespawn()
        {
            yield return new WaitForSeconds(lifetime);

            if (IsServer && netObj != null && netObj.IsSpawned)
                netObj.Despawn();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsServer) return; // only the server handles collisions & despawn

            GameObject other = collision.gameObject;
            int otherLayer = other.layer;

            // Ignore water-to-water collisions
            if (((1 << otherLayer) & waterLayer.value) != 0)
                return;

            // BUTTON: press it
            if (((1 << otherLayer) & buttonLayer.value) != 0 && other.TryGetComponent(out Button button))
            {
                button.pressed = true;
            }

            // PLAYER: push but no damage
            if (((1 << otherLayer) & playerLayer.value) != 0)
            {
                if (other.TryGetComponent(out Rigidbody playerRb))
                {
                    Vector3 pushDir = collision.contacts.Length > 0 ? -collision.contacts[0].normal : transform.forward;
                    playerRb.AddForce(pushDir.normalized * pushForceToPlayer, ForceMode.VelocityChange);
                }
            }

            // ENEMY: damage and push
            if (((1 << otherLayer) & enemyLayer.value) != 0)
            {
                if (other.TryGetComponent(out WalkingEnemy enemy))
                {
                    enemy.TakeDamage(damageToEnemy);
                }
                if (other.TryGetComponent(out Rigidbody enemyRb))
                {
                    Vector3 pushDir = collision.contacts.Length > 0 ? -collision.contacts[0].normal : transform.forward;
                    enemyRb.AddForce(pushDir.normalized * pushForceToEnemyOrBox, ForceMode.VelocityChange);
                }
            }

            // BOX or physics object: push
            if (((1 << otherLayer) & boxLayer.value) != 0)
            {
                if (other.TryGetComponent(out Rigidbody boxRb))
                {
                    Vector3 pushDir = collision.contacts.Length > 0 ? -collision.contacts[0].normal : transform.forward;
                    boxRb.AddForce(pushDir.normalized * pushForceToEnemyOrBox, ForceMode.VelocityChange);
                }
            }

            //  Despawn networked projectile (instead of Destroy)
            if (IsServer && netObj != null && netObj.IsSpawned)
                netObj.Despawn();
        }
    }
}
