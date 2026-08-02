using MoreMountains.Feedbacks;
using MoreMountains.Tools;
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
    [SerializeField] private AudioClip customClickSound;

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

        AudioClip clipToPlay = customClickSound;
#if UNITY_EDITOR
        if (clipToPlay == null)
        {
            clipToPlay = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Bladehold Audio/SFX/UI/UI_Click_01.wav");
        }
#endif
        if (clipToPlay != null)
        {
            MMSoundManagerPlayOptions options = MMSoundManagerPlayOptions.Default;
            options.MmSoundManagerTrack = MMSoundManager.MMSoundManagerTracks.UI;
            options.Location = transform.position;
            options.Volume = 0.8f;
            options.Pitch = Random.Range(0.92f, 1.08f);
            MMSoundManagerSoundPlayEvent.Trigger(clipToPlay, options);
        }
    }
}
