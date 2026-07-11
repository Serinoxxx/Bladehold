using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     The "reincarnate as a …" class picker, shown by <see cref="DeathScreen" /> alongside the
///     Reincarnate tree once points are banked. Pure selection UI: buttons are authored in the Editor
///     (one per <see cref="ClassOption" />), clicking one highlights it and records
///     <see cref="SelectedClassId" />; <see cref="DeathScreen" /> persists the choice via
///     <see cref="PlayerClassController.SetSavedClass" /> right before the reincarnate reload. The
///     saved class starts pre-selected, so reincarnating without touching the panel keeps the
///     current class.
/// </summary>
public class ClassSelectPanel : MonoBehaviour
{
    [Serializable]
    public class ClassOption
    {
        [Tooltip("The class this option selects.")]
        public ClassDefinitionSO definition;

        [Tooltip("Authored button for this class.")]
        public Button button;

        [Tooltip("Optional label filled with the class's displayName.")]
        public TMP_Text nameLabel;

        [Tooltip("Optional label filled with the class's description blurb.")]
        public TMP_Text descriptionLabel;

        [Tooltip("Optional highlight object shown on the selected class only.")]
        public GameObject selectedHighlight;
    }

    [SerializeField] private ClassOption[] options;

    private bool anyError = false;

    /// <summary>The class id the player has picked (pre-seeded with the saved class).</summary>
    public string SelectedClassId { get; private set; }

    private void Start()
    {
        if (options == null || options.Length == 0)
        {
            Debug.LogError("ClassSelectPanel has no class options wired.");
            anyError = true;
            return;
        }

        foreach (ClassOption option in options)
        {
            if (option == null || option.definition == null || option.button == null)
            {
                Debug.LogError("ClassSelectPanel: every option needs a ClassDefinitionSO and a Button.");
                anyError = true;
                return;
            }
        }

        string savedClassId = SaveSystem.Load().playerClassId;
        ClassOption selected = null;

        foreach (ClassOption option in options)
        {
            if (option.nameLabel != null)
            {
                option.nameLabel.text = string.IsNullOrEmpty(option.definition.displayName)
                    ? option.definition.id
                    : option.definition.displayName;
            }
            if (option.descriptionLabel != null)
            {
                option.descriptionLabel.text = option.definition.description;
            }

            // Capture the loop variable for the click closure.
            ClassOption captured = option;
            option.button.onClick.AddListener(() => Select(captured));

            if (option.definition.id == savedClassId)
            {
                selected = option;
            }
        }

        Select(selected ?? options[0]);
    }

    private void OnDestroy()
    {
        if (options == null)
        {
            return;
        }
        foreach (ClassOption option in options)
        {
            if (option != null && option.button != null)
            {
                option.button.onClick.RemoveAllListeners();
            }
        }
    }

    private void Select(ClassOption selected)
    {
        if (anyError)
        {
            return;
        }

        SelectedClassId = selected.definition.id;
        foreach (ClassOption option in options)
        {
            if (option.selectedHighlight != null)
            {
                option.selectedHighlight.SetActive(option == selected);
            }
        }
    }
}
