using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Generic click-juice add-on for a <see cref="Button" />: plays an assigned <see cref="MMF_Player" />
///     (a UI click/confirm sound, optionally a scale-pop) whenever the button's <c>onClick</c> fires.
///     Drop this alongside any UI <see cref="Button" /> that has no dedicated feedback of its own — the
///     skill-tree buy buttons already have their own purchase sound (see <c>SkillNodeView</c>) and don't
///     need this.
/// </summary>
[RequireComponent(typeof(Button))]
public class UIClickFeedback : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private MMF_Player clickFeedback;

    private bool anyError = false;

    private void OnValidate()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }
    }

    private void Start()
    {
        if (button == null)
        {
            Debug.LogError("Button component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (clickFeedback == null)
        {
            Debug.LogError("UIClickFeedback 'clickFeedback' is not assigned in the inspector.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        button.onClick.AddListener(HandleClick);
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }
    }

    private void HandleClick()
    {
        clickFeedback.PlayFeedbacks();
    }
}
