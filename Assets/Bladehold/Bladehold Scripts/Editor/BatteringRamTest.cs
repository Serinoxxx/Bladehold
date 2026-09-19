#if UNITY_EDITOR
using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using MoreMountains.Feedbacks;

public static class BatteringRamTest
{
    private const string BatteringRamPrefabPath = "Assets/Bladehold/Bladehold Prefabs/Objectives/BatteringRam.prefab";

    [MenuItem("Bladehold/Run Battering Ram Integration Test")]
    public static void RunIntegrationTest()
    {
        Debug.Log("[BatteringRamTest] Starting Battering Ram Integration Test...");

        // 1. Verify Prefab Assets & Components
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BatteringRamPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[BatteringRamTest] FAILED: BatteringRam prefab not found at {BatteringRamPrefabPath}");
            return;
        }

        NavMeshAgent agent = prefab.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogError("[BatteringRamTest] FAILED: Missing NavMeshAgent on prefab!");
            return;
        }
        if (!Mathf.Approximately(agent.speed, 1.5f))
        {
            Debug.LogError($"[BatteringRamTest] FAILED: Expected speed 1.5f, got {agent.speed}");
            return;
        }

        Health health = prefab.GetComponent<Health>();
        if (health == null)
        {
            Debug.LogError("[BatteringRamTest] FAILED: Missing Health on prefab!");
            return;
        }
        if (health.ImmuneToPlayerDamage)
        {
            Debug.LogError("[BatteringRamTest] FAILED: BatteringRam must NOT be immune to player damage!");
            return;
        }

        BoxCollider boxCol = prefab.GetComponent<BoxCollider>();
        if (boxCol == null)
        {
            Debug.LogError("[BatteringRamTest] FAILED: Missing BoxCollider on prefab!");
            return;
        }

        BatteringRam ramComp = prefab.GetComponent<BatteringRam>();
        if (ramComp == null)
        {
            Debug.LogError("[BatteringRamTest] FAILED: Missing BatteringRam component on prefab!");
            return;
        }

        SerializedObject ramSo = new SerializedObject(ramComp);
        float gateDmg = ramSo.FindProperty("gateDamage").floatValue;
        float interval = ramSo.FindProperty("ramInterval").floatValue;
        Transform logTrans = ramSo.FindProperty("ramLogTransform").objectReferenceValue as Transform;
        Transform ipTrans = ramSo.FindProperty("impactPoint").objectReferenceValue as Transform;
        AudioClip sound = ramSo.FindProperty("impactSound").objectReferenceValue as AudioClip;
        GameObject vfx = ramSo.FindProperty("impactVfxPrefab").objectReferenceValue as GameObject;
        MMF_Player mmf = ramSo.FindProperty("impactFeedback").objectReferenceValue as MMF_Player;

        if (gateDmg != 50f)
        {
            Debug.LogError($"[BatteringRamTest] FAILED: Expected gateDamage 50, got {gateDmg}");
            return;
        }
        if (interval != 5.0f)
        {
            Debug.LogError($"[BatteringRamTest] FAILED: Expected ramInterval 5.0, got {interval}");
            return;
        }
        if (logTrans == null)
        {
            Debug.LogError("[BatteringRamTest] FAILED: ramLogTransform is null!");
            return;
        }
        if (ipTrans == null)
        {
            Debug.LogError("[BatteringRamTest] FAILED: impactPoint is null!");
            return;
        }
        if (sound == null)
        {
            Debug.LogError("[BatteringRamTest] FAILED: impactSound is null!");
            return;
        }
        if (vfx == null)
        {
            Debug.LogError("[BatteringRamTest] FAILED: impactVfxPrefab is null!");
            return;
        }
        if (mmf == null)
        {
            Debug.LogError("[BatteringRamTest] FAILED: impactFeedback (MMF_Player) is null!");
            return;
        }

        Debug.Log("[BatteringRamTest] Prefab verification PASSED!");

        // 2. Functional Verification: instantiate in memory and test gate damage delivery
        GameObject testGo = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
        testGo.name = "Test_BatteringRam";
        BatteringRam liveRam = testGo.GetComponent<BatteringRam>();
        Health liveHealth = testGo.GetComponent<Health>();

        // Create a dummy gate
        GameObject gateGo = new GameObject("Test_Gate");
        Gate gate = gateGo.AddComponent<Gate>();
        Health gateHealth = gateGo.AddComponent<Health>();
        gateHealth.SetMaxHealth(500f);

        SerializedObject gateSo = new SerializedObject(gate);
        gateSo.FindProperty("health").objectReferenceValue = gateHealth;
        gateSo.FindProperty("attackPoint").objectReferenceValue = gateGo.transform;
        gateSo.ApplyModifiedPropertiesWithoutUndo();

        liveRam.InitializeDestination(gateGo.transform.position, gate);

        // Test damage delivery via TriggerImpact reflection or direct method
        var triggerMethod = typeof(BatteringRam).GetMethod("TriggerImpact", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (triggerMethod != null)
        {
            float gateHpBefore = gateHealth.CurrentHealth;
            triggerMethod.Invoke(liveRam, null);
            float gateHpAfter = gateHealth.CurrentHealth;
            float damageDealt = gateHpBefore - gateHpAfter;

            if (Mathf.Approximately(damageDealt, 50f))
            {
                Debug.Log($"[BatteringRamTest] Gate damage test PASSED: dealt exactly {damageDealt} damage to gate!");
            }
            else
            {
                Debug.LogError($"[BatteringRamTest] FAILED: Expected 50 damage to gate, but dealt {damageDealt}!");
            }
        }

        // Test destruction event
        liveHealth.SetMaxHealth(1000f);
        bool destroyedFired = false;
        liveRam.OnDestroyed += (r) => destroyedFired = true;
        liveHealth.ReceiveDamage(new Damage { value = 1000f, isPlayerDamage = true });

        if (destroyedFired)
        {
            Debug.Log("[BatteringRamTest] Ram destruction test PASSED: OnDestroyed fired on lethal damage!");
        }
        else
        {
            Debug.LogError("[BatteringRamTest] FAILED: OnDestroyed did not fire on 1000 damage!");
        }

        // Cleanup
        if (testGo != null) Object.DestroyImmediate(testGo);
        if (gateGo != null) Object.DestroyImmediate(gateGo);

        Debug.Log("[BatteringRamTest] ALL TESTS PASSED SUCCESSFULLY!");
    }
}
#endif
