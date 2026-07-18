using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
///     The Enemy Manager's Animation tab: browse the vendored clip libraries (Synty + Kevin
///     Iglesias), preview any Humanoid clip on the selected enemy in an isolated
///     <see cref="EnemyAnimPreviewStage" /> (scrub or play at speed — no play mode needed), and
///     assign a chosen clip into the enemy's AnimatorOverrideController so that variant plays it
///     for a given state (Attack, Death, Slam, …). The AOC lives per variant under
///     <c>Bladehold Animations/Overrides/</c> and is wired as the prefab's controller — runtime
///     code is untouched, since trigger/state names come from the base controller the AOC wraps.
/// </summary>
public class EnemyAnimationTab : IDisposable
{
    private const string OverridesFolder = "Assets/Bladehold/Bladehold Animations/Overrides";
    private static readonly string[] ClipSearchRoots =
    {
        "Assets/Third Party/Synty",
        "Assets/Third Party/Kevin Iglesias",
    };
    // Slots whose original clip name matches one of these render at the top — they're the one-shot
    // states a designer actually retargets; locomotion cycles sit collapsed below.
    private static readonly string[] OneShotHints = { "Attack", "Death", "Cheer", "Slam", "Knockdown", "GetUp", "Storm", "Cast", "Hit", "Taunt" };

    private string clipSearch = "";
    private Vector2 clipScroll;
    private Vector2 slotScroll;
    private string[] allClipPaths;
    private AnimationClip selectedClip;
    private bool showLocomotionSlots;

    private readonly EnemyAnimSampler sampler = new EnemyAnimSampler();
    private Animator boundAnimator;
    private bool playing;
    private double playTime;
    private float playSpeed = 1f;
    private double lastUpdateTime;

    public void Dispose()
    {
        StopPlaying();
        sampler.Dispose();
    }

    public void Draw(EnemyManagerSession session)
    {
        EnemyRow row = session.SelectedRow;
        if (row == null)
        {
            EditorGUILayout.HelpBox("Select an enemy type on the left.", MessageType.Info);
            return;
        }

        string prefabPath = EnemyModelTab.FindVariantPath(row.Id, out string mapError);
        if (prefabPath == null)
        {
            EditorGUILayout.HelpBox(mapError, MessageType.Warning);
            return;
        }

        DrawPreviewControls(row, prefabPath);
        EditorGUILayout.Space();
        DrawClipBrowser();
        EditorGUILayout.Space();
        DrawOverrideSlots(row, prefabPath);
    }

    // ---- Preview -----------------------------------------------------------

