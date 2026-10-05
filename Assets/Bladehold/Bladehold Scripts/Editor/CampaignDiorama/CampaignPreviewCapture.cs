using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
///     Renders a hero screenshot of every level a campaign node deploys to and assigns it as the node's
///     <see cref="CampaignNodeSO.previewImage" /> (the campaign map tooltip shows it). Each scene is opened in
///     turn and rendered from its <see cref="CampaignPreviewShotsSO" /> pose, or an automatic pose behind and
///     above the player spawn; the original scene setup is restored afterwards.
/// </summary>
public static class CampaignPreviewCapture
{
    private const string GraphPath = "Assets/Bladehold/Resources/CampaignGraph.asset";
    private const string ShotsPath = CampaignDioramaDefaults.ConfigFolder + "/CampaignPreviewShots.asset";
    public const string OutputFolder = "Assets/Bladehold/Bladehold Images/Campaign Previews";
    private const int Width = 1280;
    private const int Height = 720;

    [MenuItem("Bladehold/Campaign/Capture Level Previews", priority = 21)]
    public static void CaptureAllMenu()
    {
        CaptureAll(null);
    }

    /// <summary>Captures every campaign level (or only <paramref name="onlyScene" />) and assigns the sprites.</summary>
    public static bool CaptureAll(string onlyScene)
    {
        if (SceneManager.GetActiveScene().isDirty)
        {
            Debug.LogError("[CampaignPreviewCapture] Save the open scene first; capturing opens each level scene in turn.");
            return false;
        }
        CampaignGraphSO graph = AssetDatabase.LoadAssetAtPath<CampaignGraphSO>(GraphPath);
        if (graph == null)
        {
            Debug.LogError($"[CampaignPreviewCapture] No campaign graph at {GraphPath}.");
            return false;
        }
        CampaignPreviewShotsSO shots = AssetDatabase.LoadAssetAtPath<CampaignPreviewShotsSO>(ShotsPath);
        if (shots == null)
        {
            shots = ScriptableObject.CreateInstance<CampaignPreviewShotsSO>();
            AssetDatabase.CreateAsset(shots, ShotsPath);
        }
        EnsureFolder(OutputFolder);

        // Asset paths, not references: opening each level scene unloads unused assets, nodes included.
        Dictionary<string, List<string>> byScene = new Dictionary<string, List<string>>();
        foreach (CampaignNodeSO node in graph.allNodes)
        {
            if (node == null || string.IsNullOrEmpty(node.sceneName)) continue;
            if (onlyScene != null && node.sceneName != onlyScene) continue;
            if (!byScene.TryGetValue(node.sceneName, out List<string> list)) byScene[node.sceneName] = list = new List<string>();
            list.Add(AssetDatabase.GetAssetPath(node));
        }
        string shotsPath = AssetDatabase.GetAssetPath(shots);

        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        int done = 0;
        try
        {
            foreach (KeyValuePair<string, List<string>> kv in byScene)
            {
                string scenePath = FindScene(kv.Key);
                if (scenePath == null)
                {
                    Debug.LogError($"[CampaignPreviewCapture] Scene '{kv.Key}' not found.");
                    continue;
                }
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                string png = $"{OutputFolder}/{kv.Key}.png";
                shots = AssetDatabase.LoadAssetAtPath<CampaignPreviewShotsSO>(shotsPath);
                if (!Render(shots.Get(kv.Key), png)) continue;

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(png);
                foreach (string nodePath in kv.Value)
                {
                    CampaignNodeSO node = AssetDatabase.LoadAssetAtPath<CampaignNodeSO>(nodePath);
                    if (node == null) continue;
                    node.previewImage = sprite;
                    EditorUtility.SetDirty(node);
                    AssetDatabase.SaveAssetIfDirty(node);
                }
                done++;
            }
        }
        finally
        {
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[CampaignPreviewCapture] Captured {done}/{byScene.Count} level previews into {OutputFolder}.");
        return done == byScene.Count;
    }

    private static string FindScene(string sceneName)
    {
        foreach (string guid in AssetDatabase.FindAssets($"{sceneName} t:Scene"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName) return path;
        }
        return null;
    }

    /// <summary>Renders the open scene from the shot (or the automatic pose) into a sprite PNG.</summary>
    public static bool Render(CampaignPreviewShotsSO.Shot shot, string pngPath)
    {
        Vector3 position;
        Quaternion rotation;
        float fov = 55f;
        float brightness = 1f;
        if (shot != null)
        {
            position = shot.position;
            rotation = Quaternion.Euler(shot.euler);
            fov = shot.fieldOfView;
            brightness = shot.brightness;
        }
        else
        {
            // Behind and above the player spawn, looking out the way the player faces (over the play space).
            Player player = Object.FindFirstObjectByType<Player>(FindObjectsInactive.Include);
            Vector3 spawn = player != null ? player.transform.position : Vector3.zero;
            Vector3 forward = player != null ? player.transform.forward : Vector3.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            position = spawn - forward * 6f + Vector3.up * 13f;
            rotation = Quaternion.LookRotation(spawn + forward * 22f - position, Vector3.up);
        }

        GameObject go = new GameObject("CampaignPreviewCamera_Temp") { hideFlags = HideFlags.HideAndDontSave };
        RenderTexture rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        Texture2D tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        try
        {
            go.transform.SetPositionAndRotation(position, rotation);
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 1500f;
            cam.targetTexture = rt;
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cam.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            if (!Mathf.Approximately(brightness, 1f))
            {
                Color[] pixels = tex.GetPixels();
                for (int i = 0; i < pixels.Length; i++) pixels[i] = pixels[i] * brightness;
                tex.SetPixels(pixels);
            }
            tex.Apply();
            RenderTexture.active = previous;
            cam.targetTexture = null;

            System.IO.File.WriteAllBytes(pngPath, tex.EncodeToPNG());
        }
        finally
        {
            Object.DestroyImmediate(go);
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }

        if (!pngPath.StartsWith("Assets/")) return true; // a scratch render, not a project sprite
        AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(pngPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        return true;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
