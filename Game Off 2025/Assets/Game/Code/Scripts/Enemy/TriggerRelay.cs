using Unity.Multiplayer.Center.NetcodeForGameObjects;
using UnityEngine;

public class TriggerRelay : MonoBehaviour
{
    private WalkingEnemy enemy;

    public void Init(WalkingEnemy enemyScript)
    {
        enemy = enemyScript;
    }

    private void OnTriggerEnter(Collider other)
    {
        enemy?.HandleTriggerEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        enemy?.HandleTriggerExit(other);
    }
}
