using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
///     Bootstrap for the editor-only skill-tree preview scene: press Play in that scene to see the real
///     tree (real <see cref="SkillTreeView" />/<see cref="SkillTreeService" />/<see cref="ReincarnateService" />,
///     real purchase feedbacks) without running the game. In <c>Awake</c> — before <see cref="Wallet" /> or
///     the tree services load anything, hence the early execution order — it redirects <see cref="SaveSystem" />
///     to a separate preview save file, and seeds mock gold/points when that file doesn't exist yet.
///
///     Preview progress persists across Play sessions like the real game; wire a UI Button to
///     <see cref="ClearSave" /> to wipe it and start over. The scene is not in the build list, so the reload
///     uses the editor-only <c>LoadSceneInPlayMode</c>.
/// </summary>
[DefaultExecutionOrder(-100)]
public class SkillTreePreview : MonoBehaviour
{
    [Tooltip("Preview save file, kept separate from the real one so testing never touches actual progress.")]
    [SerializeField] private string saveFileName = "skilltree-preview.json";
    [Tooltip("Gold seeded when the preview save file doesn't exist yet. Set low to test unaffordable states.")]
    [SerializeField] private int startingGold = 100000;
    [Tooltip("Reincarnate Points seeded when the preview save file doesn't exist yet.")]
    [SerializeField] private int startingReincarnatePoints = 50;

    private void Awake()
    {
        if (string.IsNullOrWhiteSpace(saveFileName) || saveFileName == SaveSystem.DefaultFileName)
        {
            Debug.LogError($"SkillTreePreview save file name must be set and must not be the real save file ('{SaveSystem.DefaultFileName}').");
            return;
        }

        SaveSystem.UseFile(saveFileName);

        if (!SaveSystem.SaveFileExists)
        {
            SaveData data = SaveSystem.Load();
            data.totalGold = startingGold;
            data.reincarnatePoints = startingReincarnatePoints;
            SaveSystem.Save(data);
        }
    }

    /// <summary>Deletes the preview save file and reloads the scene, so everything reseeds fresh. Wire to a UI Button.</summary>
    public void ClearSave()
    {
        SaveSystem.DeleteCurrentSave();
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            gameObject.scene.path,
            new LoadSceneParameters(LoadSceneMode.Single));
#else
        Debug.LogError("SkillTreePreview is an editor-only tool; ClearSave cannot reload a scene outside the build list in a player.");
#endif
    }
}
