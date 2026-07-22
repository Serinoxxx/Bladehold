using UnityEngine;

/// <summary>
///     Listens to the player's ImpulseBuff and applies a blue emissive color to the active weapon's
///     renderer while the buff is active.
/// </summary>
public class ActiveBuffWeaponGlow : MonoBehaviour
{
    [SerializeField] private PlayerClassController classController;
    [SerializeField] private ImpulseBuff impulseBuff;

    [Header("Glow Settings")]
    [ColorUsage(true, true)] // Allow HDR colors for emissive intensity
    [SerializeField] private Color emissiveColor = new Color(0f, 0.6f, 1f) * 2.5f; // Vivid Blue
    [SerializeField] private string emissiveKeyword = "_EMISSION";
    [SerializeField] private string emissiveColorProperty = "_EmissionColor";

    private bool anyError = false;
    private Renderer[] weaponRenderers;
    private MaterialPropertyBlock propBlock;
    private bool wasActive = false;

    private void Start()
    {
        if (classController == null)
            classController = GetComponentInParent<PlayerClassController>();
        if (impulseBuff == null)
            impulseBuff = GetComponentInChildren<ImpulseBuff>();

        if (classController == null || impulseBuff == null)
        {
            anyError = true;
            return;
        }

        propBlock = new MaterialPropertyBlock();

        // The class controller activates the weapon in Awake.
        if (classController.ActiveMeleeTrigger != null)
        {
            weaponRenderers = classController.ActiveMeleeTrigger.GetComponentsInChildren<Renderer>(true);
        }

        impulseBuff.OnChanged += HandleBuffChanged;
        HandleBuffChanged(); // Initial apply
    }

    private void OnDestroy()
    {
        if (impulseBuff != null)
        {
            impulseBuff.OnChanged -= HandleBuffChanged;
        }
    }

    private void HandleBuffChanged()
    {
        if (anyError || weaponRenderers == null) return;

        bool isActive = impulseBuff.IsActive;
        if (isActive == wasActive) return;
        wasActive = isActive;

        foreach (Renderer renderer in weaponRenderers)
        {
            if (renderer == null) continue;

            renderer.GetPropertyBlock(propBlock);

            if (isActive)
            {
                // Enable emission keyword on the material instance
                renderer.material.EnableKeyword(emissiveKeyword);
                propBlock.SetColor(emissiveColorProperty, emissiveColor);
            }
            else
            {
                renderer.material.DisableKeyword(emissiveKeyword);
                propBlock.SetColor(emissiveColorProperty, Color.black);
            }

            renderer.SetPropertyBlock(propBlock);
        }
    }
}
