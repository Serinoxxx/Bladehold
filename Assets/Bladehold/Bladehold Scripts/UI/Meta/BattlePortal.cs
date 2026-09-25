using UnityEngine;

/// <summary>
///     Portal/Gate in the Meta Area scene that launches a new run: wipes run state, starts a fresh
///     campaign and opens the Campaign Map.
/// </summary>
[RequireComponent(typeof(Interactable))]
public class BattlePortal : MonoBehaviour
{
    private Interactable interactable;

    public void Initialize()
    {
        if (interactable == null)
        {
            interactable = GetComponent<Interactable>();
            if (interactable != null)
            {
                interactable.PromptText = "Begin Run";
                interactable.OnInteractedEvent += HandleEnterBattle;
            }
        }
    }

    private void Awake()
    {
        Initialize();
    }

    public void EnterBattle(Player player)
    {
        HandleEnterBattle(player);
    }

    private void OnDestroy()
    {
        if (interactable != null)
        {
            interactable.OnInteractedEvent -= HandleEnterBattle;
        }
    }

    private void HandleEnterBattle(Player player)
    {
        Debug.Log("[BattlePortal] Starting brand new run...");
        RunSession.StartNewRun();
        Time.timeScale = 1f;
        CursorLockManager.SetUnlock("MetaArea", false);

        if (Application.isPlaying)
        {
            CampaignManager.Instance.StartCampaignRun();
            CampaignManager.Instance.OpenOverviewMap();
        }
    }
}
