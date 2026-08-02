using UnityEditor;
using UnityEngine;
using System.Collections;
using Unity.EditorCoroutines.Editor;
using Synty.AnimationBaseLocomotion.Samples.InputSystem;

public class PlaytestSummonMount
{
    [MenuItem("Bladehold/Playtest Summon Mount")]
    public static void Run()
    {
        EditorCoroutineUtility.StartCoroutineOwnerless(PlaytestCoroutine());
    }

    private static IEnumerator PlaytestCoroutine()
    {
        // First wire up Player prefab if missing
        string playerPath = "Assets/Bladehold/Bladehold Prefabs/Player.prefab";
        GameObject playerPrefab = PrefabUtility.LoadPrefabContents(playerPath);
        PlayerSummonMount psm = playerPrefab.GetComponent<PlayerSummonMount>();
        if (psm == null) psm = playerPrefab.AddComponent<PlayerSummonMount>();
        
        HorseMotor horsePrefab = AssetDatabase.LoadAssetAtPath<HorseMotor>("Assets/Bladehold/Bladehold Prefabs/Horse/Horse.prefab");
        SerializedObject so = new SerializedObject(psm);
        so.FindProperty("horsePrefab").objectReferenceValue = horsePrefab;
        so.ApplyModifiedProperties();
        
        PrefabUtility.SaveAsPrefabAsset(playerPrefab, playerPath);
        PrefabUtility.UnloadPrefabContents(playerPrefab);
        Debug.Log("Wired Player.prefab");
        
        WireHUD();

        EditorApplication.isPlaying = true;
        
        while (!EditorApplication.isPlaying) yield return null;
        yield return new WaitForSeconds(3f); 
        
        Player player = Player.Instance;
        if (player != null)
        {
            player.Stats.SetBase(StatType.SummonMountUnlocked, 1f);
            yield return null;
            
            var reader = player.GetComponentInChildren<InputReader>();
            if (reader != null)
            {
                var type = reader.GetType();
                var field = type.GetField("onDismountPerformed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                if (field != null) {
                   var del = (System.Action)field.GetValue(reader);
                   del?.Invoke();
                   Debug.Log("Simulated Dismount Press");
                }
            }
            
            yield return new WaitForSeconds(2.5f);
            
            PlayerMount mount = player.GetComponent<PlayerMount>();
            if (mount != null && mount.IsMounted)
            {
                Debug.Log("SUCCESS: Player is mounted! The summon mount ability worked.");
            }
            else
            {
                Debug.Log("ERROR: Player is not mounted after summon attempt.");
            }
        }
        else
        {
            Debug.Log("ERROR: Player.Instance is null");
        }
        
        yield return new WaitForSeconds(1f);
        EditorApplication.isPlaying = false;
    }

