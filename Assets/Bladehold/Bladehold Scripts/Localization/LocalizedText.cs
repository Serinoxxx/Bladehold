using TMPro;
using UnityEngine;

/// <summary>
///     Binds a TMP label's text to a <see cref="Loc" /> key — the component for scene/prefab-baked
///     static labels (settings rows, tab titles, menu buttons, panel headings) whose full text never
///     changes at runtime. Applies on enable and re-applies on <see cref="Loc.OnLanguageChanged" />;
///     runtime-composed text (wave counters, costs, tooltips) instead calls
///     <see cref="Loc.Get(string)" />/<see cref="Loc.Format" /> at its callsite.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [Tooltip("Strings.csv key whose text this label shows, e.g. 'settings.master_volume'.")]
    [SerializeField] private string key;

    private TMP_Text label;

    private void OnEnable()
    {
        if (label == null)
        {
            label = GetComponent<TMP_Text>();
        }
        Apply();
        Loc.OnLanguageChanged += Apply;
    }

    private void OnDisable()
    {
        Loc.OnLanguageChanged -= Apply;
    }

    /// <summary>Points the label at a different key (e.g. a title picked at runtime) and re-renders.</summary>
    public void SetKey(string newKey)
    {
        key = newKey;
        if (isActiveAndEnabled)
        {
            Apply();
        }
    }

    private void Apply()
    {
        if (label != null && !string.IsNullOrEmpty(key))
        {
            label.text = Loc.Get(key);
        }
    }
}
