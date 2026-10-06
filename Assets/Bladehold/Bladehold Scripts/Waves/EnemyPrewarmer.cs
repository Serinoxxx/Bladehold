using System.Collections;
using System.Collections.Generic;
using Bladehold.UI;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
///     Behind-the-loading-screen dress rehearsal that removes the hitch on a battle scene's first enemy
///     spawn and first enemy death. Those hitches are one-time costs, not per-spawn ones: shader
///     variants compiling on first draw (the baked crowd shader, the promoted real rig, blood and
///     particle materials), the first instantiation of pooled VFX, audio clips loading on first play,
///     and first runs of the spawn and death code. Pooling the enemies themselves wouldn't touch any of
///     that, so instead this pays it up front, while nobody is watching:
///     <list type="number">
///         <item>
///             Holds the loading screen (<see cref="LoadingScreenManager.HoldFadeOut" />), or covers the
///             screen itself when the scene was entered without one (Editor play, DevConsole reload).
///         </item>
///         <item>
///             Stages one of every type <see cref="SurvivorsSpawner.GetPrewarmTypes" /> says this sector can
///             field, plus the spawn telegraph, in front of the main camera (in the frustum, so it really
///             draws), and tops <see cref="ParticlePool" /> up for every pooled burst on their MMF players.
///         </item>
///         <item>
///             After a few rendered frames, kills each baked crowd type twice: once into a real ragdoll
///             (promotes to the real rig) and once past the ragdoll cap (baked fall). The kills credit the
///             victim itself, so the existing enemy-on-enemy rules skip gold and kill stats; powerup drops
///             are stripped first, and <see cref="IsRehearsing" /> keeps the ultimate bar out of it. Other
///             types aren't killed (their death effects reach into the scene); they're destroyed alive as
///             soon as they've drawn.
///         </item>
///         <item>Clears everything, restores the volume (muted throughout) and releases the loading screen.</item>
///     </list>
///     Sits beside <see cref="SurvivorsSpawner" /> on the EnemySpawner prefab. Tunables are on
///     <see cref="EnemyPrewarmConfigSO" />.
/// </summary>
public class EnemyPrewarmer : MonoBehaviour
{
    /// <summary>True while rehearsal kills are being dealt. Systems that reward kills or damage skip these.</summary>
    public static bool IsRehearsing { get; private set; }

    [SerializeField] private EnemyPrewarmConfigSO config;
    [SerializeField] private SurvivorsSpawner spawner;

    private readonly List<EnemyDefinition> defs = new List<EnemyDefinition>();
    private readonly List<GameObject> prefabs = new List<GameObject>();
    private readonly List<GameObject> stagedAlive = new List<GameObject>();
    private readonly List<GameObject> stagedCorpses = new List<GameObject>();
    private readonly List<Health> ragdollKills = new List<Health>();
    private readonly List<Health> bakedFallKills = new List<Health>();
    private bool holdingLoadingScreen = false;
    private GameObject cover;
    private float savedVolume = 1f;
    private bool muted = false;
    private bool anyError = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => IsRehearsing = false;

    private void OnValidate()
    {
        if (spawner == null)
        {
            spawner = GetComponent<SurvivorsSpawner>();
        }
    }

    private void Awake()
    {
        // Awake runs inside the scene activation, before the loading screen starts its fade-out.
        LoadingScreenManager.HoldFadeOut();
        holdingLoadingScreen = true;
    }

    private void Start()
    {
        if (config == null)
        {
            Debug.LogError("[EnemyPrewarmer] EnemyPrewarmConfigSO is not assigned on " + gameObject.name + ".", this);
            anyError = true;
        }
        if (spawner == null)
        {
            Debug.LogError("[EnemyPrewarmer] SurvivorsSpawner is not assigned on " + gameObject.name + ".", this);
            anyError = true;
        }

        if (anyError)
        {
            Finish();
            return;
        }

        StartCoroutine(Rehearse());
    }

    private void OnDestroy()
    {
        Finish();
    }

    private IEnumerator Rehearse()
    {
        // Cinemachine places the camera in its first LateUpdate.
        yield return null;

        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[EnemyPrewarmer] No main camera; skipping the prewarm.", this);
            Finish();
            yield break;
        }

        spawner.GetPrewarmTypes(defs, prefabs);
        if (prefabs.Count == 0)
        {
            Finish();
            yield break;
        }

        if (!LoadingScreenManager.IsTransitioning)
        {
            cover = CreateCover();
        }
        savedVolume = AudioListener.volume;
        AudioListener.volume = 0f;
        muted = true;

        Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 center = StagingCenter(cam.transform.position, forward);
        Quaternion facing = Quaternion.LookRotation(-forward);

        // Crowd types are staged twice: one dies into a ragdoll, the other into a baked fall.
        int slots = 0;
        foreach (GameObject prefab in prefabs)
        {
            slots += IsCrowdType(prefab) ? 2 : 1;
        }

