using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class WarBannerController : MonoBehaviour
{
    public BannerBuffDef Buff { get; private set; }
    public BannerBountyDef Bounty { get; private set; }

    [SerializeField] private Interactable interactable;
    [SerializeField] private GameObject uiPanel;
    [SerializeField] private TMPro.TMP_Text clanNameText;
    [SerializeField] private TMPro.TMP_Text buffDescriptionText;
    [SerializeField] private TMPro.TMP_Text bountyDescriptionText;
    [SerializeField] private UnityEngine.UI.Image clanSigilImage;

    // Use a string or Action to notify GameLoopManager
    public event System.Action<WarBannerController> OnBannerInteracted;

    private void Awake()
    {
        if (interactable == null)
        {
            interactable = GetComponent<Interactable>();
            if (interactable == null)
            {
                interactable = gameObject.AddComponent<Interactable>();
            }
        }
    }

    private void Start()
    {
        interactable.OnInteractedEvent -= HandleInteracted;
        interactable.OnInteractedEvent += HandleInteracted;
    }

    private void OnDestroy()
    {
        if (interactable != null)
        {
            interactable.OnInteractedEvent -= HandleInteracted;
        }
    }

    public void Initialize(BannerBuffDef buff, BannerBountyDef bounty)
    {
        Buff = buff;
        Bounty = bounty;

        if (clanNameText != null) clanNameText.text = buff.clanName;
        if (buffDescriptionText != null) buffDescriptionText.text = buff.inGameDescription + "\n<size=80%>" + buff.gameplayEffect + "</size>";
        if (bountyDescriptionText != null) bountyDescriptionText.text = bounty.inGameDisplay;
        if (clanSigilImage != null && buff.clanSigil != null) clanSigilImage.sprite = buff.clanSigil;

        interactable.PromptText = $"Tear Down Banner\n{buff.clanName}";
        interactable.CanInteract = true;
    }

    private void HandleInteracted(Player player)
    {
        interactable.CanInteract = false;
        OnBannerInteracted?.Invoke(this);
    }
}
