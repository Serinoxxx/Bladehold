using System;
using System.Collections.Generic;
using DamageNumbersPro;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     The Dome Warden's dome: a bubble around the caster that catches projectiles aimed at any
///     enemy inside it (the Warden included). A caught hit damages the dome instead and pops a
///     purple number on its surface. At 0 the dome breaks, and the Warden raises it again at full
///     health after a cooldown. It's gone for good when the Warden dies.
///     - Tower projectiles (arrows, ballista bolts, nets, Tesla bolts) are always caught.
///       <see cref="CatapultProjectile" /> boulders burst on the shell via <see cref="FindDomeAt" />.
///     - The hero's projectiles (bow, thrown axe, wand) are caught only while the hero is outside,
///       so the hero can shoot the dome down or walk in.
///     - Melee, ground traps and zones aren't projectiles and get through.
///
///     Enemies inside are hooked through <see cref="Health.TryBlockDamage" /> on a short rescan and
///     unhooked when they leave or die, or when the dome breaks. Statuses that callers apply straight
///     after a hit are skipped through <see cref="BlockedThisFrame" />. Nets bypass Health, so
///     <see cref="NetThrowerDefense" /> asks <see cref="IsInsideAnyDome" /> directly.
/// </summary>
public class ProjectileDome : MonoBehaviour
{
    [SerializeField] private ProjectileDomeSO data;
    [SerializeField] private Health health;
    [Tooltip("Damage number popped (tinted with the SO's hitNumberColor) where a projectile hits the dome. The generator wires the enemy's own popup prefab.")]
    [SerializeField] private DamageNumber hitNumberPrefab;
    [Tooltip("Optional: played where a projectile hits the dome (a glassy ping).")]
    [SerializeField] private MMF_Player hitFeedback;
    [Tooltip("Optional: played when the dome breaks.")]
    [SerializeField] private MMF_Player breakFeedback;
    [Tooltip("Optional: played when the Warden raises the dome again.")]
    [SerializeField] private MMF_Player rebuildFeedback;
    [SerializeField] private LayerMask enemyLayers = ~0;

    private static readonly List<ProjectileDome> active = new List<ProjectileDome>();
    private static readonly Dictionary<Health, int> blockedFrame = new Dictionary<Health, int>();

    private readonly Dictionary<Health, Func<Damage, bool>> covered = new Dictionary<Health, Func<Damage, bool>>();
    private readonly List<Health> leaving = new List<Health>();
    private readonly HashSet<Health> seenThisScan = new HashSet<Health>();
    private GameObject visual;
    private float currentHealth;
    private float rebuildAtTime;
    private float nextScanTime;
    private bool isBroken;
    private bool isDown;
    private bool anyError;

    public float Radius => data != null ? data.radius : 0f;
    public float CurrentHealth => currentHealth;
    public bool IsBroken => isBroken;
    public bool IsUp => !isDown && !anyError && !isBroken;
    public Vector3 SphereCentre => transform.position + Vector3.up * (data != null ? data.visualHeightOffset : 0f);

    /// <summary>True when a point is inside any standing dome (flat distance).</summary>
    public static bool IsInsideAnyDome(Vector3 worldPos)
    {
        foreach (ProjectileDome dome in active)
        {
            if (dome != null && dome.IsUp && dome.Contains(worldPos)) return true;
        }
        return false;
    }

    /// <summary>The standing dome whose sphere contains a point in the air (3D), or null. Used for lobbed boulders.</summary>
    public static ProjectileDome FindDomeAt(Vector3 worldPos)
    {
        foreach (ProjectileDome dome in active)
        {
            if (dome == null || !dome.IsUp) continue;
            if ((worldPos - dome.SphereCentre).sqrMagnitude <= dome.Radius * dome.Radius) return dome;
        }
        return null;
    }

    /// <summary>
    ///     True if a dome caught a hit on this enemy this frame. Status appliers
    ///     (<see cref="EnemyStatusManager.ApplyStatus" />) check it, since towers apply fire/ice/lightning
    ///     straight after <c>ReceiveDamage</c>.
    /// </summary>
    public static bool BlockedThisFrame(Health target)
    {
        return target != null && blockedFrame.TryGetValue(target, out int frame) && frame == Time.frameCount;
    }

    /// <summary>
    ///     The blocking rule, kept pure so the benchmark can check it: a projectile the player owns
    ///     is caught if a tower fired it, or if the hero fired it from outside the dome.
    /// </summary>
    public static bool ShouldBlock(Damage damage, bool heroInsideDome)
    {
        if (damage == null || !damage.isProjectile || !damage.IsPlayerOwned) return false;
        return damage.isDefenseDamage || !heroInsideDome;
    }

    public bool Contains(Vector3 worldPos)
    {
        Vector3 offset = worldPos - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= Radius * Radius;
    }

    private void OnValidate()
    {
        if (health == null) health = GetComponent<Health>();
    }

