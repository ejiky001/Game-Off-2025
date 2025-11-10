using System;
using Unity.Multiplayer.Center.NetcodeForGameObjects;
using UnityEngine;

public class TriggerRelay : MonoBehaviour
{
    private WalkingEnemy enemy;
    private RollingEnemy rollingEnemy;
    private ShootingEnemy shootingEnemy;
    private DogEnemy dogEnemy;

    public void Init(WalkingEnemy enemyScript)
    {
        enemy = enemyScript;
    }

    internal void Init(RollingEnemy rollingEnemyScript)
    {
        rollingEnemy = rollingEnemyScript;
    }

    internal void Init(ShootingEnemy shootingEnemyScript)
    {
        shootingEnemy = shootingEnemyScript;
    }

    internal void Init(DogEnemy dogEnemyScript)
    {
        dogEnemy = dogEnemyScript;
    }

    private void OnTriggerEnter(Collider other)
    {
        enemy?.HandleTriggerEnter(other);
        rollingEnemy?.HandleTriggerEnter(other);
        shootingEnemy?.HandleTriggerEnter(other);
        dogEnemy?.HandleTriggerEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        enemy?.HandleTriggerExit(other);
        rollingEnemy?.HandleTriggerExit(other);
        shootingEnemy?.HandleTriggerExit(other);
        dogEnemy?.HandleTriggerExit(other);
    }
}
