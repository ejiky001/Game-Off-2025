using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using Unity.Multiplayer.Center.NetcodeForGameObjects;


/// <summary>
/// This script manages the player spawning process when the dedicated game scene is loaded.
/// It MUST be placed on a GameObject in the GameScene.
/// Requires the NetworkManager's "Auto Create Player" to be DISABLED.
/// </summary>
/// namespace CustomGame
namespace CustomGame
{
    public class GameSceneSpawnManager : NetworkBehaviour
    {
        [Header("Configuration")]
        [Tooltip("The Player prefab that should be spawned for each client. MUST be a Network Prefab.")]
        [SerializeField] private GameObject m_PlayerPrefab;

        [Tooltip("List of potential spawn points in the scene (assign these in the Inspector).")]
        [SerializeField] private List<Transform> m_SpawnPoints;

        private int m_NextSpawnIndex = 0;

        public override void OnNetworkSpawn()
        {
            // This script should only run its logic on the Server/Host.
            if (!IsServer)
            {
                // Destroy the component on clients as it's not needed for spawning.
                Destroy(this);
                return;
            }

            // --- Core Spawning Logic on Scene Load ---

            // This delegate is invoked on the server after a network scene load finishes 
            // for ALL connected clients.
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoadCompleted;

            Debug.Log("[GameSceneSpawnManager] Server initialized. Listening for scene load complete.");
        }

        public override void OnNetworkDespawn()
        {
            // Clean up the subscription when this NetworkObject is destroyed.
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoadCompleted;
            }
        }

        /// <summary>
        /// Event handler called by NGO when a scene load operation finishes.
        /// This is where we manually spawn players.
        /// </summary>
        private void OnSceneLoadCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            Debug.Log($"[GameSceneSpawnManager] Scene load completed for {sceneName}. Spawning players...");

            // Iterate over all connected client IDs.
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                // Check if the client already has a Player Object spawned (shouldn't happen, but good practice).
                if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) || client.PlayerObject != null)
                {
                    continue;
                }

                // 1. Determine Spawn Position
                Vector3 spawnPos = GetNextSpawnPoint();

                // 2. Instantiate the Player Prefab
                GameObject playerGameObject = Instantiate(m_PlayerPrefab, spawnPos, Quaternion.identity);

                // 3. Spawn the NetworkObject and assign ownership to the client (PlayerObject status = true)
                NetworkObject playerNetworkObject = playerGameObject.GetComponent<NetworkObject>();
                if (playerNetworkObject != null)
                {
                    playerNetworkObject.SpawnAsPlayerObject(clientId, true);

                    // 4. Call ServerTeleport (as defined in Player.cs) to ensure Rigidbody is reset
                    Player playerComponent = playerGameObject.GetComponent<Player>();
                    if (playerComponent != null)
                    {
                        playerComponent.ServerTeleport(spawnPos);
                    }
                }
                else
                {
                    Debug.LogError($"[GameSceneSpawnManager] Player Prefab missing NetworkObject component!");
                }
            }

            Debug.Log("[GameSceneSpawnManager] All players manually spawned.");
        }

        /// <summary>
        /// Cycles through the spawn points list to ensure even distribution.
        /// </summary>
        private Vector3 GetNextSpawnPoint()
        {
            if (m_SpawnPoints == null || m_SpawnPoints.Count == 0)
            {
                Debug.LogError("No spawn points assigned! Returning origin (0,0,0).");
                return Vector3.zero;
            }

            Transform spawnTransform = m_SpawnPoints[m_NextSpawnIndex];
            m_NextSpawnIndex = (m_NextSpawnIndex + 1) % m_SpawnPoints.Count; // Cycle index

            return spawnTransform.position;
        }
    }
}