    private void DrawPreviewControls(EnemyRow row, string prefabPath)
    {
        EnemyAnimPreviewStage stage = EnemyAnimPreviewStage.Current;

        if (stage == null || stage.Instance == null)
        {
            if (GUILayout.Button($"Open Preview Stage ({row.DisplayName})", GUILayout.Height(26f)))
            {
                EnemyAnimPreviewStage.Show(prefabPath, row.DisplayName);
            }
            return;
        }

        Animator rig = stage.RigAnimator;
        if (rig == null)
        {
            EditorGUILayout.HelpBox("The preview instance has no Animator — the rig can't be posed.", MessageType.Warning);
            return;
        }

        // (Re)bind the sampler if the stage was reopened or points at a different instance now.
        if (!sampler.IsOpen || boundAnimator != rig)
        {
            StopPlaying();
            sampler.Open(rig);
            boundAnimator = rig;
            if (selectedClip != null)
            {
                sampler.SetClip(selectedClip);
                sampler.Evaluate(0);
            }
        }

        if (selectedClip == null)
        {
            EditorGUILayout.HelpBox("Pick a clip below to pose the preview.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField($"Previewing: {selectedClip.name}  ({selectedClip.length:0.00}s{(selectedClip.isHumanMotion ? "" : ", NOT humanoid — will not retarget")})", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        float scrub = EditorGUILayout.Slider("Time", (float)playTime, 0f, selectedClip.length);
        if (EditorGUI.EndChangeCheck())
        {
            StopPlaying();
            playTime = scrub;
            sampler.Evaluate(playTime);
            SceneView.RepaintAll();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(playing ? "Stop" : "Play", GUILayout.Width(60f)))
        {
            if (playing)
            {
                StopPlaying();
            }
            else
            {
                StartPlaying();
            }
        }
        playSpeed = EditorGUILayout.Slider("Speed", playSpeed, 0.1f, 2f);
        EditorGUILayout.EndHorizontal();
    }

    private void StartPlaying()
    {
        if (playing)
        {
            return;
        }
        playing = true;
        lastUpdateTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += TickPlayback;
    }

    private void StopPlaying()
    {
        if (!playing)
        {
            return;
        }
        playing = false;
        EditorApplication.update -= TickPlayback;
    }

    private void TickPlayback()
    {
        if (selectedClip == null || !sampler.IsOpen)
        {
            StopPlaying();
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        playTime += (now - lastUpdateTime) * playSpeed;
        lastUpdateTime = now;
        if (playTime > selectedClip.length)
        {
            playTime %= selectedClip.length;
        }
        sampler.Evaluate(playTime);
        SceneView.RepaintAll();
    }

    // ---- Clip browser -------------------------------------------------------

    private void DrawClipBrowser()
    {
        EditorGUILayout.LabelField("Clip Library (Synty + Kevin Iglesias)", EditorStyles.boldLabel);

        if (allClipPaths == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:AnimationClip", ClipSearchRoots);
            var paths = new List<string>(guids.Length);
            foreach (string guid in guids)
            {
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }
            paths.Sort(StringComparer.OrdinalIgnoreCase);
            allClipPaths = paths.ToArray();
        }

        clipSearch = EditorGUILayout.TextField("Search", clipSearch);

        clipScroll = EditorGUILayout.BeginScrollView(clipScroll, GUILayout.Height(180f));
        int shown = 0;
        foreach (string path in allClipPaths)
        {
            string file = Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrEmpty(clipSearch) && file.IndexOf(clipSearch, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }
            if (++shown > 200)
            {
                EditorGUILayout.LabelField("… more matches — narrow the search.", EditorStyles.miniLabel);
                break;
            }

            bool isSelected = selectedClip != null && AssetDatabase.GetAssetPath(selectedClip) == path;
            if (GUILayout.Toggle(isSelected, file, "Button") && !isSelected)
            {
                SelectClipAt(path, file);
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void SelectClipAt(string path, string clipName)
    {
        // FBX files hold several sub-assets; pick the AnimationClip matching the file (or the first
        // non-preview clip).
        AnimationClip found = null;
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is AnimationClip candidate && !candidate.name.StartsWith("__preview__", StringComparison.Ordinal))
            {
                found = candidate;
                if (candidate.name == clipName)
                {
                    break;
                }
            }
        }
        if (found == null)
        {
            return;
        }

        selectedClip = found;
        playTime = 0;
        if (sampler.IsOpen)
        {
            sampler.SetClip(selectedClip);
            sampler.Evaluate(0);
            SceneView.RepaintAll();
        }
    }

    // ---- AnimatorOverrideController slots -----------------------------------

    private void DrawOverrideSlots(EnemyRow row, string prefabPath)
    {
        EditorGUILayout.LabelField("Animation Overrides (saved into the prefab variant)", EditorStyles.boldLabel);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Animator animator = prefab != null ? prefab.GetComponentInChildren<Animator>(true) : null;
        RuntimeAnimatorController rac = animator != null ? animator.runtimeAnimatorController : null;
        if (rac == null)
        {
            EditorGUILayout.HelpBox("The prefab's Animator has no controller — nothing to override.", MessageType.Warning);
            return;
        }

        var existingAoc = rac as AnimatorOverrideController;
        RuntimeAnimatorController baseController = existingAoc != null ? existingAoc.runtimeAnimatorController : rac;

        // A probe AOC enumerates the base controller's overridable clips without touching assets.
        var probe = new AnimatorOverrideController(baseController);
        var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        probe.GetOverrides(pairs);
        pairs.Sort((a, b) => IsOneShot(b.Key.name).CompareTo(IsOneShot(a.Key.name)));

        slotScroll = EditorGUILayout.BeginScrollView(slotScroll);
        bool drewLocomotionHeader = false;
        foreach (KeyValuePair<AnimationClip, AnimationClip> pair in pairs)
        {
            if (pair.Key == null)
            {
                continue;
            }
            if (!IsOneShot(pair.Key.name))
            {
                if (!drewLocomotionHeader)
                {
                    drewLocomotionHeader = true;
                    showLocomotionSlots = EditorGUILayout.Foldout(showLocomotionSlots, "Locomotion clips");
                }
                if (!showLocomotionSlots)
                {
                    continue;
                }
            }

            AnimationClip current = existingAoc != null ? existingAoc[pair.Key] : null;
            // AOC indexing returns the original when unoverridden — display that as "no override".
            if (current == pair.Key)
            {
                current = null;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            var chosen = (AnimationClip)EditorGUILayout.ObjectField(pair.Key.name, current, typeof(AnimationClip), false);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyOverride(prefabPath, pair.Key, chosen);
                GUIUtility.ExitGUI();
            }
            using (new EditorGUI.DisabledScope(selectedClip == null || selectedClip == current))
            {
                if (GUILayout.Button("◄ use previewed", GUILayout.Width(110f)))
                {
                    ApplyOverride(prefabPath, pair.Key, selectedClip);
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    private static bool IsOneShot(string clipName)
    {
        foreach (string hint in OneShotHints)
        {
            if (clipName.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    ///     Writes one original → replacement pair into the variant's AOC (created on first use under
    ///     <see cref="OverridesFolder" />) and wires the AOC as the prefab's controller. A null
    ///     replacement clears the override. Never nests AOCs — an existing one is edited in place.
    /// </summary>
    private static void ApplyOverride(string prefabPath, AnimationClip original, AnimationClip replacement)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Animator animator = root.GetComponentInChildren<Animator>(true);
            RuntimeAnimatorController rac = animator.runtimeAnimatorController;
            var aoc = rac as AnimatorOverrideController;
            if (aoc == null)
            {
                EnsureFolder(OverridesFolder);
                aoc = new AnimatorOverrideController(rac) { name = $"AOC_{root.name}" };
                AssetDatabase.CreateAsset(aoc, $"{OverridesFolder}/AOC_{root.name}.overrideController");
                animator.runtimeAnimatorController = aoc;
            }

            aoc[original] = replacement;
            EditorUtility.SetDirty(aoc);
            AssetDatabase.SaveAssets();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log(replacement != null
                ? $"Enemy Manager: '{Path.GetFileNameWithoutExtension(prefabPath)}' now plays '{replacement.name}' for '{original.name}'."
                : $"Enemy Manager: cleared the '{original.name}' override on '{Path.GetFileNameWithoutExtension(prefabPath)}'.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
