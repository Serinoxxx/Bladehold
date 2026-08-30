#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Bladehold.UI;

namespace Bladehold.Editor
{
    public static class ClassSelectRestyler
    {
        [MenuItem("Tools/Bladehold/Restyle Character Select")]
        public static void Restyle()
        {
            var uiCanvas = GameObject.Find("UI_Canvas");
            if (uiCanvas == null)
            {
                Debug.LogError("[ClassSelectRestyler] UI_Canvas not found in active scene!");
                return;
            }

            var charSelect = uiCanvas.transform.Find("CharacterSelectScreen");
            if (charSelect == null)
            {
                Debug.LogError("[ClassSelectRestyler] CharacterSelectScreen not found under UI_Canvas!");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(charSelect.gameObject, "Restyle Character Select");

            // 1. Load Synty Assets & Fonts
            var texturinaFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Synty/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF.asset");
            var grenzeFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Synty/InterfaceFantasyWarriorHUD/Fonts/Grenze/Grenze-SemiBold SDF.asset");

            var bannerSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/FantasyWarrior/SPR_FantasyWarrior_Banner_06.png");
            var gemSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/FantasyWarrior/SPR_FantasyWarrior_Gem_01.png");

            var boxBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/FantasyWarrior/SPR_FantasyWarrior_Box_Background_01.png");
            var frameBox24_1 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/FantasyWarrior/SPR_FantasyWarrior_Frame_Box_24_Variant_01.png");
            var frameBox24_2 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/FantasyWarrior/SPR_FantasyWarrior_Frame_Box_24_Variant_02.png");
            var frameBox14 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/FantasyWarrior/SPR_FantasyWarrior_Frame_Box_14.png");
            var frameSmall01 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/FantasyWarrior/SPR_FantasyWarrior_Frame_Box_Small_01.png");
            var banner08 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/FantasyWarrior/SPR_FantasyWarrior_Banner_08.png");

            var wingsSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/FantasyWarrior/SPR_FantasyWarrior_Greeble_Wings_01_Variant_02.png");
            var lionSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/FantasyWarrior/SPR_FantasyWarrior_Greeble_LionHead_01.png");
            var heartSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/FantasyWarrior/SPR_FantasyWarrior_Heart_01.png");
            var barFillSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/FantasyWarrior/SPR_FantasyWarrior_Bar_Horizontal_05.png");
            var barFrameSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/FantasyWarrior/SPR_FantasyWarrior_Bar_Horizontal_12_Variant_01.png");
            var barMaskSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/FantasyWarrior/SPR_FantasyWarrior_Bar_Horizontal_03_Mask.png");
            var tracerySprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Tracery_Diamond_02.png");
            var vignetteSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Vignette_Background_01.png");

            var glowSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/FX/SPR_FX_FantasyWarrior_Glow_Box_01_Smooth.png");
            var glowBoxSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/FX/SPR_FX_FantasyWarrior_Box_Glowy_01.png");

            var p02 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Samples/Sprites/SPR_HUD_FantasyWarrior_Example_HeroPortrait_02.png");
            var p05 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Samples/Sprites/SPR_HUD_FantasyWarrior_Example_HeroPortrait_05.png");
            var p14 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Samples/Sprites/SPR_HUD_FantasyWarrior_Example_HeroPortrait_14.png");
            var pRanger = p02;
            var pBerserker = p05;
            var pMage = p14;

            // 2. Hide old BottomDescPanel and old backdrops
            var oldBottom = charSelect.Find("BottomDescPanel");
            if (oldBottom != null) oldBottom.gameObject.SetActive(false);

            var oldBackdrop1 = charSelect.Find("DarkBackdrop");
            if (oldBackdrop1 != null) oldBackdrop1.gameObject.SetActive(false);
            var oldBackdrop2 = charSelect.Find("DarkBackdrop (1)");
            if (oldBackdrop2 != null) oldBackdrop2.gameObject.SetActive(false);

            // Screen Backdrop
            var backdropT = charSelect.Find("VignetteBackdrop");
            if (backdropT == null)
            {
                var go = new GameObject("VignetteBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(charSelect, false);
                go.transform.SetAsFirstSibling();
                backdropT = go.transform;
            }
            var backdropRT = backdropT as RectTransform;
            backdropRT.anchorMin = Vector2.zero;
            backdropRT.anchorMax = Vector2.one;
            backdropRT.offsetMin = Vector2.zero;
            backdropRT.offsetMax = Vector2.zero;
            var backdropImg = backdropT.GetComponent<Image>();
            backdropImg.sprite = vignetteSprite != null ? vignetteSprite : boxBgSprite;
            backdropImg.color = new Color(0.04f, 0.07f, 0.12f, 0.98f);
            backdropImg.type = Image.Type.Simple;

            // 3. Top Header Panel
            var header = charSelect.Find("HeaderPanel") as RectTransform;
            if (header != null)
            {
                header.anchorMin = new Vector2(0.5f, 1.0f);
                header.anchorMax = new Vector2(0.5f, 1.0f);
                header.pivot = new Vector2(0.5f, 1.0f);
                header.anchoredPosition = new Vector2(0f, -25f);
                header.sizeDelta = new Vector2(1150f, 200f);

                var titleBox = header.Find("TitleBox") as RectTransform;
                if (titleBox != null)
                {
                    titleBox.anchorMin = new Vector2(0.5f, 0.5f);
                    titleBox.anchorMax = new Vector2(0.5f, 0.5f);
                    titleBox.pivot = new Vector2(0.5f, 0.5f);
                    titleBox.anchoredPosition = Vector2.zero;
                    titleBox.sizeDelta = new Vector2(1080f, 150f);
                    var img = titleBox.GetComponent<Image>();
                    if (img != null)
                    {
                        img.sprite = bannerSprite;
                        img.type = Image.Type.Sliced;
                        img.pixelsPerUnitMultiplier = 2.0f;
                        img.color = new Color(1.0f, 0.92f, 0.75f, 1.0f);
                    }

                    // Blue Gem on top of banner
                    var gemT = titleBox.Find("TopGem") as RectTransform;
                    if (gemT == null)
                    {
                        var gemGo = new GameObject("TopGem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                        gemGo.transform.SetParent(titleBox, false);
                        gemT = gemGo.transform as RectTransform;
                    }
                    gemT.anchorMin = new Vector2(0.5f, 1.0f);
                    gemT.anchorMax = new Vector2(0.5f, 1.0f);
                    gemT.pivot = new Vector2(0.5f, 0.5f);
                    gemT.anchoredPosition = new Vector2(0f, -5f);
                    gemT.sizeDelta = new Vector2(62f, 62f);
                    var gemImg = gemT.GetComponent<Image>();
                    gemImg.sprite = gemSprite;
                    gemImg.color = Color.white;
                    gemImg.preserveAspect = true;
                }

                var titleText = header.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
                if (titleText != null)
                {
                    titleText.text = "SELECT YOUR HERO";
                    if (texturinaFont != null) titleText.font = texturinaFont;
                    titleText.fontSize = 46f;
                    titleText.fontStyle = FontStyles.Bold;
                    titleText.color = new Color(0.96f, 0.88f, 0.65f, 1.0f);
                    titleText.alignment = TextAlignmentOptions.Center;
                    titleText.characterSpacing = 3f;
                    var rt = titleText.rectTransform;
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(0f, 4f);
                    rt.sizeDelta = new Vector2(900f, 80f);
                }

                var subText = header.Find("SubtitleText");
                if (subText != null) subText.gameObject.SetActive(false);

                var oldFrame = header.Find("Frame");
                if (oldFrame != null) oldFrame.gameObject.SetActive(false);
            }

            // 4. Cards Container
            var cardsContainer = charSelect.Find("CardsContainer") as RectTransform;
            if (cardsContainer != null)
            {
                cardsContainer.anchorMin = new Vector2(0.5f, 0.5f);
                cardsContainer.anchorMax = new Vector2(0.5f, 0.5f);
                cardsContainer.pivot = new Vector2(0.5f, 0.5f);
                cardsContainer.anchoredPosition = new Vector2(0f, -40f);
                cardsContainer.sizeDelta = new Vector2(2850f, 1400f);

                var hlg = cardsContainer.GetComponent<HorizontalLayoutGroup>();
                if (hlg != null)
                {
                    hlg.spacing = 80f;
                    hlg.childAlignment = TextAnchor.MiddleCenter;
                    hlg.childControlWidth = false;
                    hlg.childControlHeight = false;
                    hlg.childForceExpandWidth = false;
                    hlg.childForceExpandHeight = false;
                }

                // Setup each card with original names, character names, descriptions, and skills
                SetupCard(cardsContainer.Find("Card_Class Ranger") as RectTransform,
                    "swordsman", "RANGER", "Galan", "100/100",
                    p02 != null ? p02 : pRanger,
                    "Galan is an expert with both bow and blade, specializing in swift movements, keeping his distance, and using precision shots to take out priority targets.",
                    new[] { ("Bow", "Unlock the bow: hold aim to draw and fire arrows.", "bow_unlock"),
                            ("Multi Shot", "Fire an additional arrow with reduced damage.", "multishot"),
                            ("Flaming Arrows", "Arrows deal fire damage and have a chance to instantly explode Bombers.", "flamearrow") },
                    texturinaFont, grenzeFont,
                    boxBgSprite, frameBox24_1, frameBox24_2, frameBox14, frameSmall01, banner08,
                    heartSprite, barFillSprite, barFrameSprite, barMaskSprite, tracerySprite, gemSprite,
                    glowSprite, glowBoxSprite,
                    isSelected: false);

                SetupCard(cardsContainer.Find("Card_Class Berserker") as RectTransform,
                    "berserker", "BERSERKER", "Bronin", "250/250",
                    p05 != null ? p05 : pBerserker,
                    "Bronin likes to solve all his problems with violence. Charging head on into danger is always the answer. Pain only fuels his rage.",
                    new[] { ("Colossal Growth", "Grow even larger during ultimate.", "ult_size"),
                            ("Wound Up", "More throw damage per second wound up.", "axe_charge"),
                            ("Brute Force", "Knockback enemies on hit.", "knock_1") },
                    texturinaFont, grenzeFont,
                    boxBgSprite, frameBox24_1, frameBox24_2, frameBox14, frameSmall01, banner08,
                    heartSprite, barFillSprite, barFrameSprite, barMaskSprite, tracerySprite, gemSprite,
                    glowSprite, glowBoxSprite,
                    isSelected: true);

                SetupCard(cardsContainer.Find("Card_Class Mage") as RectTransform,
                    "mage", "MAGE", "Casteria", "80/80",
                    p14 != null ? p14 : pMage,
                    "Casteria bends the arcane elements to her will. Mastering fire, ice, and lightning, she controls the flow of battle and obliterates enemies from afar.",
                    new[] { ("Wand", "Unlock the wand: hold aim to charge and fire magic missiles.", "wand_unlock"),
                            ("Flame Imbuement", "Periodic Fire Imbuement: weapon ignites dealing AoE explosions.", "imbue_fire"),
                            ("Storm Imbuement", "Periodic Lightning Imbuement: faster attacks & chain lightning on hit.", "imbue_lightning") },
                    texturinaFont, grenzeFont,
                    boxBgSprite, frameBox24_1, frameBox24_2, frameBox14, frameSmall01, banner08,
                    heartSprite, barFillSprite, barFrameSprite, barMaskSprite, tracerySprite, gemSprite,
                    glowSprite, glowBoxSprite,
                    isSelected: false);
            }

            // 5. Action Buttons (Back & Confirm)
            var actions = charSelect.Find("ActionButtons") as RectTransform;
            if (actions != null)
            {
                actions.anchorMin = new Vector2(0.5f, 0.0f);
                actions.anchorMax = new Vector2(0.5f, 0.0f);
                actions.pivot = new Vector2(0.5f, 0.0f);
                actions.anchoredPosition = new Vector2(0f, 75f);
                actions.sizeDelta = new Vector2(2650f, 150f);

                var hlg = actions.GetComponent<HorizontalLayoutGroup>();
                if (hlg != null)
                {
                    hlg.spacing = 1750f;
                    hlg.childAlignment = TextAnchor.MiddleCenter;
                    hlg.childControlWidth = false;
                    hlg.childControlHeight = false;
                }

                // Back Button (Winged)
                var backBtn = actions.Find("BackButton") as RectTransform;
                if (backBtn != null)
                {
                    backBtn.sizeDelta = new Vector2(380f, 100f);
                    var img = backBtn.GetComponent<Image>();
                    img.sprite = frameBox14 != null ? frameBox14 : banner08;
                    img.type = Image.Type.Sliced;
                    img.color = new Color(0.10f, 0.16f, 0.24f, 1.0f);

                    // Wings Ornament behind
                    var wingsT = backBtn.Find("WingsDeco") as RectTransform;
                    if (wingsT == null)
                    {
                        var wGo = new GameObject("WingsDeco", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                        wGo.transform.SetParent(backBtn, false);
                        wGo.transform.SetAsFirstSibling();
                        wingsT = wGo.transform as RectTransform;
                    }
                    wingsT.anchorMin = new Vector2(0.5f, 0.5f);
                    wingsT.anchorMax = new Vector2(0.5f, 0.5f);
                    wingsT.anchoredPosition = new Vector2(0f, -4f);
                    wingsT.sizeDelta = new Vector2(560f, 110f);
                    var wingsImg = wingsT.GetComponent<Image>();
                    wingsImg.sprite = wingsSprite;
                    wingsImg.color = new Color(0.72f, 0.85f, 0.98f, 1.0f);
                    wingsImg.preserveAspect = true;

                    var txt = backBtn.GetComponentInChildren<TextMeshProUGUI>();
                    if (txt != null)
                    {
                        txt.text = "BACK";
                        if (texturinaFont != null) txt.font = texturinaFont;
                        txt.fontSize = 38f;
                        txt.fontStyle = FontStyles.Bold;
                        txt.color = new Color(0.96f, 0.88f, 0.65f, 1.0f);
                        txt.alignment = TextAlignmentOptions.Center;
                    }
                }

                // Confirm Button (Lion Head Crest)
                var confirmBtn = actions.Find("PlayStageButton") as RectTransform;
                if (confirmBtn != null)
                {
                    confirmBtn.sizeDelta = new Vector2(400f, 110f);
                    var img = confirmBtn.GetComponent<Image>();
                    img.sprite = frameBox14 != null ? frameBox14 : banner08;
                    img.type = Image.Type.Sliced;
                    img.color = new Color(0.85f, 0.68f, 0.25f, 1.0f);

                    // Lion Head Crest atop
                    var lionT = confirmBtn.Find("LionCrest") as RectTransform;
                    if (lionT == null)
                    {
                        var lGo = new GameObject("LionCrest", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                        lGo.transform.SetParent(confirmBtn, false);
                        lionT = lGo.transform as RectTransform;
                    }
                    lionT.anchorMin = new Vector2(0.5f, 1.0f);
                    lionT.anchorMax = new Vector2(0.5f, 1.0f);
                    lionT.pivot = new Vector2(0.5f, 0.5f);
                    lionT.anchoredPosition = new Vector2(0f, 16f);
                    lionT.sizeDelta = new Vector2(110f, 110f);
                    var lionImg = lionT.GetComponent<Image>();
                    lionImg.sprite = lionSprite;
                    lionImg.color = new Color(1.0f, 0.85f, 0.45f, 1.0f);
                    lionImg.preserveAspect = true;

                    var txt = confirmBtn.GetComponentInChildren<TextMeshProUGUI>();
                    if (txt != null)
                    {
                        txt.text = "CONFIRM";
                        if (texturinaFont != null) txt.font = texturinaFont;
                        txt.fontSize = 40f;
                        txt.fontStyle = FontStyles.Bold;
                        txt.color = new Color(1.0f, 0.95f, 0.82f, 1.0f);
                        txt.alignment = TextAlignmentOptions.Center;
                    }
                }
            }

            // Mark Scene Dirty and Save
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[ClassSelectRestyler] Successfully restyled Character Select Screen!");
        }

        private static void SetupCard(RectTransform card,
            string classId, string title, string role, string healthVal,
            Sprite portrait, string description,
            (string name, string desc, string iconName)[] skills,
            TMP_FontAsset titleFont, TMP_FontAsset bodyFont,
            Sprite bgSprite, Sprite normalFrame, Sprite glowFrame, Sprite plaqueFrame, Sprite skillFrame, Sprite badgeBanner,
            Sprite heart, Sprite barFill, Sprite barFrame, Sprite barMask, Sprite tracery, Sprite gemSprite,
            Sprite glowSprite, Sprite glowBoxSprite,
            bool isSelected)
        {
            if (card == null) return;

            // Remove Mask component to allow badges and glow gems to render outside rect
            var mask = card.GetComponent<Mask>();
            if (mask != null) Object.DestroyImmediate(mask);

            // Hide old elements
            var oldHeader = card.Find("ParchmentHeader");
            if (oldHeader != null) oldHeader.gameObject.SetActive(false);
            var oldFrame = card.Find("Frame");
            if (oldFrame != null) oldFrame.gameObject.SetActive(false);
            var oldImg = card.Find("Image");
            if (oldImg != null) oldImg.gameObject.SetActive(false);

            card.sizeDelta = isSelected ? new Vector2(815f, 1330f) : new Vector2(760f, 1140f);
            card.localScale = Vector3.one;

            // 0. Selected Back Glow (behind the card)
            var backGlowT = card.Find("SelectedBackGlow") as RectTransform;
            if (backGlowT == null)
            {
                var go = new GameObject("SelectedBackGlow", typeof(RectTransform));
                go.transform.SetParent(card, false);
                backGlowT = go.transform as RectTransform;
            }
            backGlowT.SetAsFirstSibling();
            backGlowT.anchorMin = new Vector2(0.5f, 0.5f);
            backGlowT.anchorMax = new Vector2(0.5f, 0.5f);
            backGlowT.pivot = new Vector2(0.5f, 0.5f);
            backGlowT.anchoredPosition = Vector2.zero;
            backGlowT.sizeDelta = new Vector2(815f, 1330f);

            // Outer Smooth Box Glow Halo
            var haloT = backGlowT.Find("RadialHalo") as RectTransform;
            if (haloT == null)
            {
                var hGo = new GameObject("RadialHalo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                hGo.transform.SetParent(backGlowT, false);
                haloT = hGo.transform as RectTransform;
            }
            haloT.anchorMin = new Vector2(0.5f, 0.5f);
            haloT.anchorMax = new Vector2(0.5f, 0.5f);
            haloT.pivot = new Vector2(0.5f, 0.5f);
            haloT.anchoredPosition = Vector2.zero;
            haloT.sizeDelta = new Vector2(1020f, 1530f);
            var haloImg = haloT.GetComponent<Image>();
            haloImg.sprite = glowSprite;
            haloImg.type = Image.Type.Sliced;
            haloImg.pixelsPerUnitMultiplier = 1.0f;
            haloImg.color = new Color(0.12f, 0.75f, 0.98f, 0.45f);

            // Inner Box Glow
            var boxGlowT = backGlowT.Find("BoxGlow") as RectTransform;
            if (boxGlowT == null)
            {
                var bgGo = new GameObject("BoxGlow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                bgGo.transform.SetParent(backGlowT, false);
                boxGlowT = bgGo.transform as RectTransform;
            }
            boxGlowT.anchorMin = Vector2.zero;
            boxGlowT.anchorMax = Vector2.one;
            boxGlowT.offsetMin = new Vector2(-40f, -40f);
            boxGlowT.offsetMax = new Vector2(40f, 40f);
            var boxGlowImg = boxGlowT.GetComponent<Image>();
            boxGlowImg.sprite = glowBoxSprite;
            boxGlowImg.type = Image.Type.Sliced;
            boxGlowImg.color = new Color(0.20f, 0.85f, 1.0f, 0.35f);

            backGlowT.gameObject.SetActive(isSelected);

            // Base Card Background
            var baseImg = card.GetComponent<Image>();
            if (baseImg != null)
            {
                baseImg.sprite = bgSprite;
                baseImg.type = Image.Type.Sliced;
                baseImg.color = new Color(0.055f, 0.086f, 0.133f, 0.95f);
            }

            // 1. Normal Border (Bronze/slate)
            var normalBorderT = card.Find("NormalBorder") as RectTransform;
            if (normalBorderT == null)
            {
                var go = new GameObject("NormalBorder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(card, false);
                normalBorderT = go.transform as RectTransform;
            }
            normalBorderT.anchorMin = Vector2.zero;
            normalBorderT.anchorMax = Vector2.one;
            normalBorderT.offsetMin = new Vector2(-10f, -10f);
            normalBorderT.offsetMax = new Vector2(10f, 10f);
            var normalBorderImg = normalBorderT.GetComponent<Image>();
            normalBorderImg.sprite = normalFrame;
            normalBorderImg.type = Image.Type.Sliced;
            normalBorderImg.color = new Color(0.48f, 0.42f, 0.33f, 1.0f);
            normalBorderT.gameObject.SetActive(!isSelected);

            // 2. Selected Glow Border (Cyan/Gold)
            var glowBorderT = card.Find("SelectedGlowBorder") as RectTransform;
            if (glowBorderT == null)
            {
                var go = new GameObject("SelectedGlowBorder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(card, false);
                glowBorderT = go.transform as RectTransform;
            }
            glowBorderT.anchorMin = Vector2.zero;
            glowBorderT.anchorMax = Vector2.one;
            glowBorderT.offsetMin = new Vector2(-10f, -10f);
            glowBorderT.offsetMax = new Vector2(10f, 10f);
            var glowBorderImg = glowBorderT.GetComponent<Image>();
            glowBorderImg.sprite = glowFrame;
            glowBorderImg.type = Image.Type.Sliced;
            glowBorderImg.color = new Color(0.20f, 0.90f, 1.0f, 1.0f);

            // Side Gems on selected glow border
            var gemLeft = glowBorderT.Find("GemLeft") as RectTransform;
            if (gemLeft == null)
            {
                var go = new GameObject("GemLeft", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(glowBorderT, false);
                gemLeft = go.transform as RectTransform;
            }
            gemLeft.anchorMin = new Vector2(0f, 0.5f);
            gemLeft.anchorMax = new Vector2(0f, 0.5f);
            gemLeft.anchoredPosition = new Vector2(-2f, 0f);
            gemLeft.sizeDelta = new Vector2(38f, 38f);
            var gemLeftImg = gemLeft.GetComponent<Image>();
            gemLeftImg.sprite = gemSprite;
            gemLeftImg.color = Color.white;
            gemLeftImg.preserveAspect = true;

            var gemRight = glowBorderT.Find("GemRight") as RectTransform;
            if (gemRight == null)
            {
                var go = new GameObject("GemRight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(glowBorderT, false);
                gemRight = go.transform as RectTransform;
            }
            gemRight.anchorMin = new Vector2(1f, 0.5f);
            gemRight.anchorMax = new Vector2(1f, 0.5f);
            gemRight.anchoredPosition = new Vector2(2f, 0f);
            gemRight.sizeDelta = new Vector2(38f, 38f);
            var gemRightImg = gemRight.GetComponent<Image>();
            gemRightImg.sprite = gemSprite;
            gemRightImg.color = Color.white;
            gemRightImg.preserveAspect = true;

            glowBorderT.gameObject.SetActive(isSelected);

            // 3. Selected Badge ("SELECTED")
            var badgeT = card.Find("SelectedBadge") as RectTransform;
            if (badgeT == null)
            {
                var go = new GameObject("SelectedBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(card, false);
                badgeT = go.transform as RectTransform;
            }
            badgeT.anchorMin = new Vector2(0.5f, 1.0f);
            badgeT.anchorMax = new Vector2(0.5f, 1.0f);
            badgeT.pivot = new Vector2(0.5f, 0.5f);
            badgeT.anchoredPosition = new Vector2(0f, 24f);
            badgeT.sizeDelta = new Vector2(220f, 52f);
            var badgeImg = badgeT.GetComponent<Image>();
            badgeImg.sprite = badgeBanner;
            badgeImg.type = Image.Type.Sliced;
            badgeImg.color = new Color(0.85f, 0.70f, 0.35f, 1.0f);

            var badgeTxt = badgeT.GetComponentInChildren<TextMeshProUGUI>();
            if (badgeTxt == null)
            {
                var txtGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                txtGo.transform.SetParent(badgeT, false);
                badgeTxt = txtGo.GetComponent<TextMeshProUGUI>();
            }
            badgeTxt.text = "SELECTED";
            if (titleFont != null) badgeTxt.font = titleFont;
            badgeTxt.fontSize = 24f;
            badgeTxt.fontStyle = FontStyles.Bold;
            badgeTxt.color = Color.white;
            badgeTxt.alignment = TextAlignmentOptions.Center;
            badgeTxt.rectTransform.anchorMin = Vector2.zero;
            badgeTxt.rectTransform.anchorMax = Vector2.one;
            badgeTxt.rectTransform.offsetMin = Vector2.zero;
            badgeTxt.rectTransform.offsetMax = Vector2.zero;
            badgeT.gameObject.SetActive(isSelected);

            // 4. Class Title Text
            var titleT = card.Find("ClassTitleText") as RectTransform;
            if (titleT == null)
            {
                var go = new GameObject("ClassTitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                go.transform.SetParent(card, false);
                titleT = go.transform as RectTransform;
            }
            titleT.anchorMin = new Vector2(0.5f, 1.0f);
            titleT.anchorMax = new Vector2(0.5f, 1.0f);
            titleT.pivot = new Vector2(0.5f, 1.0f);
            titleT.anchoredPosition = new Vector2(0f, -44f);
            titleT.sizeDelta = new Vector2(650f, 48f);
            var titleTxt = titleT.GetComponent<TextMeshProUGUI>();
            titleTxt.text = title;
            if (titleFont != null) titleTxt.font = titleFont;
            titleTxt.fontSize = 40f;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.color = new Color(0.96f, 0.88f, 0.72f, 1.0f);
            titleTxt.alignment = TextAlignmentOptions.Center;
            titleTxt.characterSpacing = 2f;

            // 5. Health Bar
            var healthBarT = card.Find("HealthBar") as RectTransform;
            if (healthBarT == null)
            {
                var go = new GameObject("HealthBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(card, false);
                healthBarT = go.transform as RectTransform;
            }
            healthBarT.anchorMin = new Vector2(0.5f, 1.0f);
            healthBarT.anchorMax = new Vector2(0.5f, 1.0f);
            healthBarT.pivot = new Vector2(0.5f, 1.0f);
            healthBarT.anchoredPosition = new Vector2(0f, -98f);
            healthBarT.sizeDelta = new Vector2(400f, 36f);
            var hbBg = healthBarT.GetComponent<Image>();
            hbBg.sprite = barMask;
            hbBg.type = Image.Type.Sliced;
            hbBg.color = new Color(0.18f, 0.04f, 0.04f, 0.95f);

            // Health Fill
            var fillT = healthBarT.Find("Fill") as RectTransform;
            if (fillT == null)
            {
                var go = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(healthBarT, false);
                fillT = go.transform as RectTransform;
            }
            fillT.anchorMin = new Vector2(0.04f, 0.15f);
            fillT.anchorMax = new Vector2(0.96f, 0.85f);
            fillT.offsetMin = Vector2.zero;
            fillT.offsetMax = Vector2.zero;
            var fillImg = fillT.GetComponent<Image>();
            fillImg.sprite = barFill;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 1.0f;
            fillImg.color = new Color(0.78f, 0.17f, 0.17f, 1.0f);

            // Health Frame
            var barFrameT = healthBarT.Find("Frame") as RectTransform;
            if (barFrameT == null)
            {
                var go = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(healthBarT, false);
                barFrameT = go.transform as RectTransform;
            }
            barFrameT.anchorMin = Vector2.zero;
            barFrameT.anchorMax = Vector2.one;
            barFrameT.offsetMin = new Vector2(-4f, -4f);
            barFrameT.offsetMax = new Vector2(4f, 4f);
            var barFrameImg = barFrameT.GetComponent<Image>();
            barFrameImg.sprite = barFrame;
            barFrameImg.type = Image.Type.Sliced;
            barFrameImg.color = new Color(0.70f, 0.60f, 0.45f, 1.0f);

            // Heart Icon
            var heartT = healthBarT.Find("HeartIcon") as RectTransform;
            if (heartT == null)
            {
                var go = new GameObject("HeartIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(healthBarT, false);
                heartT = go.transform as RectTransform;
            }
            heartT.anchorMin = new Vector2(0f, 0.5f);
            heartT.anchorMax = new Vector2(0f, 0.5f);
            heartT.pivot = new Vector2(1f, 0.5f);
            heartT.anchoredPosition = new Vector2(-8f, 0f);
            heartT.sizeDelta = new Vector2(34f, 34f);
            var heartImg = heartT.GetComponent<Image>();
            heartImg.sprite = heart;
            heartImg.color = new Color(0.92f, 0.22f, 0.22f, 1.0f);
            heartImg.preserveAspect = true;

            // Health Text
            var healthTxt = healthBarT.GetComponentInChildren<TextMeshProUGUI>();
            if (healthTxt == null)
            {
                var txtGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                txtGo.transform.SetParent(healthBarT, false);
                healthTxt = txtGo.GetComponent<TextMeshProUGUI>();
            }
            healthTxt.text = healthVal;
            if (titleFont != null) healthTxt.font = titleFont;
            healthTxt.fontSize = 23f;
            healthTxt.fontStyle = FontStyles.Bold;
            healthTxt.color = Color.white;
            healthTxt.alignment = TextAlignmentOptions.Center;
            healthTxt.rectTransform.anchorMin = Vector2.zero;
            healthTxt.rectTransform.anchorMax = Vector2.one;
            healthTxt.rectTransform.offsetMin = Vector2.zero;
            healthTxt.rectTransform.offsetMax = Vector2.zero;

            // 6. Concentric Diamond Tracery
            var traceryT = card.Find("Tracery") as RectTransform;
            if (traceryT == null)
            {
                var go = new GameObject("Tracery", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(card, false);
                traceryT = go.transform as RectTransform;
            }
            traceryT.anchorMin = new Vector2(0.5f, 1.0f);
            traceryT.anchorMax = new Vector2(0.5f, 1.0f);
            traceryT.pivot = new Vector2(0.5f, 0.5f);
            traceryT.anchoredPosition = new Vector2(0f, -370f);
            traceryT.sizeDelta = new Vector2(460f, 460f);
            var traceryImg = traceryT.GetComponent<Image>();
            traceryImg.sprite = tracery;
            traceryImg.color = new Color(0.24f, 0.40f, 0.52f, 0.25f);
            traceryImg.preserveAspect = true;

            // 7. Hero Portrait
            var portraitT = card.Find("Portrait") as RectTransform;
            if (portraitT == null)
            {
                var go = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(card, false);
                portraitT = go.transform as RectTransform;
            }
            portraitT.anchorMin = new Vector2(0.5f, 1.0f);
            portraitT.anchorMax = new Vector2(0.5f, 1.0f);
            portraitT.pivot = new Vector2(0.5f, 0.5f);
            portraitT.anchoredPosition = new Vector2(0f, -370f);
            portraitT.sizeDelta = new Vector2(440f, 420f);
            var portraitImg = portraitT.GetComponent<Image>();
            portraitImg.sprite = portrait;
            portraitImg.color = Color.white;
            portraitImg.preserveAspect = true;

            // 8. Role Subtitle Text
            var roleT = card.Find("RoleSubtitleText") as RectTransform;
            if (roleT == null)
            {
                var go = new GameObject("RoleSubtitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                go.transform.SetParent(card, false);
                roleT = go.transform as RectTransform;
            }
            roleT.anchorMin = new Vector2(0.5f, 1.0f);
            roleT.anchorMax = new Vector2(0.5f, 1.0f);
            roleT.pivot = new Vector2(0.5f, 1.0f);
            roleT.anchoredPosition = new Vector2(0f, -600f);
            roleT.sizeDelta = new Vector2(580f, 42f);
            var roleTxt = roleT.GetComponent<TextMeshProUGUI>();
            roleTxt.text = role;
            if (titleFont != null) roleTxt.font = titleFont;
            roleTxt.fontSize = 30f;
            roleTxt.color = new Color(0.88f, 0.83f, 0.74f, 1.0f);
            roleTxt.alignment = TextAlignmentOptions.Center;

            // 9. Skills Panel (3 Horizontal Slots)
            var skillsT = card.Find("SkillsPanel") as RectTransform;
            if (skillsT == null)
            {
                var go = new GameObject("SkillsPanel", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                go.transform.SetParent(card, false);
                skillsT = go.transform as RectTransform;
            }
            skillsT.anchorMin = new Vector2(0.5f, 1.0f);
            skillsT.anchorMax = new Vector2(0.5f, 1.0f);
            skillsT.pivot = new Vector2(0.5f, 1.0f);
            skillsT.anchoredPosition = new Vector2(0f, -655f);
            skillsT.sizeDelta = new Vector2(640f, 165f);
            var shlg = skillsT.GetComponent<HorizontalLayoutGroup>();
            shlg.spacing = 30f;
            shlg.childAlignment = TextAnchor.UpperCenter;
            shlg.childControlWidth = false;
            shlg.childControlHeight = false;

            // Clear old children of SkillsPanel
            while (skillsT.childCount < 3)
            {
                var slotGo = new GameObject("SkillSlot_" + skillsT.childCount, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(KeySkillBadgeUI));
                slotGo.transform.SetParent(skillsT, false);
            }

            var tooltip = Object.FindObjectOfType<SkillTooltip>(true);
            var skillBadges = new List<KeySkillBadgeUI>();

            for (int i = 0; i < 3; i++)
            {
                var slot = skillsT.GetChild(i) as RectTransform;
                slot.sizeDelta = new Vector2(130f, 160f);

                var slotFrameImg = slot.GetComponent<Image>();
                slotFrameImg.sprite = skillFrame;
                slotFrameImg.type = Image.Type.Sliced;
                slotFrameImg.color = new Color(0.85f, 0.70f, 0.35f, 1.0f);

                // Inner Icon
                var iconT = slot.Find("Image") as RectTransform;
                if (iconT == null)
                {
                    var go = new GameObject("Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    go.transform.SetParent(slot, false);
                    iconT = go.transform as RectTransform;
                }
                iconT.anchorMin = new Vector2(0.5f, 0.5f);
                iconT.anchorMax = new Vector2(0.5f, 0.5f);
                iconT.anchoredPosition = new Vector2(0f, 12f);
                iconT.sizeDelta = new Vector2(82f, 82f);
                var iconImg = iconT.GetComponent<Image>();
                var skillIcon = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Bladehold/Art/Skill Icons/{skills[i].iconName}.png");
                if (skillIcon == null)
                {
                    var found = AssetDatabase.FindAssets($"t:Sprite {skills[i].iconName}");
                    if (found.Length > 0)
                        skillIcon = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(found[0]));
                }
                iconImg.sprite = skillIcon;
                iconImg.color = Color.white;
                iconImg.preserveAspect = true;

                // Skill Name Text
                var nameT = slot.Find("SkillName") as RectTransform;
                if (nameT == null)
                {
                    var go = new GameObject("SkillName", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                    go.transform.SetParent(slot, false);
                    nameT = go.transform as RectTransform;
                }
                nameT.anchorMin = new Vector2(0.5f, 0.0f);
                nameT.anchorMax = new Vector2(0.5f, 0.0f);
                nameT.pivot = new Vector2(0.5f, 0.0f);
                nameT.anchoredPosition = new Vector2(0f, -42f);
                nameT.sizeDelta = new Vector2(160f, 48f);
                var nameTxt = nameT.GetComponent<TextMeshProUGUI>();
                nameTxt.text = skills[i].name;
                if (bodyFont != null) nameTxt.font = bodyFont;
                nameTxt.fontSize = 21f;
                nameTxt.color = new Color(0.92f, 0.88f, 0.80f, 1.0f);
                nameTxt.alignment = TextAlignmentOptions.Top;

                var badgeUI = slot.GetComponent<KeySkillBadgeUI>();
                badgeUI.Setup(skills[i].name, skills[i].desc, skillIcon, tooltip);
                skillBadges.Add(badgeUI);
            }

            // 10. Description Panel (Ornate Plaque)
            var descPanelT = card.Find("DescriptionPanel") as RectTransform;
            if (descPanelT == null)
            {
                var go = new GameObject("DescriptionPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(card, false);
                descPanelT = go.transform as RectTransform;
            }
            descPanelT.anchorMin = new Vector2(0.5f, 1.0f);
            descPanelT.anchorMax = new Vector2(0.5f, 1.0f);
            descPanelT.pivot = new Vector2(0.5f, 1.0f);
            descPanelT.anchoredPosition = new Vector2(0f, -870f);
            descPanelT.sizeDelta = new Vector2(700f, 235f);
            var descBg = descPanelT.GetComponent<Image>();
            descBg.sprite = plaqueFrame != null ? plaqueFrame : bgSprite;
            descBg.type = Image.Type.Sliced;
            descBg.color = new Color(0.06f, 0.09f, 0.14f, 0.98f);

            var descHeader = descPanelT.Find("Header") as RectTransform;
            if (descHeader == null)
            {
                var go = new GameObject("Header", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                go.transform.SetParent(descPanelT, false);
                descHeader = go.transform as RectTransform;
            }
            descHeader.anchorMin = new Vector2(0.5f, 1.0f);
            descHeader.anchorMax = new Vector2(0.5f, 1.0f);
            descHeader.pivot = new Vector2(0.5f, 1.0f);
            descHeader.anchoredPosition = new Vector2(0f, -18f);
            descHeader.sizeDelta = new Vector2(600f, 36f);
            var headerTxt = descHeader.GetComponent<TextMeshProUGUI>();
            headerTxt.text = "CLASS DESCRIPTION";
            if (titleFont != null) headerTxt.font = titleFont;
            headerTxt.fontSize = 24f;
            headerTxt.fontStyle = FontStyles.Bold;
            headerTxt.color = new Color(0.92f, 0.78f, 0.40f, 1.0f);
            headerTxt.alignment = TextAlignmentOptions.Center;

            var descBody = descPanelT.Find("DescriptionText") as RectTransform;
            if (descBody == null)
            {
                var go = new GameObject("DescriptionText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                go.transform.SetParent(descPanelT, false);
                descBody = go.transform as RectTransform;
            }
            descBody.anchorMin = new Vector2(0.5f, 0.5f);
            descBody.anchorMax = new Vector2(0.5f, 0.5f);
            descBody.pivot = new Vector2(0.5f, 0.5f);
            descBody.anchoredPosition = new Vector2(0f, -16f);
            descBody.sizeDelta = new Vector2(640f, 155f);
            var bodyTxt = descBody.GetComponent<TextMeshProUGUI>();
            bodyTxt.text = description;
            if (titleFont != null) bodyTxt.font = titleFont;
            bodyTxt.fontSize = 21f;
            bodyTxt.color = new Color(0.88f, 0.85f, 0.80f, 1.0f);
            bodyTxt.alignment = TextAlignmentOptions.Center;
            bodyTxt.enableWordWrapping = true;
            bodyTxt.lineSpacing = 12f;

            descPanelT.gameObject.SetActive(isSelected);

            // 11. Wire Card Component
            var cardUI = card.GetComponent<CharacterSelectCardUI>();
            if (cardUI != null)
            {
                var so = new SerializedObject(cardUI);
                so.Update();
                so.FindProperty("classId").stringValue = classId;
                so.FindProperty("className").stringValue = title;
                so.FindProperty("roleSubtitle").stringValue = role;
                so.FindProperty("healthValueString").stringValue = healthVal;
                so.FindProperty("portraitSprite").objectReferenceValue = portrait;
                so.FindProperty("classDescription").stringValue = description;

                so.FindProperty("classTitleLabel").objectReferenceValue = titleTxt;
                so.FindProperty("roleSubtitleLabel").objectReferenceValue = roleTxt;
                so.FindProperty("healthTextLabel").objectReferenceValue = healthTxt;
                so.FindProperty("portraitImage").objectReferenceValue = portraitImg;
                so.FindProperty("selectedBadge").objectReferenceValue = badgeT.gameObject;
                so.FindProperty("selectedGlowBorder").objectReferenceValue = glowBorderT.gameObject;
                so.FindProperty("normalBorder").objectReferenceValue = normalBorderT.gameObject;
                so.FindProperty("selectedBackGlow").objectReferenceValue = backGlowT.gameObject;
                so.FindProperty("descriptionPanel").objectReferenceValue = descPanelT.gameObject;
                so.FindProperty("descriptionLabel").objectReferenceValue = bodyTxt;

                so.FindProperty("selectedSizeDelta").vector2Value = new Vector2(815f, 1330f);
                so.FindProperty("normalSizeDelta").vector2Value = new Vector2(760f, 1140f);
                so.FindProperty("selectedScale").floatValue = 1.00f;
                so.FindProperty("normalScale").floatValue = 1.00f;

                var badgesProp = so.FindProperty("cardSkillBadges");
                badgesProp.arraySize = skillBadges.Count;
                for (int b = 0; b < skillBadges.Count; b++)
                {
                    badgesProp.GetArrayElementAtIndex(b).objectReferenceValue = skillBadges[b];
                }

                so.ApplyModifiedProperties();
                cardUI.RefreshCardVisuals();
                cardUI.SetSelected(isSelected, immediate: true);
            }
        }
    }
}
#endif
