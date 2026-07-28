using UnityEngine;

/// <summary>
///     Manages the UI panel showing the player's active buffs. Listens to buff components on the player
///     and instantiates a BuffIconUI for each active buff (Impulse, Lightning, Runestone Imbuement, Rage).
/// </summary>
public class ActiveBuffsUI : MonoBehaviour
{
    [SerializeField] private BuffIconUI iconPrefab;
    [SerializeField] private Transform iconContainer;
    
    [Header("Buff Setup")]
    [Tooltip("Sprite for the Impulse buff.")]
    [SerializeField] private Sprite impulseIcon;
    [Tooltip("Sprite for the Chain Lightning buff.")]
    [SerializeField] private Sprite lightningIcon;
    [Tooltip("Sprite for the Berserker Rage buff.")]
    [SerializeField] private Sprite rageIcon;
    [Tooltip("Sprite for Fire Runestone imbuement (optional fallback).")]
    [SerializeField] private Sprite fireRuneIcon;
    [Tooltip("Sprite for Ice Runestone imbuement (optional fallback).")]
    [SerializeField] private Sprite iceRuneIcon;

    private ImpulseBuff impulseBuff;
    private ChainLightningBuff lightningBuff;
    private MageImbuement imbuement;
    private RageBuff rageBuff;
    
    private BuffIconUI impulseIconInstance;
    private BuffIconUI lightningIconInstance;
    private BuffIconUI imbuementIconInstance;
    private BuffIconUI rageIconInstance;

    private bool boundToPlayer = false;
    private bool anyError = false;

    private void Start()
    {
        if (iconPrefab == null || iconContainer == null)
        {
            Debug.LogError("ActiveBuffsUI: Prefab or container is not assigned.");
            anyError = true;
            return;
        }

        TryBindPlayer();
    }

    private void TryBindPlayer()
    {
        if (boundToPlayer || Player.Instance == null) return;

        impulseBuff = Player.Instance.GetComponentInChildren<ImpulseBuff>(true);
        lightningBuff = Player.Instance.GetComponentInChildren<ChainLightningBuff>(true);
        imbuement = Player.Instance.GetComponentInChildren<MageImbuement>(true);
        rageBuff = Player.Instance.GetComponentInChildren<RageBuff>(true);
        
        if (impulseBuff != null) impulseBuff.OnChanged += HandleImpulseChanged;
        if (lightningBuff != null) lightningBuff.OnChanged += HandleLightningChanged;
        if (imbuement != null) imbuement.OnChanged += HandleImbuementChanged;
        if (rageBuff != null) rageBuff.OnChanged += HandleRageChanged;

        boundToPlayer = true;

        HandleImpulseChanged();
        HandleLightningChanged();
        HandleImbuementChanged();
        HandleRageChanged();
    }

    private void OnDestroy()
    {
        if (impulseBuff != null) impulseBuff.OnChanged -= HandleImpulseChanged;
        if (lightningBuff != null) lightningBuff.OnChanged -= HandleLightningChanged;
        if (imbuement != null) imbuement.OnChanged -= HandleImbuementChanged;
        if (rageBuff != null) rageBuff.OnChanged -= HandleRageChanged;
    }

    private void HandleImpulseChanged()
    {
        if (anyError) return;

        if (impulseBuff != null && impulseBuff.IsActive)
        {
            if (impulseIconInstance == null)
            {
                impulseIconInstance = Instantiate(iconPrefab, iconContainer);
                impulseIconInstance.Setup(impulseIcon, "IMPULSE", impulseBuff.StackCount);
            }
            else
            {
                impulseIconInstance.UpdateStacks(impulseBuff.StackCount);
            }
        }
        else if (impulseIconInstance != null)
        {
            impulseIconInstance.Expire();
            impulseIconInstance = null;
        }
    }

    private void HandleLightningChanged()
    {
        if (anyError) return;

        if (lightningBuff != null && lightningBuff.IsActive)
        {
            if (lightningIconInstance == null)
            {
                lightningIconInstance = Instantiate(iconPrefab, iconContainer);
                lightningIconInstance.Setup(lightningIcon, "LIGHTNING", 0);
            }
        }
        else if (lightningIconInstance != null)
        {
            lightningIconInstance.Expire();
            lightningIconInstance = null;
        }
    }

