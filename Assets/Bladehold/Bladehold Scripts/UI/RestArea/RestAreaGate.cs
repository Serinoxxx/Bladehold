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

    private Interactable interactable;

    public void Initialize()
    {
        if (interactable == null)
        {
            interactable = GetComponent<Interactable>();
            if (interactable != null)
            {
                interactable.PromptText = "Return to Battle";
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

        // Next wave is after the completed rest (e.g. wave 3 rest -> wave 4)
        RunSession.CurrentWave = Mathf.Max(1, RunSession.RestVisitsCount * 3 + 1);

        if (Application.isPlaying)
        {
            SceneManager.LoadScene(battleSceneName);
        }
    }
}
