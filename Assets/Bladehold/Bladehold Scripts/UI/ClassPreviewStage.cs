using UnityEngine;

/// <summary>
///     Scene singleton living in the additively-loaded "Class Preview" scene: holds the rotating model
///     shown on the class-select screen, rendered into <see cref="previewCamera" />'s
///     <see cref="RenderTexture" /> and displayed on a RawImage by <see cref="ClassSelectScreen" />.
///     Runs unaffected by <see cref="Time.timeScale" /> (the gate-death freeze happens while this screen
///     can be open) — both the model's Animator and this component's own rotation use unscaled time.
/// </summary>
public class ClassPreviewStage : MonoBehaviour
{
    public static ClassPreviewStage Instance;

    [Tooltip("The current class model is instantiated under this transform and rotated in place.")]
    [SerializeField] private Transform spawnAnchor;
    [Tooltip("Renders the spawned model into its targetTexture (assign the RenderTexture asset on the camera itself).")]
    [SerializeField] private Camera previewCamera;
    [Tooltip("Idle animator controller applied to every previewed model.")]
    [SerializeField] private RuntimeAnimatorController idleController;
    [Tooltip("Model shown for a class with no characterModelPrefab of its own (the Swordsman/Ranger).")]
    [SerializeField] private GameObject fallbackModelPrefab;
    [SerializeField] private float rotationDegreesPerSecond = 20f;

    private GameObject currentModel;
    private bool anyError = false;

    /// <summary>The camera's render target, handed to the class-select screen's RawImage.</summary>
    public RenderTexture TargetTexture => previewCamera != null ? previewCamera.targetTexture : null;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (spawnAnchor == null)
        {
            Debug.LogError("ClassPreviewStage has no spawnAnchor assigned.");
            anyError = true;
        }
        if (previewCamera == null)
        {
            Debug.LogError("ClassPreviewStage has no previewCamera assigned.");
            anyError = true;
        }
        else if (previewCamera.targetTexture == null)
        {
            Debug.LogError("ClassPreviewStage's previewCamera has no targetTexture assigned.");
            anyError = true;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>Swaps in the previewed class's model (or the fallback), with an idle animator running on unscaled time.</summary>
    public void ShowClass(ClassDefinitionSO definition)
    {
        if (anyError)
        {
            return;
        }

        if (currentModel != null)
        {
            Destroy(currentModel);
            currentModel = null;
        }

        GameObject prefab = definition != null && definition.characterModelPrefab != null
            ? definition.characterModelPrefab
            : fallbackModelPrefab;
        if (prefab == null)
        {
            Debug.LogError($"ClassPreviewStage: no model to show for class '{(definition != null ? definition.id : "<none>")}' (no characterModelPrefab and no fallbackModelPrefab assigned).");
            return;
        }

        currentModel = Instantiate(prefab, spawnAnchor);
        currentModel.transform.localPosition = Vector3.zero;
        currentModel.transform.localRotation = Quaternion.identity;
        SetLayerRecursively(currentModel, spawnAnchor.gameObject.layer);

        Animator animator = currentModel.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            animator = currentModel.AddComponent<Animator>();
        }
        animator.runtimeAnimatorController = idleController;
        // The gate-death path freezes Time.timeScale while this screen can be open.
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        // The model only renders into an offscreen RenderTexture — never let culling stop the Animator.
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    private void Update()
    {
        if (spawnAnchor != null)
        {
            spawnAnchor.Rotate(Vector3.up, rotationDegreesPerSecond * Time.unscaledDeltaTime, Space.World);
        }
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.layer = layer;
        }
    }
}
