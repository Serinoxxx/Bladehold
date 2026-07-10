using System.Collections;
using UnityEngine;

/// <summary>
///     The Reincarnate "Cavalier" node: when <see cref="StatType.StartMounted" /> is owned, each
///     run begins with a fresh horse spawned beside the player and the player already in the
///     saddle. Waits one frame so <c>SkillTreeService</c>/<c>ReincarnateService</c> have re-applied
///     the saved modifiers first; a scene reload (restart, Reincarnate) naturally re-runs it. The
///     node also grants riding by itself — see <see cref="PlayerMount.CanRide" />'s code-side OR —
///     so it keeps working after a Reincarnate wipes the gold tree.
/// </summary>
public class StartMountedSpawner : MonoBehaviour
{
    [SerializeField] private PlayerMount mount;
    [SerializeField] private PlayerStats stats;
    [Tooltip("The Horse prefab to spawn (riderless default state).")]
    [SerializeField] private GameObject horsePrefab;
    [Tooltip("Local-space offset from the player where the horse appears.")]
    [SerializeField] private Vector3 spawnLocalOffset = new Vector3(1.5f, 0f, 0f);

    private void OnValidate()
    {
        if (mount == null)
        {
            mount = GetComponent<PlayerMount>();
        }
        if (stats == null)
        {
            stats = GetComponent<PlayerStats>();
        }
    }

    private void Start()
    {
        StartCoroutine(SpawnWhenReady());
    }

    private IEnumerator SpawnWhenReady()
    {
        // One frame: the tree services re-apply persisted purchases in their own Start, and stat
        // bases register in every owner's Start — both orders are unknowable this frame.
        yield return null;

        if (mount == null || stats == null)
        {
            Debug.LogError("StartMountedSpawner needs PlayerMount and PlayerStats on the GameObject.");
            yield break;
        }

        if (stats.GetValue(StatType.StartMounted) < 1f)
        {
            yield break;
        }

        if (horsePrefab == null)
        {
            Debug.LogWarning("StartMountedSpawner: the Cavalier node is owned but no Horse prefab is assigned — starting on foot.");
            yield break;
        }

        Vector3 position = transform.TransformPoint(spawnLocalOffset);
        // Settle the horse onto whatever ground is under the spawn point.
        if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
        {
            position = hit.point;
        }

        GameObject horse = Instantiate(horsePrefab, position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
        HorseMotor motor = horse.GetComponent<HorseMotor>();
        if (motor == null)
        {
            Debug.LogError("StartMountedSpawner: the Horse prefab has no HorseMotor; cannot start mounted.");
            yield break;
        }

        if (!mount.TryMount(motor))
        {
            Debug.LogWarning("StartMountedSpawner: TryMount failed (riding locked or the horse is missing its seat) — starting on foot beside the horse.");
        }
    }
}
