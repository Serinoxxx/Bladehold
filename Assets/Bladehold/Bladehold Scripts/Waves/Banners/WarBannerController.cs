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

    [Header("Effects")]
    [SerializeField] private GameObject slamVfxPrefab;
    [SerializeField] private AudioClip slamSfx;
    [SerializeField] private GameObject burnVfxPrefab;
    [SerializeField] private AudioClip burnSfx;
    [SerializeField] private GameObject groundWaypointPrefab;

    private GameObject activeGroundWaypoint;

    private void HandleInteracted(Player player)
    {
        interactable.CanInteract = false;
        TearDown();
        OnBannerInteracted?.Invoke(this);
    }

    public void SlamDown()
    {
        Vector3 endPos = transform.position;
        // Start high up
        transform.position = endPos + Vector3.up * 15f;
        
        // LeanTween slam down
        LeanTween.moveY(gameObject, endPos.y, 0.4f).setEase(LeanTweenType.easeInCubic).setOnComplete(() =>
        {
            if (slamVfxPrefab != null)
            {
                Instantiate(slamVfxPrefab, transform.position, Quaternion.identity);
            }
            if (slamSfx != null)
            {
                AudioSource.PlayClipAtPoint(slamSfx, transform.position, 1.0f);
            }
            if (groundWaypointPrefab != null)
            {
                activeGroundWaypoint = Instantiate(groundWaypointPrefab, transform.position, Quaternion.identity, transform);
            }
        });
    }

    private void TearDown()
    {
        if (activeGroundWaypoint != null) Destroy(activeGroundWaypoint);
        
        if (burnVfxPrefab != null)
        {
            GameObject fire = Instantiate(burnVfxPrefab, transform.position, Quaternion.identity, transform);
            // offset slightly up
            fire.transform.localPosition = new Vector3(0, 1.5f, 0);
        }
        if (burnSfx != null)
        {
            AudioSource.PlayClipAtPoint(burnSfx, transform.position, 1.0f);
        }
    }
}