    private void Start()
    {
        if (data == null)
        {
            Debug.LogError($"{name}: ProjectileDome.data (ProjectileDomeSO) is not assigned.", this);
            anyError = true;
        }
        if (health == null)
        {
            Debug.LogError($"{name}: ProjectileDome.health is not assigned or found.", this);
            anyError = true;
        }
        if (hitNumberPrefab == null)
        {
            Debug.LogError($"{name}: ProjectileDome.hitNumberPrefab is not assigned; dome hits won't show a number.", this);
        }
        if (anyError) return;

        if (data.domeVisualPrefab == null)
        {
            // Still blocks; it's just invisible, which players will read as a bug.
            Debug.LogError("[ProjectileDome] ProjectileDomeSO.domeVisualPrefab is not assigned.", data);
        }
        else
        {
            visual = Instantiate(data.domeVisualPrefab, transform);
            visual.transform.localPosition = new Vector3(0f, data.visualHeightOffset, 0f);
            SetVisualScale(1f);
            foreach (Collider col in visual.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        }

        int mask = enemyLayers.value;
        if (mask == ~0 || mask == 0)
        {
            mask = LayerMask.GetMask("Enemy");
            if (mask == 0) mask = 1 << 7;
        }
        enemyLayers = mask;

        currentHealth = data.domeHealth;
        health.OnDied += Collapse;
        active.Add(this);
    }

    private void Update()
    {
        if (anyError || isDown) return;

        if (isBroken)
        {
            if (Time.time >= rebuildAtTime) Rebuild();
            return;
        }

        if (Time.time < nextScanTime) return;
        nextScanTime = Time.time + data.rescanInterval;
        Rescan();
    }

    private void Rescan()
    {
        seenThisScan.Clear();
        Collider[] hits = Physics.OverlapSphere(transform.position, data.radius, enemyLayers, QueryTriggerInteraction.Collide);
        foreach (Collider hit in hits)
        {
            if (hit == null) continue;
            Health h = hit.GetComponentInParent<Health>();
            if (h == null || h.IsDead || seenThisScan.Contains(h)) continue;
            if (Player.Instance != null && h.transform.root == Player.Instance.transform.root) continue;
            if (!Contains(h.transform.position)) continue;

            seenThisScan.Add(h);
            if (!covered.ContainsKey(h)) Cover(h);
        }

        leaving.Clear();
        foreach (Health h in covered.Keys)
        {
            if (h == null || h.IsDead || !seenThisScan.Contains(h)) leaving.Add(h);
        }
        foreach (Health h in leaving) Uncover(h);
    }

    private void Cover(Health target)
    {
        Func<Damage, bool> handler = damage => HandleTryBlockDamage(target, damage);
        covered[target] = handler;
        target.TryBlockDamage += handler;
    }

    private void Uncover(Health target)
    {
        if (!covered.TryGetValue(target, out Func<Damage, bool> handler)) return;
        covered.Remove(target);
        if (target != null) target.TryBlockDamage -= handler;
    }

    private void UncoverAll()
    {
        leaving.Clear();
        leaving.AddRange(covered.Keys);
        foreach (Health h in leaving) Uncover(h);
    }

    private bool HandleTryBlockDamage(Health target, Damage damage)
    {
        if (!IsUp || target == null) return false;
        if (!Contains(target.transform.position)) return false;

        Player player = Player.Instance;
        bool heroInside = player != null && Contains(player.transform.position);
        if (!ShouldBlock(damage, heroInside)) return false;

        if (blockedFrame.Count > 256) blockedFrame.Clear(); // dead enemies never get cleaned up otherwise
        blockedFrame[target] = Time.frameCount;
        AbsorbHit(damage.value, SurfacePointToward(target.transform.position, damage.direction));
        return true;
    }

    /// <summary>
    ///     Where a projectile flying along <paramref name="flightDir" /> toward <paramref name="target" />
    ///     would have met the dome's shell. Falls back to the side facing the player.
    /// </summary>
    private Vector3 SurfacePointToward(Vector3 target, Vector3 flightDir)
    {
        Vector3 back = -flightDir;
        if (back.sqrMagnitude < 0.0001f && Player.Instance != null) back = Player.Instance.transform.position - target;
        back.y = 0f;
        if (back.sqrMagnitude < 0.0001f) back = Vector3.forward;
        return SphereCentre + back.normalized * data.radius + Vector3.up * 1.2f;
    }

    /// <summary>
    ///     A hit landing on the dome: soaks the damage, pops a purple number at <paramref name="hitPoint" />
    ///     and pulses the shell. Breaks the dome at 0. Called by the Health hook and by boulders.
    /// </summary>
    public void AbsorbHit(float damage, Vector3 hitPoint)
    {
        if (!IsUp) return;

        currentHealth -= damage;
        if (hitNumberPrefab != null) hitNumberPrefab.Spawn(hitPoint, damage).SetColor(data.hitNumberColor);
        if (hitFeedback != null) hitFeedback.PlayFeedbacks(hitPoint);

        if (currentHealth <= 0f)
        {
            Break();
            return;
        }

        if (visual != null)
        {
            LeanTween.cancel(visual);
            SetVisualScale(1.06f);
            LeanTween.scale(visual, Vector3.one * data.radius * 2f, 0.2f).setEaseOutQuad();
        }
    }

    private void Break()
    {
        isBroken = true;
        rebuildAtTime = Time.time + data.rebuildCooldown;
        UncoverAll();
        if (visual != null)
        {
            LeanTween.cancel(visual);
            visual.SetActive(false);
        }
        if (breakFeedback != null) breakFeedback.PlayFeedbacks(SphereCentre);
    }

    private void Rebuild()
    {
        isBroken = false;
        currentHealth = data.domeHealth;
        nextScanTime = 0f;
        if (visual != null)
        {
            visual.SetActive(true);
            SetVisualScale(1f);
        }
        if (rebuildFeedback != null) rebuildFeedback.PlayFeedbacks(SphereCentre);
    }

    private void SetVisualScale(float punch)
    {
        if (visual != null) visual.transform.localScale = Vector3.one * (data.radius * 2f * punch);
    }

    /// <summary>Drops the dome for good: unhooks every covered enemy and removes the visual. Runs on the Warden's death.</summary>
    public void Collapse()
    {
        if (isDown) return;
        isDown = true;
        active.Remove(this);
        UncoverAll();

        if (visual != null)
        {
            LeanTween.cancel(visual);
            Destroy(visual);
            visual = null;
        }
    }

    private void OnDestroy()
    {
        if (health != null) health.OnDied -= Collapse;
        Collapse();
    }
}
