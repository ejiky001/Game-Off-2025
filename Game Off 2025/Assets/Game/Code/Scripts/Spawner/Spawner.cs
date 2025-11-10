using Unity.Netcode;
using UnityEngine;
using System.Collections; // Needed for Coroutines

public class Spawner : NetworkBehaviour
{
    [SerializeField] private GameObject spawnPrefab;
    [SerializeField] private Transform spawnLocation;
    [SerializeField] private float spawnDelay = 1.0f; // Customizable delay in seconds

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // Start the coroutine to delay the spawn process
        StartCoroutine(DelayedSpawnRoutine());
    }

    private IEnumerator DelayedSpawnRoutine()
    {
        // Wait for the specified delay
        yield return new WaitForSeconds(spawnDelay);

        // Once the delay is over, proceed with spawning
        SpawnEnemy();
    }

    private void SpawnEnemy()
    {
        GameObject enemy = Instantiate(spawnPrefab, spawnLocation.position, spawnLocation.rotation);
        // Ensure the prefab has a NetworkObject component, which it should if it's a network object
        NetworkObject netObj = enemy.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn();
            Debug.Log("SERVER: Enemy spawned with a delay.");
        }
        else
        {
            Debug.LogError("SpawnPrefab is missing a NetworkObject component!");
        }
    }
}