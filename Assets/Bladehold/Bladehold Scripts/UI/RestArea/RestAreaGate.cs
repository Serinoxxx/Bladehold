using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
///     Station 4 in the Rest Area: Exit Gate.
///     Interacting returns the player to the main battle area to resume waves.
/// </summary>
[RequireComponent(typeof(Interactable))]
public class RestAreaGate : MonoBehaviour
{
    [SerializeField] private string battleSceneName = "Bladehold Survivors Scene";

    [SerializeField] private Bladehold.UI.AreaDefinitionSO areaDefinition;

    private Interactable interactable;

    public void Initialize()
    {
        if (interactable == null)
        {
            interactable = GetComponent<Interactable>();
            if (interactable != null)
            {
                if (areaDefinition != null)
                {
                    interactable.PromptText = areaDefinition.GetDoorPrompt();
                }
                else
                {
                    interactable.PromptText = "Return to Battle";
                }
                interactable.OnInteractedEvent -= HandleReturnToBattle;
                interactable.OnInteractedEvent += HandleReturnToBattle;
            }
        }
    }

    private void Awake()
    {
        Initialize();
    }

    public void ReturnToBattle(Player player)
    {
        HandleReturnToBattle(player);
    }

    private void OnDestroy()
    {
        if (interactable != null)
        {
            interactable.OnInteractedEvent -= HandleReturnToBattle;
        }
    }

    private void HandleReturnToBattle(Player player)
    {
        Debug.Log("[RestAreaGate] Returning to battle scene...");
        Time.timeScale = 1f;

        // Preserve player health ratio when leaving the Rest Area (e.g. from shop healing or Troll Hearts)
        if (player != null && player.Health != null)
        {
            RunSession.PlayerHealthRatio = Mathf.Clamp01(player.Health.CurrentHealth / player.Health.MaxHealth);
        }

        // Preserve player ultimate charge
        if (player != null)
        {
            var ult = player.transform.root.GetComponentInChildren<PlayerUltimateController>(true);
            if (ult != null)
            {
                RunSession.PlayerUltimateCharge = ult.CurrentCharge;
            }
        }

        // If Castle Campaign is active, completing the Rest Area returns to Campaign Map
        if (CampaignManager.Instance != null && CampaignManager.Instance.IsCampaignActive)
        {
            Debug.Log("[RestAreaGate] Castle Campaign active: completing stop scene and opening Campaign Overview Map...");
            CampaignManager.Instance.CompleteCurrentNodeAndContinue();
            return;
        }

        // No campaign (the Rest Area opened directly in the Editor): just load a battle.
        if (Application.isPlaying)
        {
            if (areaDefinition != null)
            {
                Bladehold.UI.LoadingScreenManager.Instance.LoadArea(areaDefinition);
            }
            else
            {
                Bladehold.UI.LoadingScreenManager.Instance.LoadScene(battleSceneName);
            }
        }
    }
}
