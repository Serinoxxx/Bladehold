using UnityEditor;
using UnityEngine;
using System.Collections;
using Unity.EditorCoroutines.Editor;

public class TestDodgeMechanics
{
    [MenuItem("Bladehold/Test Dodge Mechanics")]
    public static void RunTest()
    {
        EditorCoroutineUtility.StartCoroutineOwnerless(TestCoroutine());
    }

    private static IEnumerator TestCoroutine()
    {
        Debug.Log("[DodgeTest] Starting PlayMode for Dodge Test...");
        EditorApplication.isPlaying = true;

        while (!EditorApplication.isPlaying) yield return null;
        yield return new WaitForSeconds(2.5f);

        Player player = Player.Instance;
        if (player == null)
        {
            Debug.LogError("[DodgeTest] FAIL: Player.Instance is null!");
            EditorApplication.isPlaying = false;
            yield break;
        }

        PlayerDodge dodgeComp = player.GetComponent<PlayerDodge>();
        if (dodgeComp == null)
        {
            Debug.LogError("[DodgeTest] FAIL: PlayerDodge component missing on Player!");
            EditorApplication.isPlaying = false;
            yield break;
        }

        PlayerDodgeUI dodgeUI = Object.FindFirstObjectByType<PlayerDodgeUI>();
        if (dodgeUI == null)
        {
            Debug.LogWarning("[DodgeTest] WARNING: PlayerDodgeUI component missing in HUD scene!");
        }
        else
        {
            Debug.Log("[DodgeTest] PASS: PlayerDodgeUI found in scene HUD.");
        }

        // Test 1: Unlock & Trigger Dodge
        player.Stats.SetBase(StatType.DodgeUnlocked, 1f);
        player.Stats.SetBase(StatType.DodgeCooldown, 5f);
        player.Stats.SetBase(StatType.DodgeDistance, 3f);
        player.Stats.SetBase(StatType.DodgeDamageMultiplier, 0.5f);
        player.Stats.SetBase(StatType.DodgeKnockbackForce, 6f);
        player.Stats.SetBase(StatType.DodgeChainCooldownReduction, 1.5f);

        Debug.Log("[DodgeTest] Testing Dodge Execution...");
        var performMethod = dodgeComp.GetType().GetMethod("PerformDodge", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (performMethod != null)
        {
            dodgeComp.StartCoroutine((IEnumerator)performMethod.Invoke(dodgeComp, null));
            yield return new WaitForSeconds(0.3f);
            Debug.Log("[DodgeTest] PASS: Dodge coroutine started successfully!");
        }
        else
        {
            Debug.LogError("[DodgeTest] FAIL: Could not find PerformDodge method!");
        }

        yield return new WaitForSeconds(1.0f);

        Debug.Log("[DodgeTest] All automated checks completed!");
        EditorApplication.isPlaying = false;
    }
}
