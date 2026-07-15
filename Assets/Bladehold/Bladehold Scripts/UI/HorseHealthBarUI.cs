using MoreMountains.Tools;
using UnityEngine;

/// <summary>
///     Drives a screen-space <see cref="MMProgressBar" /> for the currently mounted horse's health.
///     Subscribes to <see cref="PlayerMount.OnMountedChanged" /> and re-wires the horse's
///     <see cref="Health.OnHealthChanged" /> each time the player mounts a different horse.
///     Pair this with <see cref="HorseBarGroupUI" /> for the mount/dismount show-hide animation.
/// </summary>
public class HorseHealthBarUI : MonoBehaviour
{
    [Tooltip("The player's mount component. Auto-wired from Player.Instance if left empty.")]
    [SerializeField] private PlayerMount mount;

    [Tooltip("The MMProgressBar that visualises the horse's health.")]
    [SerializeField] private MMProgressBar progressBar;

    private Health _currentHorseHealth;
    private bool _anyError;

    private void Start()
    {
        if (mount == null && Player.Instance != null)
            mount = Player.Instance.GetComponent<PlayerMount>();

        if (mount == null)
        {
            Debug.LogError("[HorseHealthBarUI] PlayerMount not found — assign it or ensure Player.Instance has one.");
            _anyError = true;
        }

        if (progressBar == null)
        {
            Debug.LogError("[HorseHealthBarUI] MMProgressBar not assigned.");
            _anyError = true;
        }

        if (_anyError) return;

        mount.OnMountedChanged += HandleMountedChanged;

        // If already mounted at Start (e.g. StartMountedSpawner), sync immediately.
        if (mount.IsMounted)
            HandleMountedChanged(true);
    }

    private void OnDestroy()
    {
        if (mount != null)
            mount.OnMountedChanged -= HandleMountedChanged;

        UnsubscribeHorseHealth();
    }

    private void HandleMountedChanged(bool mounted)
    {
        UnsubscribeHorseHealth();

        if (!mounted) return;

        HorseMotor horse = mount.CurrentHorse;
        if (horse == null) return;

        // HorseMotor stores its Health on the same GameObject by convention.
        _currentHorseHealth = horse.GetComponent<Health>();
        if (_currentHorseHealth == null) return;

        _currentHorseHealth.OnHealthChanged += RefreshHorseHealth;
        RefreshHorseHealth(); // immediate sync
    }

    private void UnsubscribeHorseHealth()
    {
        if (_currentHorseHealth != null)
        {
            _currentHorseHealth.OnHealthChanged -= RefreshHorseHealth;
            _currentHorseHealth = null;
        }
    }

    private void RefreshHorseHealth()
    {
        if (_currentHorseHealth == null) return;
        progressBar.UpdateBar(_currentHorseHealth.CurrentHealth, 0f, _currentHorseHealth.MaxHealth);
    }
}
