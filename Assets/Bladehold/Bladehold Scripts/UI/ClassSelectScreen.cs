using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
///     Full-screen "reincarnate as a …" class picker, superseding the old <see cref="ClassSelectPanel" />.
///     Shown by <see cref="DeathScreen" /> once Reincarnate Points are banked: pick a class, see its
///     rotating 3D model (rendered by the additively-loaded "Class Preview" scene's
///     <see cref="ClassPreviewStage" /> into <see cref="previewImage" />), read its description, and hover
///     its ~3 "Key Skills" nodes (a read-only <see cref="PreviewSkillTreeService" /> feeding the same
///     <see cref="SkillNodeView" />/<see cref="SkillTooltip" /> the real trees use). The Reincarnate tree
///     stays reachable via <see cref="reincarnateTreeToggle" />. There is <b>no back/cancel</b> — the
///     points are already banked and the gold tree already wiped by the time this screen opens (the same
///     reasoning as the death screen's hidden restart buttons once that happens), so
///     <see cref="confirmButton" /> is the only way out.
/// </summary>
public class ClassSelectScreen : MonoBehaviour
{
    [Serializable]
    public class ClassEntry
    {
        [Tooltip("The class this entry selects.")]
        public ClassDefinitionSO definition;

        [Tooltip("Authored button for this class.")]
        public Button button;

        [Tooltip("Optional label filled with the class's displayName.")]
        public TMP_Text nameLabel;

        [Tooltip("Optional highlight object shown on the selected class only.")]
        public GameObject selectedHighlight;
    }

    [SerializeField] private ClassEntry[] entries;
    [Tooltip("Filled with the selected class's localized name.")]
    [SerializeField] private TMP_Text classNameLabel;
    [Tooltip("Filled with the selected class's localized description.")]
    [SerializeField] private TMP_Text classDescriptionLabel;
    [Tooltip("Persists the selected class and begins the next life.")]
    [SerializeField] private Button confirmButton;
    [Tooltip("Optional label on the confirm button (loc key death.begin_next_life).")]
    [SerializeField] private TMP_Text confirmLabel;
    [Tooltip("Optional: toggles reincarnateTreePanel above this screen so banked points can be spent without leaving it.")]
    [SerializeField] private Button reincarnateTreeToggle;
    [Tooltip("The Reincarnate skill-tree panel, shown/hidden by reincarnateTreeToggle.")]
    [SerializeField] private GameObject reincarnateTreePanel;
    [Tooltip("Displays the ClassPreviewStage camera's RenderTexture.")]
    [SerializeField] private RawImage previewImage;
    [Tooltip("The additively-loaded scene holding ClassPreviewStage.")]
    [SerializeField] private string previewSceneName = "Class Preview";
    [Tooltip("Fallback skill tree for a class with no skillTree of its own (the Swordsman/Ranger).")]
    [SerializeField] private SkillTreeSO defaultSkillTree;
    [SerializeField] private RectTransform keySkillsContainer;
    [SerializeField] private SkillNodeView keySkillNodePrefab;
    [SerializeField] private SkillTooltip tooltip;

    private bool anyError = false;
    private bool previewSceneLoaded = false;
    private ClassDefinitionSO pendingPreview;
    private PreviewSkillTreeService currentKeySkillService;
    private readonly List<SkillNodeView> keySkillViews = new List<SkillNodeView>();

    /// <summary>The class id the player has picked (pre-seeded with the saved class when the screen opens).</summary>
    public string SelectedClassId { get; private set; }

    private void Start()
    {
        if (entries == null || entries.Length == 0)
        {
            Debug.LogError("ClassSelectScreen has no class entries wired.");
            anyError = true;
            return;
        }

        foreach (ClassEntry entry in entries)
        {
            if (entry == null || entry.definition == null || entry.button == null)
            {
                Debug.LogError("ClassSelectScreen: every entry needs a ClassDefinitionSO and a Button.");
                anyError = true;
                return;
            }
        }

        if (confirmButton == null)
        {
            Debug.LogError("ClassSelectScreen has no confirmButton assigned.");
            anyError = true;
            return;
        }

        foreach (ClassEntry entry in entries)
        {
            ClassEntry captured = entry;
            entry.button.onClick.AddListener(() => Select(captured));
        }
        confirmButton.onClick.AddListener(HandleConfirm);
        if (reincarnateTreeToggle != null && reincarnateTreePanel != null)
        {
            reincarnateTreeToggle.onClick.AddListener(ToggleReincarnateTree);
        }

        if (confirmLabel != null)
        {
            confirmLabel.text = Loc.Get("death.begin_next_life");
        }
    }

