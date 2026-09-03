using System;
using UnityEngine;

/// <summary>
///     Interactable world powerup that materializes in the arena between waves.
///     When interacted with via [E], opens the 3-card draft system filtered to a randomly selected
///     DraftCategory (Weapon, Elemental, or Fortress). Selecting a card consumes the powerup and
///     signals the game loop to proceed to the next wave.
/// </summary>
[RequireComponent(typeof(Interactable))]
public class WaveUpgradePowerup : MonoBehaviour
{
    [Header("Category Configuration")]
    [SerializeField] private DraftCategory category = DraftCategory.Weapon;

    [Header("Visual Effects")]
    [SerializeField] private float bobAmplitude = 0.25f;
    [SerializeField] private float bobSpeed = 2.5f;
    [SerializeField] private float rotationSpeed = 60f;
    [SerializeField] private Transform visualTransform;

    [Header("Audio")]
    [SerializeField] private AudioClip spawnSfx;
    [SerializeField] private AudioClip interactSfx;

    private Interactable interactable;
    private Vector3 initialVisualPosition;
    private bool isCollected = false;

    public DraftCategory Category => category;
    public bool IsCollected => isCollected;

    public event Action<WaveUpgradePowerup> OnClaimed;

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        if (visualTransform == null)
        {
            Transform meshChild = transform.Find("Visual");
            visualTransform = meshChild != null ? meshChild : transform;
        }
        initialVisualPosition = visualTransform.localPosition;
    }

    private void Start()
    {
        InitializeCategory(category);

        if (spawnSfx != null)
        {
            AudioSource.PlayClipAtPoint(spawnSfx, transform.position, 1.0f);
        }
    }

    private void Update()
    {
        if (visualTransform != null && !isCollected)
        {
            // Gentle hovering bob & rotate
            float newY = initialVisualPosition.y + Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            visualTransform.localPosition = new Vector3(initialVisualPosition.x, newY, initialVisualPosition.z);
            visualTransform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        }
    }

    private void OnDestroy()
    {
        if (interactable != null)
        {
            interactable.OnInteractedEvent -= HandleInteracted;
        }
    }

    /// <summary>
    ///     Initializes the powerup with a specific upgrade category and updates prompt text and visual tint.
    /// </summary>
    public void InitializeCategory(DraftCategory newCategory)
    {
        category = newCategory;
        if (interactable == null)
        {
            interactable = GetComponent<Interactable>();
        }

        if (interactable != null)
        {
            interactable.PromptText = $"Claim Upgrade ({category})";
            interactable.OnInteractedEvent -= HandleInteracted;
            interactable.OnInteractedEvent += HandleInteracted;
        }

        ApplyCategoryColor(category);
    }

    private void ApplyCategoryColor(DraftCategory cat)
    {
        Color color = cat switch
        {
            DraftCategory.Weapon => new Color(1f, 0.4f, 0.1f, 1f),       // Fiery Orange
            DraftCategory.Elemental => new Color(0.2f, 0.8f, 1f, 1f),    // Cyan / Ice Lightning
            DraftCategory.Fortress => new Color(0.9f, 0.8f, 0.2f, 1f),   // Golden Amber
            _ => Color.white
        };

        // Tint any renderers or lights attached
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            if (rend != null && rend.material != null)
            {
                rend.material.color = color;
                if (rend.material.HasProperty("_EmissionColor"))
                {
                    rend.material.SetColor("_EmissionColor", color * 1.5f);
                }
            }
        }

        Light ptLight = GetComponentInChildren<Light>();
        if (ptLight != null)
        {
            ptLight.color = color;
        }
    }

    private void HandleInteracted(Player player)
    {
        if (isCollected) return;

        isCollected = true;
        if (interactable != null)
        {
            interactable.CanInteract = false;
        }

        if (interactSfx != null)
        {
            AudioSource.PlayClipAtPoint(interactSfx, transform.position, 1.0f);
        }

        if (SurvivorsCardSelectUI.Instance != null)
        {
            SurvivorsCardSelectUI.Instance.OpenDraft(category, onComplete: () =>
            {
                CompleteCollection();
            });
        }
        else
        {
            Debug.LogWarning("[WaveUpgradePowerup] SurvivorsCardSelectUI instance not found! Auto-completing draft.");
            CompleteCollection();
        }
    }

    private void CompleteCollection()
    {
        OnClaimed?.Invoke(this);
        Destroy(gameObject);
    }

    /// <summary>
    ///     Creates a runtime fallback powerup GameObject if no prefab was assigned.
    /// </summary>
    public static WaveUpgradePowerup Spawn(Vector3 position, DraftCategory cat, GameObject prefab = null)
    {
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab, position, Quaternion.identity);
        }
        else
        {
            go = new GameObject("WaveUpgradePowerup");
            go.transform.position = position;

            // Visual sphere
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Visual";
            sphere.transform.SetParent(go.transform, false);
            sphere.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            sphere.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);

            Collider col = sphere.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer rend = sphere.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (s != null) rend.material = new Material(s);
            }

            // Light
            GameObject lightObj = new GameObject("PowerupLight");
            lightObj.transform.SetParent(go.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            Light l = lightObj.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 7f;
            l.intensity = 2.5f;

            // Trigger collider for proximity
            SphereCollider sc = go.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 2.5f;

            go.AddComponent<Interactable>();
        }

        WaveUpgradePowerup powerup = go.GetComponent<WaveUpgradePowerup>();
        if (powerup == null)
        {
            powerup = go.AddComponent<WaveUpgradePowerup>();
        }

        powerup.InitializeCategory(cat);
        return powerup;
    }
}
