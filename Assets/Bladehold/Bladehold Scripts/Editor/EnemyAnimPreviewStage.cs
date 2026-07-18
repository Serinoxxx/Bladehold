using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
///     The Enemy Manager's animation-preview stage: an isolated preview scene (like Prefab Mode)
///     holding a throwaway instance of the selected enemy variant on a ground plane. Nothing here
///     can dirty the user's open scene — the instance is <see cref="HideFlags.DontSave" /> and the
///     whole stage is discarded on close. Play mode never runs here, so the enemy's components stay
///     inert; only its Animator is driven, by <see cref="EnemyAnimSampler" />.
/// </summary>
public class EnemyAnimPreviewStage : PreviewSceneStage
{
    private string prefabPath;
    private string title;

    public GameObject Instance { get; private set; }

    /// <summary>The instance's rig Animator (on a child, per the Synty convention), or null when closed.</summary>
    public Animator RigAnimator => Instance != null ? Instance.GetComponentInChildren<Animator>(true) : null;

    public static EnemyAnimPreviewStage Current { get; private set; }

    /// <summary>Opens (or re-opens) the preview stage on an enemy prefab.</summary>
    public static EnemyAnimPreviewStage Show(string prefabPath, string displayName)
    {
        var stage = CreateInstance<EnemyAnimPreviewStage>();
        stage.prefabPath = prefabPath;
        stage.title = $"Preview: {displayName}";
        StageUtility.GoToStage(stage, true);
        return stage;
    }

    protected override GUIContent CreateHeaderContent()
    {
        return new GUIContent(title);
    }

    protected override bool OnOpenStage()
    {
        base.OnOpenStage();
        Current = this;

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Preview Ground";
        ground.transform.localScale = Vector3.one * 2f;
        ground.hideFlags = HideFlags.DontSave;
        SceneManager.MoveGameObjectToScene(ground, scene);

        var lightGo = new GameObject("Preview Light") { hideFlags = HideFlags.DontSave };
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        SceneManager.MoveGameObjectToScene(lightGo, scene);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"Enemy Manager: preview prefab not found at '{prefabPath}'.");
            return true;
        }
        Instance = Instantiate(prefab);
        Instance.name = prefab.name;
        Instance.hideFlags = HideFlags.DontSave;
        SceneManager.MoveGameObjectToScene(Instance, scene);
        Selection.activeGameObject = Instance;
        SceneView.lastActiveSceneView?.FrameSelected();
        return true;
    }

    protected override void OnCloseStage()
    {
        if (Current == this)
        {
            Current = null;
        }
        base.OnCloseStage();
    }
}
