using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     The Dome Warden's dome: a bubble around the caster that stops projectiles hitting any enemy
///     inside it (the Warden included).
///     - Tower projectiles (arrows, ballista bolts, nets, Tesla bolts) are always blocked.
///     - The hero's projectiles (bow, thrown axe, wand) are blocked only while the hero is outside
///       the dome, so the answer is to run in.
///     - Melee, ground traps, zones and the Catapult's lobbed boulders aren't projectiles and get through.
///
///     Enemies inside are hooked through <see cref="Health.TryBlockDamage" /> on a short rescan, and
///     unhooked when they leave, die, or the Warden dies. Nets bypass Health, so
///     <see cref="NetThrowerDefense" /> asks <see cref="IsInsideAnyDome" /> directly.
/// </summary>
public class ProjectileDome : MonoBehaviour
{
    [SerializeField] private ProjectileDomeSO data;
    [SerializeField] private Health health;
    [Tooltip("Optional: played where a projectile is stopped (a glassy ping).")]
    [SerializeField] private MMF_Player blockFeedback;
    [SerializeField] private LayerMask enemyLayers = ~0;

    private static readonly List<ProjectileDome> active = new List<ProjectileDome>();

    private readonly Dictionary<Health, Func<Damage, bool>> covered = new Dictionary<Health, Func<Damage, bool>>();
    private readonly List<Health> leaving = new List<Health>();
    private readonly HashSet<Health> seenThisScan = new HashSet<Health>();
    private GameObject visual;
    private float nextScanTime;
    private bool isDown;
    private bool anyError;

    public float Radius => data != null ? data.radius : 0f;
    public bool IsUp => !isDown && !anyError;

    /// <summary>True when a point is inside any standing dome (flat distance).</summary>
    public static bool IsInsideAnyDome(Vector3 worldPos)
    {
        foreach (ProjectileDome dome in active)
        {
            if (dome != null && dome.IsUp && dome.Contains(worldPos)) return true;
        }
        return false;
    }

    /// <summary>
    ///     The blocking rule, kept pure so the benchmark can check it: a projectile the player owns
    ///     is blocked if a tower fired it, or if the hero fired it from outside the dome.
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

        health.OnDied += Collapse;
        active.Add(this);
    }

    private void Update()
    {
        if (anyError || isDown) return;
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

    private bool HandleTryBlockDamage(Health target, Damage damage)
    {
        if (!IsUp || target == null) return false;
        if (!Contains(target.transform.position)) return false;

        Player player = Player.Instance;
        bool heroInside = player != null && Contains(player.transform.position);
        if (!ShouldBlock(damage, heroInside)) return false;

        if (blockFeedback != null) blockFeedback.PlayFeedbacks(target.transform.position + Vector3.up);
        if (visual != null)
        {
            LeanTween.cancel(visual);
            SetVisualScale(1.06f);
            LeanTween.scale(visual, Vector3.one * data.radius * 2f, 0.2f).setEaseOutQuad();
        }
        return true;
    }

    private void SetVisualScale(float punch)
    {
        if (visual != null) visual.transform.localScale = Vector3.one * (data.radius * 2f * punch);
    }

    /// <summary>Drops the dome: unhooks every covered enemy and removes the visual. Runs on the Warden's death.</summary>
    public void Collapse()
    {
        if (isDown) return;
        isDown = true;
        active.Remove(this);

        leaving.Clear();
        leaving.AddRange(covered.Keys);
        foreach (Health h in leaving) Uncover(h);

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
