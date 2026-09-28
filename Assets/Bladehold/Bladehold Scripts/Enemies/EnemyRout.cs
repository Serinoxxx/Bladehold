using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Makes a straggler flee when its wave's objective resolves (plan 15): a short morale-break
///     stun, then it stops chasing and attacking and runs for the nearest spawn point.
///     <see cref="GameLoopManager" /> despawns whatever is still running after the rout. Kills during
///     the rout still pay, since the enemy's death goes through the normal <see cref="Health" /> path.
///     Added at runtime by <see cref="Begin" />; never authored on a prefab.
/// </summary>
public class EnemyRout : MonoBehaviour
{
    private const float StunSeconds = 0.75f;
    private const float FleeSpeedMultiplier = 1.4f;

    private Health health;
    private NavMeshAgent agent;
    private AIMovement movement;
    private AIAttack attack;
    private Vector3 fleeTo;

    public static EnemyRout Begin(GameObject enemy, Vector3 fleeTo)
    {
        if (enemy == null) return null;
        EnemyRout rout = enemy.GetComponent<EnemyRout>() ?? enemy.AddComponent<EnemyRout>();
        rout.fleeTo = fleeTo;
        rout.StopAllCoroutines();
        rout.StartCoroutine(rout.RoutRoutine());
        return rout;
    }

    private IEnumerator RoutRoutine()
    {
        health = GetComponent<Health>();
        agent = GetComponent<NavMeshAgent>();
        movement = GetComponent<AIMovement>();
        attack = GetComponent<AIAttack>();
        if (health == null || health.IsDead) yield break;

        // Morale break: the same stagger a captain's death causes.
        if (movement != null) movement.SetMovementPaused(true);
        if (attack != null) attack.enabled = false;
        yield return new WaitForSeconds(StunSeconds);
        if (health == null || health.IsDead) yield break;

        if (movement != null)
        {
            movement.SetMovementPaused(false);
            movement.enabled = false; // stop it re-targeting the player
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed *= FleeSpeedMultiplier;
            agent.SetDestination(fleeTo);
        }
    }
}