        int slot = 0;
        for (int i = 0; i < prefabs.Count; i++)
        {
            GameObject prefab = prefabs[i];
            bool crowd = IsCrowdType(prefab);
            for (int copy = 0; copy < (crowd ? 2 : 1); copy++)
            {
                GameObject enemy = Instantiate(prefab, SlotPosition(center, right, slot++, slots), facing);
                EnemyDefinitionApplier.Apply(enemy, defs[i]);
                if (copy == 0)
                {
                    PrewarmParticlePools(enemy);
                }

                Health health = crowd ? enemy.GetComponent<Health>() : null;
                if (health == null)
                {
                    stagedAlive.Add(enemy);
                    continue;
                }
                stagedCorpses.Add(enemy);
                (copy == 0 ? ragdollKills : bakedFallKills).Add(health);
            }
            // One type per frame keeps each frame's spike small.
            yield return null;
        }

        if (spawner.IndicatorPrefab != null)
        {
            stagedAlive.Add(Instantiate(spawner.IndicatorPrefab, center, Quaternion.identity));
        }

        for (int f = 0; f < config.aliveFrames; f++)
        {
            yield return null;
        }

        // Everything has drawn; clear the ones that aren't rehearsing a death before they can act.
        DestroyAll(stagedAlive);

        IsRehearsing = true;
        foreach (Health health in ragdollKills)
        {
            Kill(health, cam.transform.position);
        }
        int ragdollCap = EnemyRagdoll.MaxActive;
        EnemyRagdoll.MaxActive = 0;
        foreach (Health health in bakedFallKills)
        {
            Kill(health, cam.transform.position);
        }
        EnemyRagdoll.MaxActive = ragdollCap;
        IsRehearsing = false;

        yield return new WaitForSecondsRealtime(config.deathSeconds);

        DestroyAll(stagedCorpses);
        // The rehearsal hits bled onto the ground (and the decal holder outlives scene loads).
        BloodDecalManager.ClearAll();
        Finish();
    }

    private static bool IsCrowdType(GameObject prefab) => prefab.GetComponentInChildren<BakedCrowdAgent>(true) != null;

    /// <summary>A NavMesh spot ahead of the camera at ground height, nearer if that misses, else the nearest spawn point.</summary>
    private Vector3 StagingCenter(Vector3 cameraPosition, Vector3 forward)
    {
        float groundY = Player.Instance != null ? Player.Instance.transform.position.y : cameraPosition.y;
        float[] fractions = { 1f, 0.6f, 0.35f };
        foreach (float fraction in fractions)
        {
            Vector3 candidate = cameraPosition + forward * (config.stagingDistance * fraction);
            candidate.y = groundY;
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, config.navMeshSampleRadius, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }
        // Out of view, so no shader warm-up, but everything else still runs.
        return spawner.NearestSpawnPoint(cameraPosition);
    }

    private Vector3 SlotPosition(Vector3 center, Vector3 right, int slot, int slots)
    {
        Vector3 position = center + right * ((slot - (slots - 1) * 0.5f) * config.spacing);
        return NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas) ? hit.position : center;
    }

    private void PrewarmParticlePools(GameObject enemy)
    {
        if (config.particlePoolSize <= 0) return;
        foreach (MMF_Player player in enemy.GetComponentsInChildren<MMF_Player>(true))
        {
            if (player.FeedbacksList == null) continue;
            foreach (MMF_Feedback feedback in player.FeedbacksList)
            {
                if (feedback is MMF_PooledParticleBurst burst && burst.ParticlePrefab != null)
                {
                    ParticlePool.Prewarm(burst.ParticlePrefab, config.particlePoolSize);
                }
            }
        }
    }

    private static void Kill(Health health, Vector3 sourcePosition)
    {
        if (health == null || health.IsDead) return;

        // Immediate, so the dropper's OnDestroy unsubscribes before the death fires.
        foreach (PowerupDropper dropper in health.GetComponentsInChildren<PowerupDropper>(true))
        {
            DestroyImmediate(dropper);
        }

        health.ReceiveDamage(new Damage
        {
            value = 9999f,
            // Credited to the victim: CoinDropper and Enemy skip enemy-on-enemy kills (no gold, no kill stat).
            source = health,
            sourcePosition = sourcePosition,
            isPlayerDamage = false
        });
    }

    private static void DestroyAll(List<GameObject> objects)
    {
        foreach (GameObject go in objects)
        {
            if (go != null)
            {
                Destroy(go);
            }
        }
        objects.Clear();
    }

    private static GameObject CreateCover()
    {
        var root = new GameObject("EnemyPrewarmCover");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(root.transform, false);
        RectTransform rect = (RectTransform)fill.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        fill.GetComponent<Image>().color = Color.black;
        return root;
    }

    /// <summary>Idempotent: undoes everything the rehearsal changed, so a destroyed or failed run can't leave the game muted or the loading screen held.</summary>
    private void Finish()
    {
        IsRehearsing = false;
        if (muted)
        {
            AudioListener.volume = savedVolume;
            muted = false;
        }
        if (cover != null)
        {
            Destroy(cover);
            cover = null;
        }
        if (holdingLoadingScreen)
        {
            LoadingScreenManager.ReleaseFadeOut();
            holdingLoadingScreen = false;
        }
    }
}
