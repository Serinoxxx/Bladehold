using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using GameAnalyticsSDK;

[Serializable]
public class RunTelemetryData
{
    public string classId;
    public string playtestVersion;
    public int startingWave;
    public int maxWaveReached;
    public float totalRunTimeSeconds;
    public string fatalEnemy;
    public string gateDestroyerEnemy;
    public int gateDestroyedWave;
    public float meleeDamageDealt;
    public float rangedDamageDealt;
    public int timesDodged;
    public float mountTimeSeconds;
    public int chestsDestroyed;
}

/// <summary>
///     Uploads playtest data. By default uses a Discord Webhook. 
///     If you import GameAnalytics, uncomment the using statement above and the code in SendToGameAnalytics().
/// </summary>
public class PlaytestTelemetryUploader : MonoBehaviour
{
    [Tooltip("Your Discord Webhook URL or Google Apps Script URL. Leave empty to disable Webhook.")]
    public string webhookUrl = "";

    [Tooltip("Enable if you have imported GameAnalytics and set up the SDK.")]
    public bool useGameAnalytics = false;
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureEditorPlatformSupported();

        GameObject go = new GameObject("PlaytestTelemetryUploader");
        DontDestroyOnLoad(go);
        
        if (UnityEngine.Object.FindFirstObjectByType<GameAnalytics>() == null)
        {
            go.AddComponent<GameAnalytics>();
        }

        GameAnalytics.SetBuildAllPlatforms(Application.version);
        GameAnalytics.Initialize();

        var uploader = go.AddComponent<PlaytestTelemetryUploader>();
        uploader.useGameAnalytics = true; // Set to true by default for ease
    }

    private static void EnsureEditorPlatformSupported()
    {
#if UNITY_EDITOR
        var settings = (GameAnalyticsSDK.Setup.Settings)Resources.Load("GameAnalytics/Settings", typeof(GameAnalyticsSDK.Setup.Settings));
        if (settings != null && settings.Platforms != null)
        {
            int winPlayerIdx = settings.Platforms.IndexOf(RuntimePlatform.WindowsPlayer);
            if (winPlayerIdx >= 0 && !settings.Platforms.Contains(RuntimePlatform.WindowsEditor))
            {
                string gameKey = settings.GetGameKey(winPlayerIdx);
                string secretKey = settings.GetSecretKey(winPlayerIdx);
                string build = (settings.Build != null && winPlayerIdx < settings.Build.Count) ? settings.Build[winPlayerIdx] : Application.version;

                settings.AddPlatform(RuntimePlatform.WindowsEditor);
                int newIdx = settings.Platforms.IndexOf(RuntimePlatform.WindowsEditor);
                if (newIdx >= 0)
                {
                    GameAnalyticsSDK.Setup.Settings.UpdateKeys(newIdx, gameKey, secretKey);
                    if (settings.Build != null && newIdx < settings.Build.Count)
                    {
                        settings.Build[newIdx] = build;
                    }
                }
                Debug.Log("[PlaytestTelemetryUploader] Automatically mapped WindowsPlayer GameAnalytics config to WindowsEditor for Play Mode.");
            }
        }
#endif
    }

    private bool isSubscribed = false;

    private void Start()
    {
        TrySubscribe();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        UpdateVersionLabels();
    }

    private void Update()
    {
        if (!isSubscribed)
        {
            TrySubscribe();
        }
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        TrySubscribe();
        UpdateVersionLabels();
    }

    private void TrySubscribe()
    {
        if (!isSubscribed && RunTelemetry.Instance != null)
        {
            RunTelemetry.Instance.OnRunEnded += HandleRunEnded;
            isSubscribed = true;
            Debug.Log("[PlaytestTelemetryUploader] Successfully subscribed to RunTelemetry.OnRunEnded.");
        }
    }

    private void UpdateVersionLabels()
    {
        var labels = GameObject.FindGameObjectsWithTag("Untagged"); // brute force fallback or just find by name
        var labelGo = GameObject.Find("VersionLabel");
        if (labelGo != null)
        {
            var tmp = labelGo.GetComponent<TMPro.TMP_Text>();
            if (tmp != null) tmp.text = "v" + Application.version;
        }
    }

    private void OnDestroy()
    {
        if (isSubscribed && RunTelemetry.Instance != null)
        {
            RunTelemetry.Instance.OnRunEnded -= HandleRunEnded;
            isSubscribed = false;
        }
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void HandleRunEnded(RunTelemetryData data)
    {
        Debug.Log($"[PlaytestTelemetryUploader] HandleRunEnded triggered! Max wave: {data.maxWaveReached}, Fatal enemy: '{data.fatalEnemy}'");
        if (useGameAnalytics)
        {
            SendToGameAnalytics(data);
        }

        if (!string.IsNullOrEmpty(webhookUrl))
        {
            string jsonPayload = JsonUtility.ToJson(data, true);
            StartCoroutine(PostWebhookRoutine(webhookUrl, jsonPayload));
        }
    }

    private void SendToGameAnalytics(RunTelemetryData data)
    {
        Debug.Log($"[PlaytestTelemetryUploader] Sending GameAnalytics events for Wave {data.maxWaveReached} (Fatal enemy: '{data.fatalEnemy}')...");
        GameAnalytics.NewProgressionEvent(GAProgressionStatus.Complete, "Run", $"Wave_{data.maxWaveReached}");
        GameAnalytics.NewDesignEvent("RunStats:TotalTime", data.totalRunTimeSeconds);
        GameAnalytics.NewDesignEvent("RunStats:TimesDodged", data.timesDodged);
        GameAnalytics.NewDesignEvent("RunStats:MountTime", data.mountTimeSeconds);
        GameAnalytics.NewDesignEvent("RunStats:ChestsDestroyed", data.chestsDestroyed);
        
        GameAnalytics.NewDesignEvent("Damage:MeleeDealt", data.meleeDamageDealt);
        GameAnalytics.NewDesignEvent("Damage:RangedDealt", data.rangedDamageDealt);

        if (!string.IsNullOrEmpty(data.fatalEnemy))
        {
            GameAnalytics.NewDesignEvent($"DeathBy:{data.fatalEnemy}", data.maxWaveReached);
        }
        if (!string.IsNullOrEmpty(data.gateDestroyerEnemy))
        {
            GameAnalytics.NewDesignEvent($"GateDestroyedBy:{data.gateDestroyerEnemy}", data.gateDestroyedWave);
        }
    }

    private IEnumerator PostWebhookRoutine(string url, string json)
    {
        // We wrap JSON in Discord's expected format if it's a discord webhook.
        string payload = url.Contains("discord") 
            ? "{\"content\": \"```json\\n" + json.Replace("\"", "\\\"") + "\\n```\"}"
            : json;

        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(payload);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Telemetry upload failed: {www.error}");
            }
            else
            {
                Debug.Log("Playtest telemetry uploaded successfully!");
            }
        }
    }
}
