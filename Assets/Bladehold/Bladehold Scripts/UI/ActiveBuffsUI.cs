using UnityEngine;

/// <summary>
///     Manages the UI panel showing the player's active buffs. Listens to buff components on the player
///     and instantiates a BuffIconUI for each active buff.
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

    private ImpulseBuff impulseBuff;
    private ChainLightningBuff lightningBuff;
    
    private BuffIconUI impulseIconInstance;
    private BuffIconUI lightningIconInstance;

    private bool anyError = false;

    private void Start()
    {
        if (iconPrefab == null || iconContainer == null)
        {
            Debug.LogError("ActiveBuffsUI: Prefab or container is not assigned.");
            anyError = true;
        }

        if (Player.Instance != null)
        {
            impulseBuff = Player.Instance.GetComponentInChildren<ImpulseBuff>();
            lightningBuff = Player.Instance.GetComponentInChildren<ChainLightningBuff>();
            
            if (impulseBuff != null) impulseBuff.OnChanged += HandleImpulseChanged;
            if (lightningBuff != null) lightningBuff.OnChanged += HandleLightningChanged;
        }
        else
        {
            Debug.LogWarning("ActiveBuffsUI: Player.Instance is null at Start. Buffs will not be tracked.");
        }
    }

    private void OnDestroy()
    {
        if (impulseBuff != null) impulseBuff.OnChanged -= HandleImpulseChanged;
        if (lightningBuff != null) lightningBuff.OnChanged -= HandleLightningChanged;
    }

    private void HandleImpulseChanged()
    {
        if (anyError) return;

        if (impulseBuff.IsActive)
        {
            if (impulseIconInstance == null)
            {
                impulseIconInstance = Instantiate(iconPrefab, iconContainer);
                impulseIconInstance.Setup(impulseIcon, "IMPULSE");
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

        if (lightningBuff.IsActive)
        {
            if (lightningIconInstance == null)
            {
                lightningIconInstance = Instantiate(iconPrefab, iconContainer);
                lightningIconInstance.Setup(lightningIcon, "LIGHTNING");
            }
        }
        else if (lightningIconInstance != null)
        {
            lightningIconInstance.Expire();
            lightningIconInstance = null;
        }
    }

    private void Update()
    {
        if (anyError) return;

        if (impulseIconInstance != null && impulseBuff != null && impulseBuff.IsActive)
        {
            impulseIconInstance.UpdateTime(impulseBuff.RemainingSeconds, impulseBuff.MaxSeconds);
        }

        if (lightningIconInstance != null && lightningBuff != null && lightningBuff.IsActive)
        {
            lightningIconInstance.UpdateTime(lightningBuff.RemainingSeconds, lightningBuff.MaxSeconds);
        }
    }
}
