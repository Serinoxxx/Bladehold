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

    private void Awake()
    {
        player = GetComponent<Player>();
    }

    private void OnEnable()
    {
        Health.OnAnyHealthDamaged += HandleAnyHealthDamaged;
        BindInput();
    }

    private void OnDisable()
    {
        Health.OnAnyHealthDamaged -= HandleAnyHealthDamaged;
        if (ultimateAction != null)
        {
            ultimateAction.performed -= HandleUltimateInput;
            ultimateAction = null;
        }
    }

    private void Start()
    {
        if (ultimateAction == null)
        {
            BindInput();
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

    private void HandleAnyHealthDamaged(Health target, Damage damage)
    {
        if (IsUltimateActive || player == null || player.Stats == null) return;
        if (player.Stats.GetValue(StatType.UltimateUnlocked) <= 0f) return;

        if (damage.source == player.Damageable && target != player.Health)
        {
            // Base charge gained is scaled by damage dealt and charge multiplier.
            float chargeGained = damage.value * 0.1f * player.Stats.GetValue(StatType.UltimateChargeMultiplier);
            AddCharge(chargeGained);
        }
    }

    public void AddCharge(float amount)
    {
        if (IsUltimateActive || player == null || player.Stats == null) return;
        if (player.Stats.GetValue(StatType.UltimateUnlocked) <= 0f) return;

        float oldCharge = CurrentCharge;
        CurrentCharge = Mathf.Clamp(CurrentCharge + amount, 0f, MaxCharge);
        
        if (!Mathf.Approximately(oldCharge, CurrentCharge))
        {
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
