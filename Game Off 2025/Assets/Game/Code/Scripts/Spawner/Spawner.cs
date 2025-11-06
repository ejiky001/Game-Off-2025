using Unity.Netcode;
using UnityEngine;

public class Spawner : NetworkBehaviour
{
    [SerializeField] private GameObject spawnPrefab;
    [SerializeField] private Transform spawnLocation;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        SpawnEnemy();
    }

    private void SpawnEnemy()
    {
        GameObject enemy = Instantiate(spawnPrefab, spawnLocation.position, spawnLocation.rotation);
        enemy.GetComponent<NetworkObject>().Spawn();
        Debug.Log("SERVER: Enemy spawned");
    }
}
