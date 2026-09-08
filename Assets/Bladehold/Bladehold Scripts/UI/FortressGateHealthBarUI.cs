using MoreMountains.Tools;
using TMPro;
using UnityEngine;

/// <summary>
///     Drives a screen-space MMProgressBar and numerical health text for the Fortress Gate.
///     Auto-binds to the active scene's Gate / Gate.All in Start if health is unassigned,
///     or falls back to RunSession.FortressGateCurrentHealth if no Gate is present in the scene.
///     Never falls back to Player.Instance.Health.
/// </summary>
public class FortressGateHealthBarUI : MonoBehaviour
{
    [Tooltip("The Gate's Health component. Auto-wired from Gate.All if left empty.")]
    [SerializeField] private Health health;

    [Tooltip("The MMProgressBar that visualises fortress gate health.")]
    [SerializeField] private MMProgressBar progressBar;

    [Tooltip("Optional text field to display exact health (e.g. 400 / 400).")]
    [SerializeField] private TextMeshProUGUI healthText;

    private bool _anyError;

    private void Awake()
    {
        if (progressBar == null)
        {
            progressBar = GetComponent<MMProgressBar>();
        }

        if (healthText == null)
        {
            Transform label = transform.Find("SliderBox/SPR_Frame/Label_HP");
            if (label != null)
            {
                healthText = label.GetComponent<TextMeshProUGUI>();
            }
        }
    }

    private void Start()
    {
        BindHealth();
        Refresh();
    }

    public void BindHealth()
    {
        if (health == null)
        {
            if (Gate.All != null && Gate.All.Count > 0)
            {
                foreach (Gate g in Gate.All)
                {
                    if (g != null && g.GetComponent<Health>() != null)
                    {
                        health = g.GetComponent<Health>();
                        break;
                    }
                }
            }

            if (health == null)
            {
                GameObject gateGo = GameObject.Find("SM_Bld_Castle_Wall_Gate_L_01 (2)");
                if (gateGo != null)
                {
                    health = gateGo.GetComponent<Health>();
                }
                else
                {
                    Gate g = FindFirstObjectByType<Gate>();
                    if (g != null)
                    {
                        health = g.GetComponent<Health>();
                    }
                }
            }
        }

        if (health != null)
        {
            health.OnHealthChanged -= Refresh;
            health.OnHealthChanged += Refresh;
            health.OnDied -= Refresh;
            health.OnDied += Refresh;
        }

        if (progressBar == null)
        {
            progressBar = GetComponent<MMProgressBar>();
        }

        if (progressBar == null)
        {
            Debug.LogError("[FortressGateHealthBarUI] MMProgressBar not assigned.");
            _anyError = true;
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnHealthChanged -= Refresh;
            health.OnDied -= Refresh;
        }
    }

    public void Refresh()
    {
        if (_anyError || progressBar == null) return;

        float cur = 0f;
        float max = 0f;

        if (health != null)
        {
            cur = health.CurrentHealth;
            max = health.MaxHealth;
        }
        else if (RunSession.FortressGateMaxHealth > 0f)
        {
            cur = Mathf.Max(0f, RunSession.FortressGateCurrentHealth);
            max = RunSession.FortressGateMaxHealth;
        }

        if (max <= 0f)
        {
            max = 400f; // Default gate max HP
            cur = max;
        }

        progressBar.UpdateBar(cur, 0f, max);
        if (healthText != null)
        {
            healthText.text = $"{Mathf.CeilToInt(cur)} / {Mathf.CeilToInt(max)}";
        }
    }
}
