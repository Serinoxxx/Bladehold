using System;
using UnityEngine;

/// <summary>
///     Station 3 in the Rest Area: The Draft Station.
///     Awards a 3-card draft for a randomly selected category (Weapon or Elemental).
///     The plinth, light, and interaction prompt dynamically reflect the chosen category.
///     Enforces Targeted Weapon Pool (only equipped weapons) and Elemental Lock.
/// </summary>
[RequireComponent(typeof(Interactable))]
public class DraftStation : MonoBehaviour
{
    [SerializeField] private AudioClip openDraftSfx;
    [SerializeField] private Light stationLight;

    private Interactable interactable;
    private bool usedThisVisit = false;
    private DraftCategory currentCategory = DraftCategory.Weapon;

    public DraftCategory CurrentCategory => currentCategory;
    public bool UsedThisVisit => usedThisVisit;

    public void Initialize()
    {
        if (interactable == null)
        {
            interactable = GetComponent<Interactable>();
        }

        // Randomly select 1 category for this rest station visit
        DraftCategory[] categories = (DraftCategory[])Enum.GetValues(typeof(DraftCategory));
        currentCategory = categories[UnityEngine.Random.Range(0, categories.Length)];

        if (interactable != null)
        {
            interactable.PromptText = $"Draft Upgrades ({currentCategory})";
            interactable.OnInteractedEvent -= HandleDraft;
            interactable.OnInteractedEvent += HandleDraft;
        }

        ApplyCategoryTheme(currentCategory);
    }

    private void Awake()
    {
        Initialize();
    }

    private void OnDestroy()
    {
        if (interactable != null)
        {
            interactable.OnInteractedEvent -= HandleDraft;
        }
    }

    private void ApplyCategoryTheme(DraftCategory cat)
    {
        Color color = cat switch
        {
            DraftCategory.Weapon => new Color(1f, 0.4f, 0.1f, 1f),      // Fiery Orange
            DraftCategory.Elemental => new Color(0.2f, 0.8f, 1f, 1f),   // Cyan / Ice Lightning
            _ => Color.white
        };

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            if (rend != null && rend.material != null)
            {
                rend.material.color = color;
                if (rend.material.HasProperty("_EmissionColor"))
                {
                    rend.material.SetColor("_EmissionColor", color * 1.5f);
                }
            }
        }

        if (stationLight == null)
        {
            Debug.LogError("[DraftStation] stationLight is not assigned (a child point Light).", this);
            return;
        }

        stationLight.color = color;
    }

    private void OnValidate()
    {
        if (stationLight == null) stationLight = GetComponentInChildren<Light>(true);
    }

    private void HandleDraft(Player player)
    {
        if (usedThisVisit) return;

        if (openDraftSfx != null)
        {
            AudioSource.PlayClipAtPoint(openDraftSfx, transform.position, 1.0f);
        }

        // Open Card Select UI
        SurvivorsCardSelectUI cardUI = SurvivorsCardSelectUI.Instance ?? FindAnyObjectByType<SurvivorsCardSelectUI>();
        if (cardUI != null)
        {
            cardUI.OpenDraft(currentCategory, onComplete: () =>
            {
                CompleteDraft();
            });
        }
        else
        {
            Debug.LogWarning("[DraftStation] SurvivorsCardSelectUI not found in scene! Auto-completing draft.");
            CompleteDraft();
        }
    }

    private void CompleteDraft()
    {
        usedThisVisit = true;
        if (interactable != null)
        {
            interactable.PromptText = "Draft Completed";
            interactable.CanInteract = false;
        }
        Debug.Log($"[DraftStation] Completed {currentCategory} Draft.");
    }
}