    private void OnDestroy()
    {
        if (entries != null)
        {
            foreach (ClassEntry entry in entries)
            {
                if (entry != null && entry.button != null)
                {
                    entry.button.onClick.RemoveAllListeners();
                }
            }
        }
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(HandleConfirm);
        }
        if (reincarnateTreeToggle != null)
        {
            reincarnateTreeToggle.onClick.RemoveListener(ToggleReincarnateTree);
        }
        ClearKeySkillViews();
    }

    /// <summary>Activates the screen, kicks off the additive preview-scene load, and pre-selects the saved class.</summary>
    public void Open()
    {
        gameObject.SetActive(true);
        if (reincarnateTreePanel != null)
        {
            reincarnateTreePanel.SetActive(false);
        }

        if (!previewSceneLoaded)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(previewSceneName, LoadSceneMode.Additive);
            if (load == null)
            {
                Debug.LogError($"ClassSelectScreen: scene '{previewSceneName}' is not in Build Settings — the 3D preview will stay blank.");
            }
            else
            {
                load.completed += _ => HandlePreviewSceneLoaded();
            }
        }

        string savedClassId = SaveSystem.Load().playerClassId;
        ClassEntry selected = null;
        foreach (ClassEntry entry in entries)
        {
            if (entry != null && entry.definition != null && entry.definition.id == savedClassId)
            {
                selected = entry;
            }
        }
        Select(selected ?? (entries != null && entries.Length > 0 ? entries[0] : null));
    }

    private void HandlePreviewSceneLoaded()
    {
        previewSceneLoaded = true;
        ClassPreviewStage stage = ClassPreviewStage.Instance;
        if (stage == null)
        {
            Debug.LogError("ClassSelectScreen: preview scene loaded but ClassPreviewStage.Instance is null.");
            return;
        }
        if (previewImage != null)
        {
            previewImage.texture = stage.TargetTexture;
        }
        // The player may have already picked a class while the additive scene was still loading.
        if (pendingPreview != null)
        {
            stage.ShowClass(pendingPreview);
            pendingPreview = null;
        }
    }

    private void ToggleReincarnateTree()
    {
        reincarnateTreePanel.SetActive(!reincarnateTreePanel.activeSelf);
    }

    private void Select(ClassEntry selected)
    {
        if (anyError || selected == null)
        {
            return;
        }

        SelectedClassId = selected.definition.id;
        foreach (ClassEntry entry in entries)
        {
            if (entry.selectedHighlight != null)
            {
                entry.selectedHighlight.SetActive(entry == selected);
            }
            if (entry.nameLabel != null)
            {
                entry.nameLabel.text = entry.definition.LocalizedDisplayName;
            }
        }

        if (classNameLabel != null)
        {
            classNameLabel.text = selected.definition.LocalizedDisplayName;
        }
        if (classDescriptionLabel != null)
        {
            classDescriptionLabel.text = selected.definition.LocalizedDescription;
        }

        ClassPreviewStage stage = ClassPreviewStage.Instance;
        if (stage != null)
        {
            stage.ShowClass(selected.definition);
        }
        else
        {
            // The additive scene hasn't finished loading yet — applied once it has.
            pendingPreview = selected.definition;
        }

        RebuildKeySkills(selected.definition);
    }

    private void RebuildKeySkills(ClassDefinitionSO definition)
    {
        ClearKeySkillViews();

        if (keySkillsContainer == null || keySkillNodePrefab == null || definition.keySkillIds == null)
        {
            return;
        }

        SkillTreeSO tree = definition.ResolveSkillTree(defaultSkillTree);
        if (tree == null)
        {
            return;
        }

        currentKeySkillService = new PreviewSkillTreeService(tree);
        foreach (string id in definition.keySkillIds)
        {
            SkillNode node = tree.GetById(id);
            if (node == null)
            {
                Debug.LogWarning($"ClassSelectScreen: class '{definition.id}' names key skill '{id}', which isn't in its skill tree.");
                continue;
            }

            SkillNodeView view = Instantiate(keySkillNodePrefab, keySkillsContainer);
            view.Bind(node, currentKeySkillService, null);
            view.HoverEntered += HandleKeySkillHoverEntered;
            view.HoverExited += HandleKeySkillHoverExited;
            keySkillViews.Add(view);
        }
    }

    private void HandleKeySkillHoverEntered(SkillNodeView view)
    {
        if (tooltip != null && currentKeySkillService != null && view.Node != null)
        {
            tooltip.Show(view.Node, currentKeySkillService, false);
        }
    }

    private void HandleKeySkillHoverExited(SkillNodeView view)
    {
        if (tooltip != null)
        {
            tooltip.Hide();
        }
    }

    private void ClearKeySkillViews()
    {
        foreach (SkillNodeView view in keySkillViews)
        {
            if (view == null)
            {
                continue;
            }
            view.HoverEntered -= HandleKeySkillHoverEntered;
            view.HoverExited -= HandleKeySkillHoverExited;
            Destroy(view.gameObject);
        }
        keySkillViews.Clear();
        currentKeySkillService = null;
        if (tooltip != null)
        {
            tooltip.Hide();
        }
    }

    private void HandleConfirm()
    {
        if (anyError || string.IsNullOrEmpty(SelectedClassId))
        {
            return;
        }

        PlayerClassController.SetSavedClass(SelectedClassId);

        // CompleteReincarnate reloads the gameplay scene (Single mode), which auto-unloads this
        // screen's additive preview scene — never unload it explicitly. There is no back button:
        // points are already banked and the gold tree already wiped, so Confirm is the only way forward.
        if (ReincarnateService.Instance != null)
        {
            ReincarnateService.Instance.CompleteReincarnate();
        }
    }
}
