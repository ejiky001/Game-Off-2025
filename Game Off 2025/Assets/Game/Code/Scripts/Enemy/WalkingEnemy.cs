using UnityEngine;
using System.Collections;

public class WalkingEnemy : MonoBehaviour
{
    [SerializeField] private float health = 5f;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private int attackDamage = 1;

    [SerializeField] private float attackDelay = 3f; // seconds between attacks

    public LayerMask playerLayer;

    private Player targetPlayer;
    private Coroutine attackCoroutine;

    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) == 0)
            return;

        Player player = other.GetComponent<Player>();
        if (player == null) return;

        targetPlayer = player;

        // Start attacking if not already
        if (attackCoroutine == null)
        {
            attackCoroutine = StartCoroutine(AttackLoop());
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) == 0)
            return;

        Player player = other.GetComponent<Player>();
        if (player == targetPlayer)
        {
            StopAttacking();
        }
    }

    private IEnumerator AttackLoop()
    {
        //delay before first attack
        yield return new WaitForSeconds(1f);

        while (targetPlayer != null && targetPlayer.isAlive)
        {
            Attack(targetPlayer);
            yield return new WaitForSeconds(attackDelay);
        }

        StopAttacking();
    }

    private void Attack(Player player)
    {
        if (player != null && player.isAlive)
        {
            player.TakeDamage(attackDamage);
        }
    }

    private void StopAttacking()
    {
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }
        targetPlayer = null;
    }
    public void TakeDamage(float amount)
    {
        health -= amount;
        if (health <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Destroy(gameObject); 
    }

}
