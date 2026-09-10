using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

public static class TestDraftWeaponCharges
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Bladehold/Tests/Draft Weapon Charges (Play Mode)")]
    public static void Run()
    {
        if (!Application.isPlaying || Player.Instance == null)
            throw new InvalidOperationException("Enter Play mode with the Player prefab before running draft charge tests.");

        var weapons = PlayerWeaponManager.GetInstance();
        var stats = Player.Instance.Stats;
        string originalMelee = weapons.CurrentMeleeId;
        string originalRanged = weapons.CurrentRangedId;
        var originalCharges = new Dictionary<string, string>(RunSession.ElementalSlots);
        var existingObjects = new HashSet<GameObject>(Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None));
        var dummies = new List<GameObject>();
        var randomState = UnityEngine.Random.state;
        int checks = 0;

        try
        {
            foreach (var slot in weapons.meleeWeapons)
            {
                weapons.EquipMelee(slot.definition.id);
                checks += CheckHighlight(slot.elementalHighlight, "SLOT_MELEE");
            }
            foreach (var slot in weapons.rangedWeapons)
            {
                weapons.EquipRanged(slot.definition.id);
                checks += CheckHighlight(slot.elementalHighlight, "SLOT_RANGED");
            }
            weapons.EquipMelee(originalMelee);

            foreach (string element in new[] { "FIRE", "ICE", "LIGHTNING", "" })
            {
                var targets = new Health[3];
                var received = new Damage[3];
                for (int i = 0; i < targets.Length; i++)
                {
                    var go = new GameObject("DraftChargeTestTarget");
                    dummies.Add(go);
                    go.transform.position = new Vector3(10000f + i, 1000f, 10000f);
                    go.layer = LayerMask.NameToLayer("Enemy");
                    go.AddComponent<BoxCollider>();
                    if (i == 1) go.AddComponent<SphereCollider>();
                    go.AddComponent<NavMeshAgent>().enabled = false;
                    targets[i] = go.AddComponent<Health>();
                    targets[i].SetMaxHealth(10000f);
                    int index = i;
                    targets[i].OnDamaged += damage => received[index] = damage;
                }
                Physics.SyncTransforms();
                if (element.Length == 0) RunSession.ClearElementalSlot("SLOT_MELEE");
                else RunSession.SetElementalSlot("SLOT_MELEE", element);

                Damage delivered = null;
                Action<IDamageable, Damage, Vector3> capture = (target, damage, point) =>
                {
                    if (target == targets[0]) delivered = damage;
                };
                DamageTrigger trigger = weapons.ActiveMeleeTrigger;
                trigger.Activate();
                trigger.OnHit += capture;
                try
                {
                    typeof(DamageTrigger).GetMethod("TryHitTarget", PrivateInstance).Invoke(trigger,
                        new object[] { targets[0].GetComponent<Collider>(), 99, targets[0].transform.position });
                }
                finally
                {
                    trigger.OnHit -= capture;
                    trigger.Deactivate();
                }

                Require(delivered != null && delivered.value > 0f, "Weapon hit did not land.");
                Require(string.Equals(delivered.elementId ?? "", element, StringComparison.OrdinalIgnoreCase), "Wrong damage element.");
                if (element == "FIRE" || element == "LIGHTNING")
                {
                    StatType percent = element == "FIRE" ? StatType.WeaponFireExplosionDamagePercent : StatType.WeaponLightningDamagePercent;
                    for (int i = 1; i < targets.Length; i++)
                    {
                        Require(received[i] != null, $"{element} did not reach target {i}.");
                        float expected = delivered.value * stats.GetValue(percent);
                        if (received[i].isCritical) expected *= stats.GetValue(StatType.CritMultiplier);
                        Require(Mathf.Abs(10000f - targets[i].CurrentHealth - expected) < 0.01f,
                            $"{element} damage mismatch or duplicate collider hit.");
                    }
                }
                else if (element == "ICE")
                {
                    var slow = targets[0].GetComponent<SlowStatus>();
                    Require(slow != null && slow.IsSlowed, "Ice did not chill.");
                    stats.AddModifier(StatType.IceDeepFreezeUnlocked, ModifierKind.Flat, 1f);
                    try
                    {
                        targets[0].ReceiveDamage(new Damage { value = 1f, type = DamageType.sharp, isPlayerDamage = true, elementId = "ICE" });
                        Require(slow.CurrentSlowFraction == 1f && targets[0].GetComponent<EnemyStatusManager>().HasStatus("Frozen"), "Deep Freeze did not freeze.");
                    }
                    finally { stats.RemoveModifier(StatType.IceDeepFreezeUnlocked, ModifierKind.Flat, 1f); }
                }
                else
                {
                    Require(received[1] == null && received[2] == null && targets[0].GetComponent<EnemyStatusManager>() == null,
                        "Cleared charge still applies elemental effects.");
                }
                checks++;
                foreach (var target in targets) Object.DestroyImmediate(target.gameObject);
            }
            Debug.Log($"[DraftWeaponCharges] PASS: {checks} highlight/clear and combat cases.");
        }
        finally
        {
            foreach (var dummy in dummies) if (dummy != null) Object.DestroyImmediate(dummy);
            // Only clean up test-created effects at the isolated test location, not existing gameplay objects.
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (go != null && !existingObjects.Contains(go) && go.transform.parent == null
                    && Vector3.Distance(go.transform.position, new Vector3(10000f, 1000f, 10000f)) < 20f)
                    Object.DestroyImmediate(go);
            foreach (string slot in RunSession.ElementalSlots.Keys.ToArray()) RunSession.ClearElementalSlot(slot);
            foreach (var pair in originalCharges) RunSession.SetElementalSlot(pair.Key, pair.Value);
            weapons.EquipMelee(originalMelee);
            weapons.EquipRanged(originalRanged);
            UnityEngine.Random.state = randomState;
        }
    }

    private static int CheckHighlight(HighlightPlus.HighlightEffect highlight, string slot)
    {
        Require(highlight != null, $"Missing {slot} highlight.");
        foreach (string element in new[] { "FIRE", "ICE", "LIGHTNING" })
        {
            RunSession.SetElementalSlot(slot, element);
            string profile = element == "ICE" ? "Frost Weapon HPP" : element == "FIRE" ? "Fire Weapon HPP" : "Lightning Weapon HPP";
            Require(highlight.enabled && highlight.highlighted && highlight.profile.name == profile, $"Wrong {slot} {element} highlight.");
        }
        RunSession.ClearElementalSlot(slot);
        Require(!highlight.enabled && !highlight.highlighted, $"{slot} highlight remains enabled after clear.");
        return 4;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[DraftWeaponCharges] " + message);
    }
}