    private static void WireHUD()
    {
        string hudPath = "Assets/Bladehold/Bladehold Prefabs/UI/Bladehold HUD.prefab";
        GameObject hudPrefab = PrefabUtility.LoadPrefabContents(hudPath);
        
        WeaponHUDController whc = hudPrefab.GetComponentInChildren<WeaponHUDController>(true);
        if (whc != null && whc.meleeWeaponIcon != null)
        {
            Transform weaponContainer = whc.meleeWeaponIcon.transform.parent; 
            Transform layoutGroup = weaponContainer.parent;
            
            Transform existing = layoutGroup.Find("SummonMountButton");
            GameObject summonMountObj;
            if (existing != null)
            {
                summonMountObj = existing.gameObject;
            }
            else
            {
                summonMountObj = Object.Instantiate(weaponContainer.gameObject, layoutGroup);
                summonMountObj.name = "SummonMountButton";
            }
            
            SummonMountUI ui = summonMountObj.GetComponent<SummonMountUI>();
            if (ui == null) ui = summonMountObj.AddComponent<SummonMountUI>();
            
            UnityEngine.UI.Image skillIcon = summonMountObj.transform.Find(whc.meleeWeaponIcon.name)?.GetComponent<UnityEngine.UI.Image>();
            if (skillIcon == null) skillIcon = summonMountObj.GetComponentInChildren<UnityEngine.UI.Image>();
            ui.skillIcon = skillIcon;
            
            Transform fillObj = summonMountObj.transform.Find("CooldownFill");
            if (fillObj == null)
            {
                GameObject fillGo = new GameObject("CooldownFill");
                fillGo.transform.SetParent(summonMountObj.transform, false);
                ui.radialFillImage = fillGo.AddComponent<UnityEngine.UI.Image>();
                ui.radialFillImage.type = UnityEngine.UI.Image.Type.Filled;
                ui.radialFillImage.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
                ui.radialFillImage.color = new Color(0, 0, 0, 0.5f);
                
                RectTransform rt = fillGo.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            }
            else
            {
                ui.radialFillImage = fillObj.GetComponent<UnityEngine.UI.Image>();
            }
            
            Transform textObj = summonMountObj.transform.Find("CooldownText");
            if (textObj == null)
            {
                GameObject txtGo = new GameObject("CooldownText");
                txtGo.transform.SetParent(summonMountObj.transform, false);
                ui.timerText = txtGo.AddComponent<TMPro.TextMeshProUGUI>();
                ui.timerText.alignment = TMPro.TextAlignmentOptions.Center;
                ui.timerText.color = Color.white;
                
                RectTransform rt = txtGo.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            }
            else
            {
                ui.timerText = textObj.GetComponent<TMPro.TextMeshProUGUI>();
            }
            
            UnityEngine.UI.Image keybindIcon = summonMountObj.transform.Find(whc.meleeKeybindIcon.name)?.GetComponent<UnityEngine.UI.Image>();
            ui.keybindIcon = keybindIcon;
            
            Sprite skillSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Textures/Icons/Skills/Warriorskill_43_nobg.png");
            if (skillSprite != null && ui.skillIcon != null) ui.skillIcon.sprite = skillSprite;
            
            // --- ADD CAST BAR ---
            Transform existingCastBar = hudPrefab.transform.Find("SummonCastBar");
            if (existingCastBar == null)
            {
                GameObject castBarRoot = new GameObject("SummonCastBar");
                castBarRoot.transform.SetParent(hudPrefab.transform, false);
                
                RectTransform rt = castBarRoot.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.4f); // Middle of screen, slightly below center
                rt.anchorMax = new Vector2(0.5f, 0.4f);
                rt.sizeDelta = new Vector2(300, 30);
                rt.anchoredPosition = Vector2.zero;
                
                CanvasGroup cg = castBarRoot.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                
                // Background
                GameObject bgObj = new GameObject("Background");
                bgObj.transform.SetParent(castBarRoot.transform, false);
                UnityEngine.UI.Image bgImage = bgObj.AddComponent<UnityEngine.UI.Image>();
                bgImage.color = new Color(0, 0, 0, 0.8f);
                RectTransform bgRt = bgObj.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
                
                // Fill
                GameObject fillObj = new GameObject("Fill");
                fillObj.transform.SetParent(castBarRoot.transform, false);
                UnityEngine.UI.Image fillImage = fillObj.AddComponent<UnityEngine.UI.Image>();
                fillImage.color = Color.cyan;
                fillImage.type = UnityEngine.UI.Image.Type.Filled;
                fillImage.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
                fillImage.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
                RectTransform fillRt = fillObj.GetComponent<RectTransform>();
                fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
                fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;
                
                // Text
                GameObject txtObj = new GameObject("Label");
                txtObj.transform.SetParent(castBarRoot.transform, false);
                TMPro.TextMeshProUGUI labelText = txtObj.AddComponent<TMPro.TextMeshProUGUI>();
                labelText.text = "Summoning Mount";
                labelText.alignment = TMPro.TextAlignmentOptions.Center;
                labelText.color = Color.white;
                labelText.fontSize = 18;
                RectTransform txtRt = txtObj.GetComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
                txtRt.offsetMin = Vector2.zero; txtRt.offsetMax = Vector2.zero;
                
                // Components
                MoreMountains.Tools.MMProgressBar mmProgress = castBarRoot.AddComponent<MoreMountains.Tools.MMProgressBar>();
                mmProgress.FillMode = MoreMountains.Tools.MMProgressBar.MMProgressBarFillMode.FillAmount;
                mmProgress.ForegroundBar = fillRt;
                
                SummonCastBarUI castUi = castBarRoot.AddComponent<SummonCastBarUI>();
                castUi.progressBar = mmProgress;
                castUi.canvasGroup = cg;
                castUi.castLabel = labelText;
                
                // Juice using MMF_Player
                GameObject juiceObj = new GameObject("CastFinishedJuice");
                juiceObj.transform.SetParent(castBarRoot.transform, false);
                MMF_Player juicePlayer = juiceObj.AddComponent<MMF_Player>();
                MMF_Flash flash = new MMF_Flash();
                flash.FlashColor = Color.cyan;
                flash.FlashDuration = 0.2f;
                juicePlayer.AddFeedback(flash);
                
                castUi.castFinishedFeedback = juicePlayer;
            }
            
            PrefabUtility.SaveAsPrefabAsset(hudPrefab, hudPath);
            Debug.Log("Wired Bladehold HUD.prefab successfully");
        }
        else
        {
            Debug.Log("Failed to find WeaponHUDController or its icons.");
        }
        PrefabUtility.UnloadPrefabContents(hudPrefab);
    }
}
