using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
///     Portal/Gate in the Meta Area scene that launches a new run.
/// </summary>
[RequireComponent(typeof(Interactable))]
public class BattlePortal : MonoBehaviour
{
    [SerializeField] private string battleSceneName = "Bladehold Survivors Scene";

    private Interactable interactable;

    public void Initialize()
    {
        if (interactable == null)
        {
            interactable = GetComponent<Interactable>();
            if (interactable != null)
            {
                interactable.PromptText = "Begin Run (Enter Battle)";
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
            SceneManager.LoadScene(battleSceneName);
        }
    }
}
