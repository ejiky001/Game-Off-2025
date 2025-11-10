using System;
using Unity.Multiplayer.Center.NetcodeForGameObjects;
using UnityEngine;

public class TriggerRelay : MonoBehaviour
{
    private WalkingEnemy enemy;

    public void Init(WalkingEnemy enemyScript)
    {
        enemy = enemyScript;
    }

    internal void Init(RollingEnemy rollingEnemy)
    {
        throw new NotImplementedException();
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
