using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Drives a single, reused SineVFX <see cref="LightningSystemChain" /> so the Chain Lightning skill
///     (<see cref="ChainLightning" />) actually draws an animated bolt through the enemies a chain hops
///     across, instead of only flashing a one-off prefab at each target.
///
///     Only one instance is needed: the SineVFX chain is a persistent <c>VisualEffect</c> that renders
///     continuously between the live positions of its <c>chainPoints</c> array while
///     <see cref="LightningSystemChain.vfxEnabled" /> is true, so we keep the GameObject enabled (so its
///     <c>Start</c> stays run and the effect stays initialized) and toggle it via that bool rather than
///     activating/deactivating the object. Each <see cref="ShowChain" /> repositions the shared anchors,
///     turns the bolt on, and it auto-turns-off after <see cref="flashDuration" /> — so if several sword
///     hits in one swing each chain, the most recent one wins the shared visual (the damage of every chain
///     still lands; only the cosmetic bolt is shared).
///
///     The anchors the SineVFX reads are our own transforms, snapped to the captured hop world positions,
///     and parented to a root container (never the moving player) so the bolt stays put in the world for
///     the duration of the flash rather than dragging along as the player runs.
/// </summary>
public class ChainLightningVfx : MonoBehaviour
{
    [Tooltip("The SineVFX LightningSystemChain instance to drive (a child of this object). Use a 'SingleVFXOnly' Chain prefab — the 'WithExampleMeshes' variants carry visible demo spheres.")]
    [SerializeField] private LightningSystemChain lightningChain;
    [Tooltip("Seconds the bolt stays lit after a chain fires before it switches off.")]
    [SerializeField] private float flashDuration = 0.25f;
    [Tooltip("Bolt thickness/scale. Set on the SineVFX chain at startup (masterScale). Bump up if the arc reads too thin at gameplay camera distance; the raw prefab's ~1 is often too fine for enemy-to-enemy spans.")]
    [SerializeField] private float boltScale = 1.5f;
    [Tooltip("Maximum chain points the bolt can connect (origin + bounces). Chains longer than this are clamped.")]
    [SerializeField] private int maxAnchors = 16;

    private Transform anchorRoot;
    private Transform[] anchors;
    // Pre-allocated per-length slices so ShowChain never allocates: slices[n] is a Transform[n]
    // referencing anchors 0..n-1 (SineVFX derives its point count from chainPoints.Length, so the
    // array handed to it must be exactly the used length, not a padded buffer).
    private Transform[][] slices;

    private float offTimer;
    private bool anyError;

    private void OnValidate()
    {
        if (lightningChain == null)
        {
            lightningChain = GetComponentInChildren<LightningSystemChain>();
        }
    }

    private void Start()
    {
        if (lightningChain == null)
        {
            Debug.LogError("ChainLightningVfx 'lightningChain' (a child LightningSystemChain) is not assigned.");
            anyError = true;
            return;
        }
        if (maxAnchors < 2)
        {
            maxAnchors = 2;
        }

        anchorRoot = new GameObject("ChainLightningAnchors").transform;
        anchors = new Transform[maxAnchors];
        for (int i = 0; i < maxAnchors; i++)
        {
            GameObject go = new GameObject("ChainAnchor" + i);
            go.transform.SetParent(anchorRoot, false);
            anchors[i] = go.transform;
        }

        slices = new Transform[maxAnchors + 1][];
        for (int n = 2; n <= maxAnchors; n++)
        {
            slices[n] = new Transform[n];
            for (int i = 0; i < n; i++)
            {
                slices[n][i] = anchors[i];
            }
        }

        // Force safe, self-contained scaling. The SineVFX chain prefabs ship with autoScaleEnabled = true
        // and no autoScaleAnchor assigned, so ProcessAutoScale() dereferences a null Transform and throws
        // every frame — which aborts the rest of its Update (the ChainCount/CreateLightning driving), so
        // the bolt never really renders. Driving masterScale directly with autoScale off avoids that trap
        // and means the effect works from a raw prefab drop without extra inspector wiring.
        lightningChain.autoScaleEnabled = false;
        lightningChain.masterScale = boltScale;
        lightningChain.vfxEnabled = false;
    }

    private void Update()
    {
        if (anyError || !lightningChain.vfxEnabled)
        {
            return;
        }

        offTimer -= Time.deltaTime;
        if (offTimer <= 0f)
        {
            lightningChain.vfxEnabled = false;
        }
    }

    /// <summary>
    ///     Lights the shared bolt through <paramref name="points" /> (a chain's origin followed by each
    ///     hop target, in order). A no-op with fewer than two points — there's nothing to connect.
    /// </summary>
    public void ShowChain(IReadOnlyList<Vector3> points)
    {
        if (anyError || points == null || points.Count < 2)
        {
            return;
        }

        int count = Mathf.Min(points.Count, anchors.Length);
        for (int i = 0; i < count; i++)
        {
            anchors[i].position = points[i];
        }

        lightningChain.chainPoints = slices[count];
        lightningChain.vfxEnabled = true;
        offTimer = flashDuration;
    }

    private void OnDestroy()
    {
        if (anchorRoot != null)
        {
            Destroy(anchorRoot.gameObject);
        }
    }
}
