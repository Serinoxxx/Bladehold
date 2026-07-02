using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
///     One enemy type parsed from the roster CSV. Nullable fields are optional overrides — blank in
///     the CSV means "keep the value from the prefab's own ScriptableObject" (HealthSO / AIAttackSO /
///     EnemySO / AIMovementSO stay the source of base truth; the CSV only overrides what the designer
///     filled in, and the shared SO assets are never mutated).
/// </summary>
public class EnemyDefinition
{
    public string id;
    public string displayName;

    /// <summary>Overrides <see cref="HealthSO.maxHealth" />. Blank = prefab default.</summary>
    public float? health;

    /// <summary>Overrides <see cref="AIAttackSO.damage" />. Blank = prefab default.</summary>
    public float? damage;

    /// <summary>Override <see cref="EnemySO.minCoinDrop" />/<see cref="EnemySO.maxCoinDrop" />. Blank = prefab default; filling only one uses it for both.</summary>
    public int? minGold;
    public int? maxGold;

    /// <summary>Overrides <see cref="AIMovementSO.speed" /> (the NavMeshAgent speed). Blank = prefab default.</summary>
    public float? speed;

    /// <summary>Multiplier on the prefab's authored transform scale (and NavMeshAgent radius/height). Blank = 1.</summary>
    public float scale = 1f;

    /// <summary>First wave (1-based) this type can appear on. Blank = 1.</summary>
    public int unlockWave = 1;

    /// <summary>Per-spawn roll chance once unlocked, stored 0..1 (authored as a percent in the CSV: 10 = 10%). Ignored for the first (fallback) row, which spawns whenever no other type wins its roll.</summary>
    public float spawnChance = 0f;

    /// <summary>Guaranteed spawns of this type per wave once unlocked (spawned before any chance rolls, still respecting maxConcurrent). 0 or blank = no guarantee.</summary>
    public int minSpawn = 0;

    /// <summary>Maximum of this type alive at once. 0 or blank = unlimited.</summary>
    public int maxConcurrent = 0;
}

/// <summary>
///     The enemy roster, authored as a CSV and parsed into <see cref="EnemyDefinition" />s — the same
///     designer-edits-a-spreadsheet pattern as <see cref="SkillTreeSO" />. The CSV is the balance sheet
///     for every enemy type; <see cref="WaveSpawner" /> maps each row's id to a prefab and applies the
///     row's overrides to each spawned instance.
///
///     CSV columns (one type per row):
///     <c>id, displayName, health, damage, minGold, maxGold, speed, scale, unlockWave, spawnChance, minSpawn, maxConcurrent</c>
///     <list type="bullet">
///         <item>The <b>first row is the fallback type</b> (spawned when no other type wins its
///         spawn-chance roll or all are at their concurrent cap), so it is effectively unlimited and
///         its <c>spawnChance</c> is ignored.</item>
///         <item><c>health</c>/<c>damage</c>/<c>minGold</c>/<c>maxGold</c>/<c>speed</c> blank → keep the
///         prefab's ScriptableObject value.</item>
///         <item><c>spawnChance</c> is a <b>percent</b> (10 = 10%). Each spawn slot first fills any type
///         still owed its <c>minSpawn</c> guarantee this wave, then checks the remaining rows in CSV
///         order; the first unlocked, under-cap type to win its roll is spawned.</item>
///     </list>
/// </summary>
[CreateAssetMenu(fileName = "EnemyRosterSO", menuName = "Scriptable Objects/EnemyRosterSO")]
public class EnemyRosterSO : ScriptableObject
{
    [Tooltip("CSV defining the enemy roster. Edit in a spreadsheet; Unity reimports automatically.")]
    [SerializeField] private TextAsset csv;

    [Tooltip("Skip the first CSV row as a header.")]
    [SerializeField] private bool hasHeaderRow = true;

    [NonSerialized] private List<EnemyDefinition> enemies;

    private const int ColumnCount = 12;

    /// <summary>All enemy types in CSV order (index 0 = the fallback type), parsed lazily.</summary>
    public IReadOnlyList<EnemyDefinition> Enemies
    {
        get
        {
            EnsureParsed();
            return enemies;
        }
    }

    /// <summary>Forces a re-parse (e.g. after editing the CSV at runtime). Normally not needed.</summary>
    public void Reload()
    {
        enemies = null;
        EnsureParsed();
    }

