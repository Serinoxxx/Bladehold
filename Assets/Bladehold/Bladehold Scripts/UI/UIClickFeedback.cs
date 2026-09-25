using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Generic click-juice add-on for a <see cref="Button" />: plays an assigned <see cref="MMF_Player" />
///     (a UI click/confirm sound, optionally a scale-pop) whenever the button's <c>onClick</c> fires.
/// </summary>
[RequireComponent(typeof(Button))]
public class UIClickFeedback : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private MMF_Player clickFeedback;

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
            button = GetComponent<Button>();
        }
        if (button != null)
        {
            button.onClick.AddListener(HandleClick);
        }
        if (clickFeedback == null)
        {
            Debug.LogError($"[UIClickFeedback] {name}: clickFeedback is not assigned, the button has no click sound.", this);
        }
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
        if (clickFeedback != null)
        {
            clickFeedback.PlayFeedbacks();
        }
    }
}
