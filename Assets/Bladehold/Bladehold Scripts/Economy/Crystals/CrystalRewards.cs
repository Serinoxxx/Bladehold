using DamageNumbersPro;
using UnityEngine;

/// <summary>
///     Pays out elemental crystals in a battle scene (plan 17): elite/special, siege and captain kills
///     drop a <see cref="CrystalPickup" />, and every resolved wave banks crystals straight into
///     <see cref="RunSession" /> (more when its objective succeeded). Elements are rolled through
///     <see cref="SceneCrystalBias" />. Listens to <see cref="GameLoopManager" />; the generator puts it
///     beside <see cref="DefenseSceneRules" />.
/// </summary>
public class CrystalRewards : MonoBehaviour
{
    [SerializeField] private CrystalConfigSO config;
    [Tooltip("Popup over the player for wave crystal rewards.")]
    [SerializeField] private DamageNumber rewardPopup;

    private GameLoopManager loop;
    private bool anyError;

    private void Start()
    {
        if (config == null) { Debug.LogError($"[CrystalRewards] {name}: config is not assigned.", this); anyError = true; }
        if (config != null && config.pickupPrefab == null) Debug.LogError($"[CrystalRewards] {name}: CrystalConfigSO.pickupPrefab is not assigned.", this);
        if (rewardPopup == null) Debug.LogError($"[CrystalRewards] {name}: rewardPopup is not assigned.", this);
        loop = GameLoopManager.Instance;
        if (loop == null) { Debug.LogError($"[CrystalRewards] {name}: no GameLoopManager in the scene.", this); anyError = true; }
        if (anyError) return;

        loop.OnEnemyKilledEvent += HandleEnemyKilled;
        loop.OnWaveResolved += HandleWaveResolved;
    }

    private void OnDestroy()
    {
        if (loop != null)
        {
            loop.OnEnemyKilledEvent -= HandleEnemyKilled;
            loop.OnWaveResolved -= HandleWaveResolved;
        }
    }

    public void SetConfig(CrystalConfigSO value)
    {
        config = value;
    }

    private void HandleEnemyKilled(Health enemy)
    {
        if (enemy == null || EnemyPrewarmer.IsRehearsing) return;
        int amount = DropAmountFor(enemy.gameObject);
        if (amount <= 0) return;
        CrystalPickup.Spawn(config.pickupPrefab, enemy.transform.position, SceneCrystalBias.Roll(), amount);
    }

    /// <summary>How many crystals this kill drops (0 for fodder or a failed elite roll).</summary>
    public int DropAmountFor(GameObject enemy)
    {
        if (enemy.GetComponentInParent<ICaptain>() != null) return config.captainDropAmount;
        if (WallNavCost.IsSiege(enemy)) return config.siegeDropAmount;

        Enemy e = enemy.GetComponentInParent<Enemy>();
        string id = e != null ? e.RosterId : null;
        if (string.IsNullOrEmpty(id) || config.fodderIds.Contains(id)) return 0;
        return Random.value < config.eliteDropChance ? config.eliteDropAmount : 0;
    }

    private void HandleWaveResolved(int wave, bool success, WaveCard card)
    {
        int amount = config.waveClearCrystals;
        if (success && card != null && card.objective != null) amount += config.objectiveCrystals;
        if (amount <= 0) return;

        // One roll per crystal, so a 2-crystal reward can be mixed.
        var banked = new int[4];
        for (int i = 0; i < amount; i++)
        {
            StructureElement e = SceneCrystalBias.Roll();
            RunSession.AddCrystals(e, 1);
            banked[(int)e]++;
        }

        if (rewardPopup != null && Player.Instance != null)
        {
            var parts = new System.Collections.Generic.List<string>();
            foreach (StructureElement e in StructureElements.All)
            {
                if (banked[(int)e] > 0) parts.Add($"+{banked[(int)e]} {e.CrystalName()}");
            }
            rewardPopup.Spawn(Player.Instance.transform.position + Vector3.up * 2.6f, string.Join("  ", parts));
        }
    }
}