    private void EnsureParsed()
    {
        if (enemies != null)
        {
            return;
        }

        enemies = new List<EnemyDefinition>();

        if (csv == null)
        {
            Debug.LogError($"EnemyRosterSO '{name}' has no CSV assigned.");
            return;
        }

        var seenIds = new HashSet<string>();
        string[] lines = csv.text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (i == 0 && hasHeaderRow)
            {
                continue;
            }

            string line = lines[i].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            EnemyDefinition def = ParseRow(line, i + 1);
            if (def == null)
            {
                continue;
            }

            if (!seenIds.Add(def.id))
            {
                Debug.LogError($"EnemyRosterSO '{name}': duplicate enemy id '{def.id}' on line {i + 1}; ignoring the duplicate.");
                continue;
            }

            enemies.Add(def);
        }
    }

    private EnemyDefinition ParseRow(string line, int lineNumber)
    {
        List<string> f = CsvUtil.SplitLine(line);
        if (f.Count < ColumnCount)
        {
            Debug.LogError($"EnemyRosterSO '{name}': line {lineNumber} has {f.Count} columns, expected at least {ColumnCount}. Skipping.");
            return null;
        }

        string id = f[0].Trim();
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogError($"EnemyRosterSO '{name}': line {lineNumber} has an empty id. Skipping.");
            return null;
        }

        var def = new EnemyDefinition
        {
            id = id,
            displayName = f[1].Trim(),
            health = ParseOptionalFloat(f[2], lineNumber, "health"),
            damage = ParseOptionalFloat(f[3], lineNumber, "damage"),
            minGold = ParseOptionalInt(f[4], lineNumber, "minGold"),
            maxGold = ParseOptionalInt(f[5], lineNumber, "maxGold"),
            speed = ParseOptionalFloat(f[6], lineNumber, "speed"),
            scale = ParseOptionalFloat(f[7], lineNumber, "scale") ?? 1f,
            unlockWave = ParseOptionalInt(f[8], lineNumber, "unlockWave") ?? 1,
            spawnChance = ParseOptionalFloat(f[9], lineNumber, "spawnChance") ?? 0f,
            minSpawn = ParseOptionalInt(f[10], lineNumber, "minSpawn") ?? 0,
            maxConcurrent = ParseOptionalInt(f[11], lineNumber, "maxConcurrent") ?? 0,
        };

        // Filling only one of the gold columns means a fixed drop of that amount.
        if (def.minGold.HasValue != def.maxGold.HasValue)
        {
            def.minGold = def.maxGold = def.minGold ?? def.maxGold;
        }
        if (def.minGold.HasValue && def.maxGold.Value < def.minGold.Value)
        {
            Debug.LogError($"EnemyRosterSO '{name}': line {lineNumber} has maxGold < minGold. Using minGold for both.");
            def.maxGold = def.minGold;
        }

        // Authored as a percent (10 = 10%); stored as a 0..1 probability.
        def.spawnChance = Mathf.Clamp01(def.spawnChance / 100f);
        def.unlockWave = Mathf.Max(1, def.unlockWave);
        def.minSpawn = Mathf.Max(0, def.minSpawn);
        def.maxConcurrent = Mathf.Max(0, def.maxConcurrent);
        if (def.scale <= 0f)
        {
            Debug.LogError($"EnemyRosterSO '{name}': line {lineNumber} has non-positive scale. Using 1.");
            def.scale = 1f;
        }

        return def;
    }

    private float? ParseOptionalFloat(string s, int lineNumber, string field)
    {
        s = s.Trim();
        if (string.IsNullOrEmpty(s))
        {
            return null;
        }
        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
        {
            return v;
        }
        Debug.LogError($"EnemyRosterSO '{name}': line {lineNumber} has invalid {field} '{s}'. Treating as blank.");
        return null;
    }

    private int? ParseOptionalInt(string s, int lineNumber, string field)
    {
        s = s.Trim();
        if (string.IsNullOrEmpty(s))
        {
            return null;
        }
        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
        {
            return v;
        }
        Debug.LogError($"EnemyRosterSO '{name}': line {lineNumber} has invalid {field} '{s}'. Treating as blank.");
        return null;
    }
}
