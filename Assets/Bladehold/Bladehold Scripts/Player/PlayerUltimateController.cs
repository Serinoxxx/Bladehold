using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerUltimateController : MonoBehaviour
{
    public float CurrentCharge { get; private set; }
    public const float MaxCharge = 100f;

    public event Action<float> OnChargeChanged;
    public event Action OnUltimateActivated;
    public event Action OnUltimateDeactivated;

    public bool IsUltimateActive { get; private set; }

    private Player player;
    private InputAction ultimateAction;

    private int lastProcessedFrame = -1;
    private IDamageable lastProcessedTarget;
    private float lastProcessedDamage;

    private void Awake()
    {
        player = GetComponentInChildren<Player>();
        RegisterDefaultStats();
    }

    private void OnEnable()
    {
        Health.OnAnyHealthDamaged += HandleAnyHealthDamaged;
        BindHitTriggers();
        BindInput();
    }

    private void OnDisable()
    {
        Health.OnAnyHealthDamaged -= HandleAnyHealthDamaged;
        UnbindHitTriggers();
        if (ultimateAction != null)
        {
            ultimateAction.performed -= HandleUltimateInput;
            ultimateAction = null;
        }
    }

    private void Start()
    {
        RegisterDefaultStats();
        BindHitTriggers();
        if (ultimateAction == null)
        {
            BindInput();
        }
    }

    private void RegisterDefaultStats()
    {
        if (player != null && player.Stats != null)
        {
            player.Stats.SetBase(StatType.UltimateChargeMultiplier, 1f);
            player.Stats.SetBase(StatType.UltimateDurationSeconds, 6f);
            player.Stats.SetBase(StatType.UltimateUnlocked, 0f);
        }
    }

    private void BindInput()
    {
        if (player != null && player.InputSettings != null)
        {
            var map = player.InputSettings.GetRebindableActionMap();
            if (map != null)
            {
                ultimateAction = map.FindAction("Ultimate");
                if (ultimateAction != null)
                {
                    ultimateAction.performed -= HandleUltimateInput;
                    ultimateAction.performed += HandleUltimateInput;
                    if (!ultimateAction.enabled) ultimateAction.Enable();
                }
            }
        }
    }

    private void BindHitTriggers()
    {
        if (player == null) return;

        foreach (var trigger in player.GetComponentsInChildren<DamageTrigger>(true))
        {
            trigger.OnHit -= HandleHitEvent;
            trigger.OnHit += HandleHitEvent;
        }

        var bow = player.GetComponentInChildren<PlayerBow>(true);
        if (bow != null)
        {
            bow.OnHit -= HandleHitEvent;
            bow.OnHit += HandleHitEvent;
        }

        var wand = player.GetComponentInChildren<PlayerWand>(true);
        if (wand != null)
        {
            wand.OnHit -= HandleHitEvent;
            wand.OnHit += HandleHitEvent;
        }

        var thrownAxe = player.GetComponentInChildren<PlayerThrownAxe>(true);
        if (thrownAxe != null)
        {
            thrownAxe.OnHit -= HandleHitEvent;
            thrownAxe.OnHit += HandleHitEvent;
        }
    }

    private void UnbindHitTriggers()
    {
        if (player == null) return;

        foreach (var trigger in player.GetComponentsInChildren<DamageTrigger>(true))
        {
            trigger.OnHit -= HandleHitEvent;
        }

        var bow = player.GetComponentInChildren<PlayerBow>(true);
        if (bow != null) bow.OnHit -= HandleHitEvent;

        var wand = player.GetComponentInChildren<PlayerWand>(true);
        if (wand != null) wand.OnHit -= HandleHitEvent;

        var thrownAxe = player.GetComponentInChildren<PlayerThrownAxe>(true);
        if (thrownAxe != null) thrownAxe.OnHit -= HandleHitEvent;
    }

    private void HandleHitEvent(IDamageable target, Damage damage, Vector3 hitPoint)
    {
        ProcessDamage(target, damage);
    }

    private void HandleAnyHealthDamaged(Health target, Damage damage)
    {
        ProcessDamage(target, damage);
    }

    private void ProcessDamage(IDamageable target, Damage damage)
    {
        if (IsUltimateActive || player == null || player.Stats == null) return;

        // Deduplicate multiple callbacks for the exact same hit in the same frame
        if (Time.frameCount == lastProcessedFrame && ReferenceEquals(target, lastProcessedTarget) && Mathf.Approximately(damage.value, lastProcessedDamage))
        {
            return;
        }

        lastProcessedFrame = Time.frameCount;
        lastProcessedTarget = target;
        lastProcessedDamage = damage.value;

        // Ignore damage dealt to the player
        if (player.Health != null && target == (IDamageable)player.Health) return;
        if (player.Damageable != null && target == player.Damageable) return;

        float unlocked = player.Stats.GetValue(StatType.UltimateUnlocked);
        Debug.Log($"[PlayerUltimateController] ProcessDamage: target={target}, damage={damage.value}, UltUnlocked={unlocked}");

        if (unlocked <= 0f)
        {
            Debug.LogWarning($"[PlayerUltimateController] Hit detected on {target}, but UltimateUnlocked stat is 0. (Purchase 'ult_unlock' in Skill Tree to enable Ultimate).");
            return;
        }

        float mult = player.Stats.GetValue(StatType.UltimateChargeMultiplier);
        if (mult <= 0f) mult = 1f;

        float chargeGained = damage.value * 0.1f * mult;
        Debug.Log($"[PlayerUltimateController] ADDING CHARGE: +{chargeGained:F1} (CurrentCharge={CurrentCharge:F1}/{MaxCharge})");
        AddCharge(chargeGained);
    }

    public void AddCharge(float amount)
    {
        if (IsUltimateActive || player == null || player.Stats == null) return;
        if (player.Stats.GetValue(StatType.UltimateUnlocked) <= 0f) return;

        float oldCharge = CurrentCharge;
        CurrentCharge = Mathf.Clamp(CurrentCharge + amount, 0f, MaxCharge);
        
        if (!Mathf.Approximately(oldCharge, CurrentCharge))
        {
            Debug.Log($"[PlayerUltimateController] Charge updated: {oldCharge:F1} -> {CurrentCharge:F1} / {MaxCharge}");
            OnChargeChanged?.Invoke(CurrentCharge);
        }
    }

    private void HandleUltimateInput(InputAction.CallbackContext context)
    {
        if (IsUltimateActive || CurrentCharge < MaxCharge || player == null || player.Stats == null) return;
        if (player.Stats.GetValue(StatType.UltimateUnlocked) <= 0f) return;

        ActivateUltimate();
    }

    private void ActivateUltimate()
    {
        IsUltimateActive = true;
        CurrentCharge = 0f;
        OnChargeChanged?.Invoke(CurrentCharge);

        OnUltimateActivated?.Invoke();

        var handlers = GetComponents<IUltimateHandler>();
        bool handlerActivated = false;
        foreach (var handler in handlers)
        {
            if (((MonoBehaviour)handler).enabled)
            {
                handler.Activate(this);
                handlerActivated = true;
            }
        }

        if (!handlerActivated)
        {
            EndUltimate();
        }
    }

    public void EndUltimate()
    {
        if (!IsUltimateActive) return;
        IsUltimateActive = false;
        OnUltimateDeactivated?.Invoke();
    }
}

public interface IUltimateHandler
{
    void Activate(PlayerUltimateController controller);
}