    private void HandleImbuementChanged()
    {
        if (anyError) return;

        if (imbuement != null && imbuement.IsActive && imbuement.CurrentElement.HasValue)
        {
            Sprite sprite = GetImbuementSprite(imbuement.CurrentElement.Value);
            string name = imbuement.CurrentElement.Value.ToString().ToUpper();
            int charges = imbuement.ChargeCount;

            if (imbuementIconInstance == null)
            {
                imbuementIconInstance = Instantiate(iconPrefab, iconContainer);
                imbuementIconInstance.Setup(sprite, name, charges);
            }
            else
            {
                imbuementIconInstance.Setup(sprite, name, charges);
            }
        }
        else if (imbuementIconInstance != null)
        {
            imbuementIconInstance.Expire();
            imbuementIconInstance = null;
        }
    }

    private void HandleRageChanged()
    {
        if (anyError) return;

        if (rageBuff != null && rageBuff.IsActive)
        {
            int stacks = Mathf.RoundToInt(rageBuff.CurrentRage);
            if (rageIconInstance == null)
            {
                Sprite icon = rageBuff.Icon != null ? rageBuff.Icon : rageIcon;
                rageIconInstance = Instantiate(iconPrefab, iconContainer);
                rageIconInstance.Setup(icon, "RAGE", stacks);
            }
            else
            {
                rageIconInstance.UpdateStacks(stacks);
            }
        }
        else if (rageIconInstance != null)
        {
            rageIconInstance.Expire();
            rageIconInstance = null;
        }
    }

    private Sprite GetImbuementSprite(ElementType element)
    {
        MageImbuement.ElementStyle style = imbuement != null ? imbuement.CurrentStyle : null;
        if (style != null && style.icon != null)
        {
            return style.icon;
        }

        switch (element)
        {
            case ElementType.Fire: return fireRuneIcon != null ? fireRuneIcon : impulseIcon;
            case ElementType.Ice: return iceRuneIcon != null ? iceRuneIcon : impulseIcon;
            case ElementType.Lightning: return lightningIcon;
            default: return impulseIcon;
        }
    }

    private void Update()
    {
        if (anyError) return;

        if (!boundToPlayer)
        {
            TryBindPlayer();
        }

        if (impulseBuff != null && impulseBuff.IsActive)
        {
            if (impulseIconInstance == null) HandleImpulseChanged();
            if (impulseIconInstance != null)
            {
                impulseIconInstance.UpdateTime(impulseBuff.RemainingSeconds, impulseBuff.MaxSeconds);
                impulseIconInstance.UpdateStacks(impulseBuff.StackCount);
            }
        }
        else if (impulseIconInstance != null)
        {
            HandleImpulseChanged();
        }

        if (lightningBuff != null && lightningBuff.IsActive)
        {
            if (lightningIconInstance == null) HandleLightningChanged();
            if (lightningIconInstance != null)
            {
                lightningIconInstance.UpdateTime(lightningBuff.RemainingSeconds, lightningBuff.MaxSeconds);
            }
        }
        else if (lightningIconInstance != null)
        {
            HandleLightningChanged();
        }

        if (imbuement != null && imbuement.IsActive)
        {
            if (imbuementIconInstance == null) HandleImbuementChanged();
            if (imbuementIconInstance != null)
            {
                float maxDuration = Player.Instance != null && Player.Instance.Stats != null
                    ? Player.Instance.Stats.GetValue(StatType.MageImbuementDuration)
                    : 10f;
                imbuementIconInstance.UpdateTime(imbuement.RemainingSeconds, maxDuration);
                imbuementIconInstance.UpdateStacks(imbuement.ChargeCount);
            }
        }
        else if (imbuementIconInstance != null)
        {
            HandleImbuementChanged();
        }

        if (rageBuff != null && rageBuff.IsActive)
        {
            if (rageIconInstance == null) HandleRageChanged();
            if (rageIconInstance != null)
            {
                rageIconInstance.UpdateTime(rageBuff.CurrentRage, rageBuff.MaxRage);
                rageIconInstance.UpdateStacks(Mathf.RoundToInt(rageBuff.CurrentRage));
            }
        }
        else if (rageIconInstance != null)
        {
            HandleRageChanged();
        }
    }
}
