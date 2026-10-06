using UnityEngine;

/// <summary>
///     Tunables for Captain Mogra Hexfang, the Bonecaller: a goblin shaman captain who fights at range with
///     ground rune patterns, slow hex bolts and bone totems, over three HP phases. Every damage number here
///     is before the tier multiplier (<see cref="BannerDifficultyHelper.GetStatMultiplier" />). Per-phase
///     arrays are indexed phase 1, 2, 3.
/// </summary>
[CreateAssetMenu(fileName = "CaptainMograSO", menuName = "Scriptable Objects/Enemies/Captain Mogra SO")]
public class CaptainMograSO : ScriptableObject
{
    [Header("Base Stats")]
    [Tooltip("Base maximum health before tier scaling.")]
    public float baseMaxHealth = 500f;

    [Tooltip("Base movement speed. He keeps his distance rather than chasing.")]
    public float baseMoveSpeed = 3.2f;

    [Tooltip("He walks towards the player until this close, then stands and casts.")]
    public float preferredRange = 9f;

    [Header("Phases")]
    [Tooltip("Health fraction at or below which phase 2 starts (totems).")]
    [Range(0f, 1f)] public float phase2HealthFraction = 0.66f;

    [Tooltip("Health fraction at or below which phase 3 starts (the Bonefire Ritual).")]
    [Range(0f, 1f)] public float phase3HealthFraction = 0.33f;

    [Tooltip("Seconds of the phase-change roar. He does nothing else while roaring (a free hit window).")]
    public float phaseRoarSeconds = 1.6f;

    [Tooltip("Seconds of recovery after each attack, per phase.")]
    public float[] recoveryByPhase = { 2.8f, 2.2f, 1.6f };

    [Header("Hex Bolts (fan of slow, non-homing orbs)")]
    [Tooltip("Bolts per volley, per phase.")]
    public int[] boltCountByPhase = { 3, 3, 5 };

    [Tooltip("Total fan angle in degrees. A volley hits at most once however many bolts touch you.")]
    public float boltFanDegrees = 44f;

    [Tooltip("Wind-up before the bolts fly (the cast animation).")]
    public float boltWindupSeconds = 0.55f;

    [Tooltip("Bolt travel speed in m/s. Slow enough to strafe out of.")]
    public float boltSpeed = 9f;

    [Tooltip("Damage per bolt.")]
    public float boltDamage = 8f;

    [Tooltip("Seconds before an unspent bolt fizzles.")]
    public float boltLifetime = 3.5f;

    [Tooltip("Volleys per bolt attack, per phase (0.35 s apart).")]
    public int[] boltVolleysByPhase = { 1, 2, 2 };

    [Header("Rune Eruption (ground patterns)")]
    [Tooltip("Seconds each rune glows before it erupts, per phase. The benchmark keeps this at or above minReadableTelegraphSeconds.")]
    public float[] runeTelegraphByPhase = { 1.3f, 1.1f, 0.95f };

    [Tooltip("Floor for any rune telegraph. Below this a pattern isn't readable.")]
    public float minReadableTelegraphSeconds = 0.9f;

    [Tooltip("Delay between successive steps of a sequenced pattern (inner ring then outer ring, spiral steps).")]
    public float runeStepSeconds = 0.4f;

    [Tooltip("Radius of each rune's blast.")]
    public float runeRadius = 1.6f;

    [Tooltip("Damage per rune (a player can only be hit once per rune).")]
    public float runeDamage = 12f;

    [Tooltip("Wind-up animation before the runes appear.")]
    public float runeWindupSeconds = 0.5f;

    [Tooltip("Patterns he picks from in each phase. Phase 1 uses the easy ones.")]
    public HexRunePattern[] phase1Patterns = { HexRunePattern.Rings, HexRunePattern.Cross };
    public HexRunePattern[] phase2Patterns = { HexRunePattern.Rings, HexRunePattern.Cross, HexRunePattern.Checkerboard };
    public HexRunePattern[] phase3Patterns = { HexRunePattern.Cross, HexRunePattern.Checkerboard, HexRunePattern.Spiral };

    [Header("Hex Step (punishes face-tanking)")]
    [Tooltip("The player counts as 'in his face' within this distance.")]
    public float hexStepTriggerRange = 3.5f;

    [Tooltip("Seconds the player must stay in range before he blinks.")]
    public float hexStepDwellSeconds = 2.5f;

    [Tooltip("How far he blinks away from the player.")]
    public float hexStepBlinkDistance = 8f;

    [Tooltip("Radius of the blast he leaves where he stood.")]
    public float hexStepBlastRadius = 3f;

    [Tooltip("Telegraph seconds before the left-behind blast goes off.")]
    public float hexStepBlastDelay = 0.8f;

    [Tooltip("Damage of the left-behind blast.")]
    public float hexStepBlastDamage = 10f;

    [Tooltip("Seconds between Hex Steps.")]
    public float hexStepCooldown = 6f;

