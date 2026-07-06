using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Scene singleton that caps how many corpses linger at once. <see cref="CorpseDespawner" />s
///     register here on death (oldest first); when the count exceeds
///     <see cref="CorpseConfigSO.maxCorpses" />, the oldest are told to sink early. Optional — with
///     no manager in the scene, each corpse just runs its own lifetime timer.
/// </summary>
public class CorpseManager : MonoBehaviour
{
    public static CorpseManager Instance;

    [SerializeField] private CorpseConfigSO config;

    // FIFO: corpses despawn oldest-first, both naturally and when the cap evicts them. Entries that
    // despawned on their own timer become destroyed (== null) and are pruned as they reach the front.
    private readonly Queue<CorpseDespawner> corpses = new Queue<CorpseDespawner>();

    private bool anyError = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        if (config == null)
        {
            Debug.LogError("CorpseConfigSO is not assigned in the inspector.");
            anyError = true;
        }
    }

    /// <summary>Called by a <see cref="CorpseDespawner" /> when its enemy dies.</summary>
    public void Register(CorpseDespawner corpse)
    {
        if (anyError || corpse == null)
        {
            return;
        }

        PruneDespawned();
        corpses.Enqueue(corpse);

        if (config.maxCorpses <= 0)
        {
            return;
        }
        while (corpses.Count > config.maxCorpses)
        {
            CorpseDespawner oldest = corpses.Dequeue();
            if (oldest != null)
            {
                oldest.DespawnNow();
            }
        }
    }

    private void PruneDespawned()
    {
        while (corpses.Count > 0 && corpses.Peek() == null)
        {
            corpses.Dequeue();
        }
    }
}
