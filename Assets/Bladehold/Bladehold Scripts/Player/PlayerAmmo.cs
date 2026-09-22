using System;
using UnityEngine;

/// <summary>
///     Manages the player's ammunition pool for ranged weapons (Bow, Thrown Axe, Wand).
///     Registers <see cref="StatType.MaxAmmo" /> (base 20) with <see cref="PlayerStats" />,
///     syncs the live count with <see cref="RunSession.CurrentAmmo" /> across scenes, and
///     raises events when ammo is consumed or gathered.
/// </summary>
public class PlayerAmmo : MonoBehaviour
{
    public static PlayerAmmo Instance { get; private set; }

    [Header("Audio Feedback (Optional)")]
    [Tooltip("Optional SFX played when trying to fire with 0 ammo.")]
    [SerializeField] private AudioClip outOfAmmoClip;
    [Tooltip("Optional SFX played when picking up ammo.")]
    [SerializeField] private AudioClip pickupAmmoClip;

    private PlayerStats stats;

    /// <summary>Raised whenever ammo count changes on this player instance: (currentAmmo, maxAmmo).</summary>
    public event Action<int, int> OnAmmoChanged;

    /// <summary>Global event raised whenever any player's ammo count changes: (currentAmmo, maxAmmo).</summary>
    public static event Action<int, int> OnAnyAmmoChanged;

    public int MaxAmmo
    {
        get
        {
            if (stats != null)
            {
                float val = stats.GetValue(StatType.MaxAmmo);
                if (val > 0f) return Mathf.RoundToInt(val);
            }
            return RunSession.HasMetaPerk("deep_quiver") ? 25 : 20;
        }
    }

    public int CurrentAmmo
    {
        get => RunSession.CurrentAmmo;
        private set
        {
            int clamped = Mathf.Clamp(value, 0, MaxAmmo);
            if (RunSession.CurrentAmmo != clamped)
            {
                RunSession.CurrentAmmo = clamped;
                NotifyChanged();
            }
        }
    }

    public bool HasAmmo => CurrentAmmo > 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        if (stats == null)
        {
            stats = GetComponent<PlayerStats>();
            if (stats == null && Player.Instance != null)
            {
                stats = Player.Instance.Stats;
            }
            if (stats == null)
            {
                stats = GetComponentInParent<PlayerStats>();
            }
            if (stats == null)
            {
                stats = GetComponentInChildren<PlayerStats>();
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        if (stats == null && Player.Instance != null)
        {
            stats = Player.Instance.Stats;
        }

        if (stats != null)
        {
            // Register base capacity 20; meta perks or draft upgrades layer on top
            stats.SetBase(StatType.MaxAmmo, 20f);
        }

        // Deep Quiver permanent meta perk: +5 capacity
        if (RunSession.HasMetaPerk("deep_quiver") && stats != null)
        {
            stats.AddModifier(StatType.MaxAmmo, ModifierKind.Flat, 5f);
        }

        // Clamp in-run ammo to current max capacity
        int max = MaxAmmo;
        if (RunSession.CurrentAmmo > max)
        {
            RunSession.CurrentAmmo = max;
        }
        else if (RunSession.CurrentAmmo <= 0 && RunSession.CurrentWave <= 1 && RunSession.InRunGold == 0)
        {
            // Fresh run begins full
            RunSession.CurrentAmmo = max;
        }

        NotifyChanged();
    }

    /// <summary>
    ///     Attempts to consume <paramref name="amount" /> ammo.
    ///     Returns true if successfully spent; returns false if insufficient ammo.
    /// </summary>
    public bool TryConsumeAmmo(int amount = 1)
    {
        if (amount <= 0) return true;

        if (RunSession.CurrentAmmo < amount)
        {
            PlayOutOfAmmoSound();
            return false;
        }

        RunSession.CurrentAmmo -= amount;
        NotifyChanged();
        return true;
    }

    /// <summary>
    ///     Adds ammunition up to <see cref="MaxAmmo" />.
    ///     Returns the actual number of rounds added (0 if already full).
    /// </summary>
    public int AddAmmo(int amount)
    {
        if (amount <= 0) return 0;

        int before = RunSession.CurrentAmmo;
        int target = Mathf.Clamp(before + amount, 0, MaxAmmo);
        int added = target - before;

        if (added > 0)
        {
            RunSession.CurrentAmmo = target;
            NotifyChanged();
            PlayPickupSound();
        }

        return added;
    }

    /// <summary>
    ///     Instantly restores ammunition to maximum capacity.
    /// </summary>
    public void RefillAmmo()
    {
        int max = MaxAmmo;
        if (RunSession.CurrentAmmo != max)
        {
            RunSession.CurrentAmmo = max;
            NotifyChanged();
            PlayPickupSound();
        }
    }

    private void NotifyChanged()
    {
        int curr = RunSession.CurrentAmmo;
        int max = MaxAmmo;
        OnAmmoChanged?.Invoke(curr, max);
        OnAnyAmmoChanged?.Invoke(curr, max);
    }

    public void PlayOutOfAmmoSound()
    {
        if (outOfAmmoClip != null)
        {
            AudioSource.PlayClipAtPoint(outOfAmmoClip, transform.position, 0.7f);
        }
    }

    private void PlayPickupSound()
    {
        if (pickupAmmoClip != null)
        {
            AudioSource.PlayClipAtPoint(pickupAmmoClip, transform.position, 0.8f);
        }
    }
}