    [Header("Bone Totems (phase 2+)")]
    [Tooltip("Totems raised per summon, per phase (phase 1 never summons).")]
    public int[] totemCountByPhase = { 0, 2, 3 };

    [Tooltip("Seconds of the summon animation.")]
    public float totemSummonSeconds = 1.2f;

    [Tooltip("Totems appear this far from him, spread evenly around him.")]
    public float totemPlacementRadius = 6f;

    [Tooltip("Base totem health before tier scaling. A few sword swings.")]
    public float totemBaseHealth = 45f;

    [Tooltip("Seconds between each totem's shockwave rings.")]
    public float totemPulseInterval = 4.5f;

    [Tooltip("Seconds each totem glows and hums before a pulse (the tell). Part of the pulse interval, not added to it.")]
    public float totemChargeSeconds = 1.4f;

    [Tooltip("Seconds before a new totem's first ring, so the player can react to it appearing.")]
    public float totemFirstPulseDelay = 2f;

    [Tooltip("Ring expansion speed in m/s.")]
    public float ringSpeed = 5f;

    [Tooltip("Rings fade at this radius.")]
    public float ringMaxRadius = 13f;

    [Tooltip("Thickness of the damaging band. Dash through it (i-frames) or stay outside its reach.")]
    public float ringWidth = 0.8f;

    [Tooltip("Ring damage (once per ring).")]
    public float ringDamage = 8f;

    [Tooltip("Seconds before he re-summons after all his totems are broken.")]
    public float totemResummonCooldown = 14f;

    [Tooltip("Breaking a totem backlashes: he's staggered this long.")]
    public float totemBacklashStaggerSeconds = 2.5f;

    [Header("Stagger")]
    [Tooltip("Damage taken multiplier while staggered (the payoff window).")]
    public float staggerDamageTakenMultiplier = 1.5f;

    [Header("Bonefire Ritual (phase 3)")]
    [Tooltip("Seconds between rituals (from the end of one to the start of the next).")]
    public float ritualCooldown = 22f;

    [Tooltip("Seconds of channel before it detonates.")]
    public float ritualChannelSeconds = 6f;

    [Tooltip("Damage needed to break the channel, as a fraction of his max HP.")]
    [Range(0f, 1f)] public float ritualBreakHealthFraction = 0.1f;

    [Tooltip("Stagger when the ritual is broken. The big payoff.")]
    public float ritualBrokenStaggerSeconds = 4f;

    [Tooltip("Radius of the detonation around him. Everything inside is hit unless standing in a safe circle.")]
    public float ritualBlastRadius = 16f;

    [Tooltip("Detonation damage.")]
    public float ritualBlastDamage = 25f;

    [Tooltip("Safe circles shown during the channel.")]
    public int ritualSafeCircleCount = 3;

    [Tooltip("Radius of each safe circle.")]
    public float ritualSafeCircleRadius = 2.2f;

    [Tooltip("Safe circles are placed between these distances from him.")]
    public float ritualSafeCircleMinDistance = 5f;
    public float ritualSafeCircleMaxDistance = 11f;

    [Header("Rewards (× tier reward multiplier)")]
    public int bonusGold = 60;
    public int bonusGoblinBlood = 4;
    [Tooltip("Arcane Cores dropped on death (spent to fire ultimates). Not multiplied by the tier reward.")]
    public CaptainArcaneCoreReward arcaneCores = new CaptainArcaneCoreReward();

    [Header("Spawned Prefabs")]
    [Tooltip("HexBolt projectile prefab.")]
    public GameObject boltPrefab;

    [Tooltip("HexRuneBlast prefab: a green ground telegraph that erupts after its delay. Used by runes and the Hex Step blast.")]
    public GameObject runeBlastPrefab;

    [Tooltip("BoneTotem prefab.")]
    public GameObject totemPrefab;

    [Tooltip("HexShockwaveRing prefab the totems emit.")]
    public GameObject shockwaveRingPrefab;

    [Tooltip("Ground marker for a ritual safe circle (scaled to its diameter).")]
    public GameObject ritualSafeCirclePrefab;

    [Tooltip("Ground marker for the ritual's danger area (scaled to its diameter).")]
    public GameObject ritualDangerPrefab;

    /// <summary>Clamps a phase number (1-3) into a per-phase array.</summary>
    public static T ByPhase<T>(T[] values, int phase, T fallback)
    {
        if (values == null || values.Length == 0) return fallback;
        return values[Mathf.Clamp(phase - 1, 0, values.Length - 1)];
    }

    public HexRunePattern[] PatternsForPhase(int phase) =>
        phase >= 3 ? phase3Patterns : phase == 2 ? phase2Patterns : phase1Patterns;

    /// <summary>The rune telegraph for a phase, never below <see cref="minReadableTelegraphSeconds" />.</summary>
    public float RuneTelegraphSeconds(int phase) =>
        Mathf.Max(minReadableTelegraphSeconds, ByPhase(runeTelegraphByPhase, phase, 1.2f));
}
