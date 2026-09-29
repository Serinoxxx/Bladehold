using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
///     Builds everything Captain Mogra Hexfang needs that isn't code, idempotently:
///     <list type="number">
///         <item>green VFX variants and materials,</item>
///         <item>the spell prefabs (hex bolt, rune blast, bone totem, shockwave ring, ritual markers) with their MMF feedbacks,</item>
///         <item>the from-scratch <c>Mogra AC</c> animator controller,</item>
///         <item>the <c>captain_mogra</c> enemy variant (via the manifest), then the Goblin Shaman model swap, a staff, the feedback players and the ritual cast bar,</item>
///         <item>the Player AC's Dodge layer (the dash animation).</item>
///     </list>
///     Menu: Bladehold/Captains/Build Captain Mogra Assets. Re-running rebuilds the generated prefabs
///     and the controller, so hand-tuning belongs on <c>CaptainMograSO</c>, not on these assets.
/// </summary>
public static class CaptainMograBuilder
{
    public const string Folder = "Assets/Bladehold/Bladehold Prefabs/Captains/Mogra";
    public const string VfxFolder = Folder + "/VFX";
    public const string MaterialFolder = Folder + "/Materials";
    public const string AnimFolder = "Assets/Bladehold/Bladehold Animations/Mogra";
    public const string ControllerPath = AnimFolder + "/Mogra AC.controller";

    public const string BoltPrefabPath = Folder + "/HexBolt.prefab";
    public const string RuneBlastPrefabPath = Folder + "/HexRuneBlast.prefab";
    public const string TotemPrefabPath = Folder + "/BoneTotem.prefab";
    public const string RingPrefabPath = Folder + "/HexShockwaveRing.prefab";
    public const string SafeCirclePrefabPath = Folder + "/RitualSafeCircle.prefab";
    public const string DangerPrefabPath = Folder + "/RitualDanger.prefab";

    public const string VariantPath = "Assets/Bladehold/Bladehold Prefabs/Captain Mogra Enemy Variant.prefab";
    public const string SoPath = "Assets/Bladehold/Bladehold Scripts/Enemies/Captain/CaptainMograSO.asset";

    private const string HexBurstPath = VfxFolder + "/HexBurst.prefab";
    private const string HexBurstLargePath = VfxFolder + "/HexBurstLarge.prefab";
    private const string HexDazePath = VfxFolder + "/HexDaze.prefab";
    private const string GlowMaterialPath = MaterialFolder + "/Hex Glow.mat";
    private const string FillMaterialPath = MaterialFolder + "/Hex Fill.mat";

    private const string PlayerControllerPath = "Assets/Third Party/Synty/AnimationBaseLocomotion/Animations/Sidekick/Player AC.controller";
    private const string ShamanModelPath = "Assets/Synty/Goblins/SM_Chr_Goblin_Shaman_01/SM_Chr_Goblin_Shaman_01.prefab";
    private const string GoblinBasePrefabPath = "Assets/Bladehold/Bladehold Prefabs/Goblin Enemy (Base).prefab";
    private const string StaffPath = "Assets/Synty/Weapons/SM_Wep_Staff_03/SM_Wep_Staff_03.prefab";
    private const string CastBarPrefabPath = "Assets/Bladehold/Bladehold Prefabs/UI/BossCastBar.prefab";
    private const string LineMaterialPath = "Assets/Bladehold/Materials/Mockup/Mockup_TelegraphLine.mat";
    private const string MarkerGreenPath = "Assets/Third Party/GabrielAguiarProductions/UniqueMarkersPointersVol_1/Prefabs/Markers/vfx_RoundMarker02_Green.prefab";
    private const string MarkerBluePath = "Assets/Third Party/GabrielAguiarProductions/UniqueMarkersPointersVol_1/Prefabs/Markers/vfx_RoundMarker02_Blue.prefab";

    private const string SoundTemplatePrefab = "Assets/Bladehold/Bladehold Prefabs/Bosses/NecromancerSweepMMF.prefab";
    private const string ParticleTemplatePrefab = "Assets/Bladehold/Bladehold Prefabs/Bosses/NecromancerSummonMMF.prefab";
    private const string ImpulseTemplatePrefab = "Assets/Bladehold/Bladehold Prefabs/Bosses/NecromancerShatterMMF.prefab";

    private const string SfxLaugh = "Assets/Bladehold/Audio/Enemies/Goblin/goblin_fairy_laugh_angry_02.wav";
    private const string SfxChant = "Assets/Bladehold/Audio/Enemies/Goblin/goblin_fairy_attack_10.wav";
    private const string SfxWhimper = "Assets/Bladehold/Audio/Enemies/Goblin/goblin_fairy_cry_whimper_high_02.wav";
    private const string SfxFizzle = "Assets/Bladehold/Audio/Attacks/Fantasy_Game_Magic_Fuse_Fizzle_Gas.wav";
    private const string SfxPoof = "Assets/Bladehold/Audio/Attacks/Fantasy_Game_Magic_Organic_Poof_Buff_Hit_5.wav";
    private const string SfxBlastShort = "Assets/Bladehold/Audio/Attacks/EXPLOSION_Short_04_mono.wav";
    private const string SfxBlastLarge = "Assets/Bladehold/Audio/Enemies/Bomber/explosion_large_01.wav";
    private const string SfxSizzleLoop = "Assets/Bladehold/Audio/Attacks/Fantasy_Game_Loops_Acid Pool_1_Sizzle_Liquid_Danger_Hazard.wav";
    private const string SfxWhooshBig = "Assets/Bladehold/Bladehold Audio/SFX/Wooshes/whoosh_swish_high_big_01.wav";
    private const string SfxWhooshDeep = "Assets/Bladehold/Bladehold Audio/SFX/Wooshes/whoosh_slow_deep_06.wav";
    private const string SfxUgh = "Assets/Bladehold/Audio/Enemies/DemonUndead/MONSTER_Ugh_03_mono.wav";
    private const string MograSfx = "Assets/Bladehold/Audio/Enemies/Mogra/";
    private const string SfxBoltWindup = MograSfx + "PoisonAcid_Warmup_Short_1_M.wav";
    private const string SfxBoltRelease = MograSfx + "Fantasy_Game_Magic_Dark Magic_1_Cast_Shadow_Warlock_Spell.wav";
    private const string SfxBoltHit = MograSfx + "PoisonAcid_Hit_1_M.wav";
    private const string SfxRuneWindup = MograSfx + "Fantasy_Game_Magic_Action_Rune_A.wav";
    private const string SfxRuneRelease = MograSfx + "Fantasy_Game_Magic_Dark Magic_4_Cast_Shadow_Warlock_Spell.wav";
    private const string SfxRuneErupt = MograSfx + "PoisonAcid_Explosion_1_M.wav";
    private const string SfxSummonCast = MograSfx + "Fantasy_Game_Magic_Earth_Long_Cast_Spell_A.wav";
    private const string SfxBlinkCast = MograSfx + "Fantasy_Game_Magic_Dark Magic_3_Cast_Shadow_Warlock_Spell.wav";
    private const string SfxRitualStart = MograSfx + "PoisonAcid_Warmup_Long_1_M.wav";
    private const string SfxRitualBlast = MograSfx + "Fantasy_Game_Magic_Dark Magic_2_Blast_Shadow_Warlock_Spell.wav";
    private const string SfxTotemCharge = MograSfx + "PoisonAcid_Warmup_Short_2_M.wav";
    private const string SfxTotemDischarge = MograSfx + "PoisonWarmupExplosion_0.wav";
    private const string MograHighlightProfilePath = "Assets/Mogra Outline.asset";

    private static readonly Color HexGreen = new Color(0.35f, 1f, 0.25f, 1f);

    [MenuItem("Bladehold/Captains/Build Captain Mogra Assets")]
    public static void BuildAll()
    {
        EnsureFolder(Folder);
        EnsureFolder(VfxFolder);
        EnsureFolder(MaterialFolder);
        EnsureFolder(AnimFolder);

        Material glow = EnsureUnlitMaterial(GlowMaterialPath, HexGreen, transparent: false);
        Material fill = EnsureUnlitMaterial(FillMaterialPath, new Color(0.35f, 1f, 0.25f, 0.28f), transparent: true);

        BuildTintedVfx("Assets/Synty/PolygonParticleFX/Prefabs/FX_MagicBlast_01.prefab", HexBurstPath, 1f);
        BuildTintedVfx("Assets/Synty/PolygonParticleFX/Prefabs/FX_MagicBlast_02.prefab", HexBurstLargePath, 2f);
        BuildTintedVfx("Assets/Synty/PolygonParticleFX/Prefabs/FX_Sparkle_Orbit_01.prefab", HexDazePath, 1.3f);
        AssetDatabase.SaveAssets();

        BuildBoltPrefab(glow);
        BuildRuneBlastPrefab(fill);
        BuildRingPrefab();
        BuildMarkerPrefab(SafeCirclePrefabPath, "RitualSafeCircle", MarkerBluePath);
        BuildMarkerPrefab(DangerPrefabPath, "RitualDanger", MarkerGreenPath);
        BuildTotemPrefab(glow);
        AssetDatabase.SaveAssets();

        BuildMograController();
        AssetDatabase.SaveAssets();

        EnemyPrefabGenerator.GenerateById("captain_mogra");
        FillSoPrefabRefs();
        ConfigureVariant();
        AddPlayerDodgeLayer();

        AssetDatabase.SaveAssets();
        Debug.Log("[CaptainMograBuilder] Captain Mogra assets built.");
    }

    // ------------------------------------------------------------------ materials & VFX

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    private static Material EnsureUnlitMaterial(string path, Color color, bool transparent)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        if (transparent)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>A green copy of a non-looping Synty burst, scaled. Particle Instantiation feedbacks spawn these.</summary>
    private static void BuildTintedVfx(string sourcePath, string targetPath, float scale)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if (source == null) throw new InvalidOperationException($"VFX source missing: {sourcePath}");
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        try
        {
            instance.name = System.IO.Path.GetFileNameWithoutExtension(targetPath);
            instance.transform.localScale = Vector3.one * scale;
            foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                main.loop = false;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                Color original = main.startColor.color;
                main.startColor = new Color(HexGreen.r, HexGreen.g, HexGreen.b, original.a <= 0f ? 1f : original.a);
                ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
                if (col.enabled)
                {
                    Gradient g = new Gradient();
                    Gradient src = col.color.gradient;
                    GradientAlphaKey[] alphas = src != null ? src.alphaKeys : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) };
                    g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alphas);
                    col.color = new ParticleSystem.MinMaxGradient(g);
                }
            }
            PrefabUtility.SaveAsPrefabAsset(instance, targetPath);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    // ------------------------------------------------------------------ MMF helpers

    private static T Template<T>(string prefabPath) where T : MMF_Feedback
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) throw new InvalidOperationException($"MMF template prefab missing: {prefabPath}");
        foreach (MMF_Player p in prefab.GetComponentsInChildren<MMF_Player>(true))
        {
            if (p.FeedbacksList == null) continue;
            foreach (MMF_Feedback f in p.FeedbacksList)
            {
                if (f is T typed) return typed;
            }
        }
        throw new InvalidOperationException($"No {typeof(T).Name} in {prefabPath}");
    }

    private static T Clone<T>(T source) where T : MMF_Feedback
    {
        T clone = (T)JsonUtility.FromJson(JsonUtility.ToJson(source), source.GetType());
        clone.UniqueID = Guid.NewGuid().GetHashCode();
        return clone;
    }

    /// <summary>A child GameObject holding a fresh MMF_Player (replaced if one of that name exists).</summary>
    private static MMF_Player NewPlayer(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        MMF_Player player = go.AddComponent<MMF_Player>();
        player.FeedbacksList = new List<MMF_Feedback>();
        return player;
    }

    private static void AddSound(MMF_Player player, string clipPath, float volume, bool loop = false)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
        if (clip == null) throw new InvalidOperationException($"Audio clip missing: {clipPath}");
        MMF_MMSoundManagerSound sound = Clone(Template<MMF_MMSoundManagerSound>(SoundTemplatePrefab));
        sound.Sfx = clip;
        sound.MinVolume = volume;
        sound.MaxVolume = volume;
        sound.Loop = loop;
        sound.StopSoundOnFeedbackStop = loop;
        sound.MaximumConcurrentInstances = 3;
        sound.Label = "Sound " + clip.name;
        player.FeedbacksList.Add(sound);
    }

    private static void AddParticles(MMF_Player player, string vfxPath)
    {
        GameObject vfx = AssetDatabase.LoadAssetAtPath<GameObject>(vfxPath);
        ParticleSystem ps = vfx != null ? vfx.GetComponent<ParticleSystem>() : null;
        if (ps == null) throw new InvalidOperationException($"VFX with a root ParticleSystem missing: {vfxPath}");
        MMF_ParticlesInstantiation particles = Clone(Template<MMF_ParticlesInstantiation>(ParticleTemplatePrefab));
        particles.ParticlesPrefab = ps;
        particles.Mode = MMF_ParticlesInstantiation.Modes.OnDemand;
        particles.CachedRecycle = false;
        particles.PositionMode = MMF_ParticlesInstantiation.PositionModes.Script;
        particles.NestParticles = false;
        particles.ForceStopAction = true;
        particles.StopAction = ParticleSystemStopAction.Destroy;
        particles.Label = "Particles " + vfx.name;
        player.FeedbacksList.Add(particles);
    }

    private static void AddImpulse(MMF_Player player)
    {
        MMF_Feedback impulse = null;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ImpulseTemplatePrefab);
        foreach (MMF_Player p in prefab.GetComponentsInChildren<MMF_Player>(true))
        {
            foreach (MMF_Feedback f in p.FeedbacksList)
            {
                if (f.GetType().Name.Contains("CinemachineImpulse")) impulse = f;
            }
        }
        if (impulse == null) throw new InvalidOperationException($"No Cinemachine impulse in {ImpulseTemplatePrefab}");
        player.FeedbacksList.Add(Clone(impulse));
    }

    // ------------------------------------------------------------------ spell prefabs

    private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        MeshRenderer r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    private static GameObject NestPrefab(string path, Transform parent, string name, Vector3 localPos, float scale)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException($"Prefab missing: {path}");
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one * scale;
        return go;
    }

    private static void Save(GameObject root, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    private static void SetRef(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null) throw new InvalidOperationException($"{target.GetType().Name} has no serialized field '{field}'.");
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildBoltPrefab(Material glow)
    {
        GameObject root = new GameObject("HexBolt");
        SphereCollider col = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.35f;
        Rigidbody body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        HexBolt bolt = root.AddComponent<HexBolt>();

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        Primitive(PrimitiveType.Sphere, "Core", visual.transform, glow).transform.localScale = Vector3.one * 0.35f;
        NestPrefab("Assets/Synty/PolygonParticleFX/Prefabs/FX_Poison_Green_01.prefab", visual.transform, "Trail", Vector3.zero, 0.35f);

        MMF_Player impact = NewPlayer(root.transform, "ImpactMMF");
        AddParticles(impact, HexBurstPath);
        AddSound(impact, SfxBoltHit, 0.7f);

        SetRef(bolt, "body", body);
        SetRef(bolt, "impactFeedback", impact);
        SetRef(bolt, "visualRoot", visual);
        Save(root, BoltPrefabPath);
    }

    private static void BuildRuneBlastPrefab(Material fill)
    {
        GameObject root = new GameObject("HexRuneBlast");
        HexRuneBlast blast = root.AddComponent<HexRuneBlast>();

        GameObject telegraph = new GameObject("Telegraph");
        telegraph.transform.SetParent(root.transform, false);
        NestPrefab(MarkerGreenPath, telegraph.transform, "Marker", Vector3.zero, 0.1f);

        GameObject fillDisc = Primitive(PrimitiveType.Cylinder, "Fill", root.transform, fill);
        fillDisc.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        fillDisc.transform.localScale = new Vector3(0f, 0.01f, 0f);

        MMF_Player erupt = NewPlayer(root.transform, "EruptMMF");
        AddParticles(erupt, HexBurstPath);
        AddSound(erupt, SfxRuneErupt, 0.35f);

        SetRef(blast, "telegraphVisual", telegraph.transform);
        SetRef(blast, "fillVisual", fillDisc.transform);
        SetRef(blast, "eruptFeedback", erupt);
        Save(root, RuneBlastPrefabPath);
    }

    private static void BuildRingPrefab()
    {
        GameObject root = new GameObject("HexShockwaveRing");
        LineRenderer line = root.AddComponent<LineRenderer>();
        line.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(LineMaterialPath);
        line.startColor = new Color(HexGreen.r, HexGreen.g, HexGreen.b, 0.95f);
        line.endColor = line.startColor;
        line.numCapVertices = 0;
        line.alignment = LineAlignment.TransformZ;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        root.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        HexShockwaveRing ring = root.AddComponent<HexShockwaveRing>();
        SetRef(ring, "line", line);
        Save(root, RingPrefabPath);
    }

    private static void BuildMarkerPrefab(string path, string name, string markerPath)
    {
        GameObject root = new GameObject(name);
        NestPrefab(markerPath, root.transform, "Marker", Vector3.zero, 0.1f);
        Save(root, path);
    }

    private static void BuildTotemPrefab(Material glow)
    {
        GameObject root = new GameObject("BoneTotem");
        root.layer = LayerMask.NameToLayer("Enemy");
        Health health = root.AddComponent<Health>();
        BoneTotem totem = root.AddComponent<BoneTotem>();
        CapsuleCollider col = root.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 1.1f, 0f);
        col.height = 2.2f;
        col.radius = 0.45f;

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        GameObject spine = NestPrefab("Assets/Synty/PolygonDungeon/Prefabs/Environments/Bones/SM_Env_Bone_Spike_02.prefab", visual.transform, "Spike", Vector3.zero, 1f);
        FitHeight(spine, 1.9f);
        GameObject skull = NestPrefab("Assets/Synty/PolygonDungeon/Prefabs/Environments/Bones/SM_Env_Bone_Skull_01.prefab", visual.transform, "Skull", new Vector3(0f, 1.9f, 0f), 1f);
        FitHeight(skull, 0.45f);
        Primitive(PrimitiveType.Sphere, "Eye Glow", visual.transform, glow).transform.localPosition = new Vector3(0f, 2.1f, 0f);
        visual.transform.Find("Eye Glow").localScale = Vector3.one * 0.18f;
        NestPrefab("Assets/Synty/PolygonParticleFX/Prefabs/FX_Poison_Green_01.prefab", visual.transform, "Hex Smoke", new Vector3(0f, 2.05f, 0f), 0.5f);
        foreach (Transform t in visual.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = root.layer;
            foreach (Collider c in t.GetComponents<Collider>()) Object.DestroyImmediate(c);
        }

        GameObject ringOrigin = new GameObject("Ring Origin");
        ringOrigin.transform.SetParent(root.transform, false);
        ringOrigin.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        GameObject tetherAnchor = new GameObject("Tether Anchor");
        tetherAnchor.transform.SetParent(root.transform, false);
        tetherAnchor.transform.localPosition = new Vector3(0f, 2.1f, 0f);

        GameObject tetherGo = new GameObject("Tether");
        tetherGo.transform.SetParent(root.transform, false);
        LineRenderer tether = tetherGo.AddComponent<LineRenderer>();
        tether.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(LineMaterialPath);
        tether.startColor = new Color(HexGreen.r, HexGreen.g, HexGreen.b, 0.8f);
        tether.endColor = new Color(HexGreen.r, HexGreen.g, HexGreen.b, 0.35f);
        tether.widthMultiplier = 0.08f;
        tether.positionCount = 2;
        tether.useWorldSpace = true;
        tether.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        MMF_Player rise = NewPlayer(root.transform, "RiseMMF");
        AddParticles(rise, HexBurstLargePath);
        AddSound(rise, SfxWhooshDeep, 0.6f);
        MMF_Player charge = NewPlayer(root.transform, "ChargeMMF");
        AddSound(charge, SfxTotemCharge, 0.8f);
        MMF_Player pulse = NewPlayer(root.transform, "PulseMMF");
        AddSound(pulse, SfxTotemDischarge, 0.75f);
        AddSound(pulse, SfxWhooshDeep, 0.4f);

        // Charge-up glow (Mogra's own highlight profile) and a green light ramped by BoneTotem.
        HighlightPlus.HighlightEffect chargeGlow = root.AddComponent<HighlightPlus.HighlightEffect>();
        HighlightPlus.HighlightProfile profile = AssetDatabase.LoadAssetAtPath<HighlightPlus.HighlightProfile>(MograHighlightProfilePath);
        if (profile != null)
        {
            chargeGlow.profile = profile;
            chargeGlow.ProfileLoad(profile);
        }
        else
        {
            Debug.LogWarning($"[CaptainMograBuilder] {MograHighlightProfilePath} not found; the totem glow uses HighlightPlus defaults.");
        }
        chargeGlow.highlighted = false;
        GameObject lightGo = new GameObject("Charge Light");
        lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 2f, 0f);
        Light chargeLight = lightGo.AddComponent<Light>();
        chargeLight.type = LightType.Point;
        chargeLight.color = HexGreen;
        chargeLight.range = 5f;
        chargeLight.intensity = 0f;
        chargeLight.shadows = LightShadows.None;
        chargeLight.enabled = false;
        MMF_Player brk = NewPlayer(root.transform, "BreakMMF");
        AddParticles(brk, HexBurstLargePath);
        AddSound(brk, SfxPoof, 0.8f);

        SetRef(totem, "health", health);
        SetRef(totem, "ringOrigin", ringOrigin.transform);
        SetRef(totem, "tetherAnchor", tetherAnchor.transform);
        SetRef(totem, "tether", tether);
        SetRef(totem, "visualRoot", visual);
        SetRef(totem, "riseFeedback", rise);
        SetRef(totem, "chargeFeedback", charge);
        SetRef(totem, "pulseFeedback", pulse);
        SetRef(totem, "chargeGlow", chargeGlow);
        SetRef(totem, "chargeLight", chargeLight);
        SetRef(totem, "breakFeedback", brk);
        Save(root, TotemPrefabPath);
    }

    /// <summary>Uniformly scales a nested model so its renderer bounds are <paramref name="height" /> tall.</summary>
    private static void FitHeight(GameObject go, float height)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        if (b.size.y <= 0.001f) return;
        go.transform.localScale *= height / b.size.y;
    }

    // ------------------------------------------------------------------ animator

    private static AnimationClip LoadClip(string fileName, string clipName = null)
    {
        foreach (string guid in AssetDatabase.FindAssets(fileName))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!System.IO.Path.GetFileNameWithoutExtension(path).Equals(fileName, StringComparison.OrdinalIgnoreCase)) continue;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__") && (clipName == null || clip.name == clipName)) return clip;
            }
        }
        throw new InvalidOperationException($"Animation clip '{clipName ?? fileName}' not found in '{fileName}'.");
    }

    /// <summary>
    ///     Mogra's own controller, built from scratch: a MoveSpeed idle/walk/run blend, one-shot casts on
    ///     triggers, bool-held channel and stun loops, Death and Cheer (AIAnimation), and every parameter
    ///     the shared enemy scripts write (LocomotionAnimator, KnockbackReceiver) so none of them warn.
    /// </summary>
    private static void BuildMograController()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null) AssetDatabase.DeleteAsset(ControllerPath);
        AnimatorController ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        string[] floats = { "MoveSpeed", "CurrentGait", "StrafeDirectionX", "StrafeDirectionZ", "ShuffleDirectionX", "ShuffleDirectionZ", "ForwardStrafe", "LeanValue", "LocomotionStartDirection", "InclineAngle" };
        string[] bools = { "IsStopped", "IsStarting", "IsStrafing", "IsWalking", "IsCrouching", "IsGrounded", "IsTurningInPlace", "Channeling", "Staggered" };
        string[] triggers = { "Death", "Cheer", "Attack", "Knockdown", "Stagger", "Taunt", "CastBolt", "CastRunes", "Summon", "Blink", "Channel", "Stun" };
        foreach (string f in floats) ac.AddParameter(f, AnimatorControllerParameterType.Float);
        foreach (string b in bools) ac.AddParameter(b, AnimatorControllerParameterType.Bool);
        foreach (string t in triggers) ac.AddParameter(t, AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = ac.layers[0].stateMachine;

        BlendTree blend;
        AnimatorState locomotion = ac.CreateBlendTreeInController("Locomotion", out blend, 0);
        blend.blendType = BlendTreeType.Simple1D;
        blend.blendParameter = "MoveSpeed";
        blend.useAutomaticThresholds = false;
        blend.AddChild(LoadClip("A_MOD_GBL_Idle_Fidget_Menacing_Neut"), 0f);
        blend.AddChild(LoadClip("A_MOD_GBL_Walk_F_Neut"), 1.6f);
        blend.AddChild(LoadClip("A_MOD_GBL_Run_F_Neut"), 4f);
        sm.defaultState = locomotion;

        AnimatorState castBolt = OneShot(sm, locomotion, "CastBolt", LoadClip("Sorceress@RangeAttack1"), 1.6f);
        AnimatorState castRunes = OneShot(sm, locomotion, "CastRunes", LoadClip("Sorceress@SpecialAttack1"), 1.5f);
        AnimatorState summon = OneShot(sm, locomotion, "Summon", LoadClip("Sorceress@SpecialAttack3"), 1f);
        AnimatorState blink = OneShot(sm, locomotion, "Blink", LoadClip("Sorceress@DashBackward"), 2f);
        AnimatorState taunt = OneShot(sm, locomotion, "Taunt", LoadClip("A_MOD_EMOT_Aggressive_Roar_High_Masc"), 2.3f);
        AnimatorState cheer = OneShot(sm, locomotion, "Cheer", LoadClip("A_MOD_EMOT_Celebrate_BeatChest_Masc"), 1f);

        // Channel: start → loop while Channeling → end.
        AnimatorState channelStart = sm.AddState("ChannelStart");
        channelStart.motion = LoadClip("Sorceress@SpecialAttack2", "SpecialAttack2_start");
        AnimatorState channelLoop = sm.AddState("ChannelLoop");
        channelLoop.motion = LoadClip("Sorceress@SpecialAttack2", "SpecialAttack2_loop");
        AnimatorState channelEnd = sm.AddState("ChannelEnd");
        channelEnd.motion = LoadClip("Sorceress@SpecialAttack2", "SpecialAttack2_end");
        AnyTrigger(sm, channelStart, "Channel", 0.15f);
        ExitAfter(channelStart, channelLoop, 1f, 0.05f);
        AnimatorStateTransition loopOut = channelLoop.AddTransition(channelEnd);
        loopOut.hasExitTime = false;
        loopOut.duration = 0.1f;
        loopOut.AddCondition(AnimatorConditionMode.IfNot, 0f, "Channeling");
        ExitAfter(channelEnd, locomotion, 0.85f, 0.2f);

        // Stun: loops while Staggered.
        AnimatorState stun = sm.AddState("Stunned");
        stun.motion = LoadClip("Sorceress@Stunned");
        AnyTrigger(sm, stun, "Stun", 0.1f);
        AnimatorStateTransition stunOut = stun.AddTransition(locomotion);
        stunOut.hasExitTime = false;
        stunOut.duration = 0.25f;
        stunOut.AddCondition(AnimatorConditionMode.IfNot, 0f, "Staggered");

        AnimatorState death = sm.AddState("Death");
        death.motion = LoadClip("A_MOD_SWD_Death_B_Neut");
        AnimatorStateTransition toDeath = AnyTrigger(sm, death, "Death", 0.15f);

        // Death wins over everything; the stun beats casts.
        List<AnimatorStateTransition> any = new List<AnimatorStateTransition>(sm.anyStateTransitions);
        any.Remove(toDeath);
        any.Insert(0, toDeath);
        sm.anyStateTransitions = any.ToArray();

        foreach (ChildAnimatorState child in sm.states) child.state.writeDefaultValues = true;
        EditorUtility.SetDirty(ac);
    }

    private static AnimatorState OneShot(AnimatorStateMachine sm, AnimatorState back, string trigger, AnimationClip clip, float speed)
    {
        AnimatorState state = sm.AddState(trigger);
        state.motion = clip;
        state.speed = speed;
        AnyTrigger(sm, state, trigger, 0.12f);
        ExitAfter(state, back, 0.9f, 0.2f);
        return state;
    }

    private static AnimatorStateTransition AnyTrigger(AnimatorStateMachine sm, AnimatorState state, string trigger, float duration)
    {
        AnimatorStateTransition t = sm.AddAnyStateTransition(state);
        t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        t.hasExitTime = false;
        t.hasFixedDuration = true;
        t.duration = duration;
        t.canTransitionToSelf = false;
        return t;
    }

    private static void ExitAfter(AnimatorState from, AnimatorState to, float exitTime, float duration)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = true;
        t.exitTime = exitTime;
        t.hasFixedDuration = true;
        t.duration = duration;
    }

    // ------------------------------------------------------------------ variant

    private static void FillSoPrefabRefs()
    {
        CaptainMograSO data = AssetDatabase.LoadAssetAtPath<CaptainMograSO>(SoPath);
        if (data == null) throw new InvalidOperationException($"CaptainMograSO not found at {SoPath} (the generator should have created it).");
        if (data.boltPrefab == null) data.boltPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BoltPrefabPath);
        if (data.runeBlastPrefab == null) data.runeBlastPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RuneBlastPrefabPath);
        if (data.totemPrefab == null) data.totemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TotemPrefabPath);
        if (data.shockwaveRingPrefab == null) data.shockwaveRingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RingPrefabPath);
        if (data.ritualSafeCirclePrefab == null) data.ritualSafeCirclePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SafeCirclePrefabPath);
        if (data.ritualDangerPrefab == null) data.ritualDangerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DangerPrefabPath);
        EditorUtility.SetDirty(data);
    }

    private static void ConfigureVariant()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(VariantPath);
        try
        {
            Animator animator = root.GetComponentInChildren<Animator>(true);
            if (animator == null) throw new InvalidOperationException("Mogra variant has no Animator.");
            animator.applyRootMotion = false;

            // Model: Lance's Sidekick character (Bladehold Sidekick Characters/Mogra) replaced the goblin rig
            // (2026-09-29). Only the old Goblin Shaman swap fallback remains, for a variant still on the goblin rig.
            bool onGoblinRig = animator.name == "SidekickSyntyCharacter";
            ModelSwapRecord record = root.GetComponent<ModelSwapRecord>();
            if (!onGoblinRig && record != null)
            {
                // The record described a swap onto the old goblin rig, which is gone.
                Object.DestroyImmediate(record);
            }
            else if (onGoblinRig && record == null)
            {
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ShamanModelPath);
                if (model == null) throw new InvalidOperationException($"Goblin Shaman model missing: {ShamanModelPath}");
                List<string> swapped = new List<string>();
                int count = ModelSwapUtility.Swap(animator, model, deleteOldRenderers: false, "Mogra model", swapped);
                if (count == 0) throw new InvalidOperationException("The Goblin Shaman model didn't bind to the goblin rig.");
                record = root.AddComponent<ModelSwapRecord>();
                record.sourceModelPrefab = model;
                record.swappedRendererNames = swapped.ToArray();
            }

            // A new rig comes without the goblin base's ragdoll (bone bodies, colliders, joints, blood).
            if (animator.GetComponentsInChildren<Rigidbody>(true).Length == 0)
            {
                CopyRagdoll(animator.transform);
            }

            // A staff in the right hand.
            Transform hand = FindDeep(animator.transform, "hand_r");
            if (hand != null && hand.Find("Mogra Staff") == null)
            {
                GameObject staff = NestPrefab(StaffPath, hand, "Mogra Staff", Vector3.zero, 1f);
                staff.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                foreach (Collider c in staff.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            }
            else if (hand == null)
            {
                Debug.LogWarning("[CaptainMograBuilder] No hand_r bone found; Mogra has no staff.");
            }

            CaptainMograController controller = root.GetComponent<CaptainMograController>();
            if (controller == null) throw new InvalidOperationException("Mogra variant has no CaptainMograController (did the generator run?).");

            Transform feedbacks = root.transform.Find("Mogra Feedbacks");
            if (feedbacks == null)
            {
                feedbacks = new GameObject("Mogra Feedbacks").transform;
                feedbacks.SetParent(root.transform, false);
            }

            MMF_Player roar = NewPlayer(feedbacks, "PhaseRoarMMF");
            AddSound(roar, SfxLaugh, 1f);
            AddImpulse(roar);
            MMF_Player boltCast = NewPlayer(feedbacks, "BoltCastMMF");
            AddSound(boltCast, SfxBoltWindup, 0.8f);
            AddSound(boltCast, SfxChant, 0.6f);
            MMF_Player boltRelease = NewPlayer(feedbacks, "BoltReleaseMMF");
            AddSound(boltRelease, SfxBoltRelease, 0.8f);
            AddSound(boltRelease, SfxFizzle, 0.5f);
            MMF_Player runeCast = NewPlayer(feedbacks, "RuneCastMMF");
            AddSound(runeCast, SfxRuneWindup, 0.9f);
            AddSound(runeCast, SfxLaugh, 0.5f);
            MMF_Player runeRelease = NewPlayer(feedbacks, "RuneReleaseMMF");
            AddSound(runeRelease, SfxRuneRelease, 0.8f);
            AddSound(runeRelease, SfxPoof, 0.6f);
            MMF_Player blinkFx = NewPlayer(feedbacks, "BlinkMMF");
            AddParticles(blinkFx, HexBurstLargePath);
            AddSound(blinkFx, SfxWhooshBig, 0.8f);
            AddSound(blinkFx, SfxBlinkCast, 0.7f);
            MMF_Player summonFx = NewPlayer(feedbacks, "SummonMMF");
            AddSound(summonFx, SfxChant, 1f);
            AddSound(summonFx, SfxSummonCast, 0.8f);
            MMF_Player channel = NewPlayer(feedbacks, "RitualChannelMMF");
            AddSound(channel, SfxSizzleLoop, 0.8f, loop: true);
            AddSound(channel, SfxRitualStart, 0.9f);
            MMF_Player broken = NewPlayer(feedbacks, "RitualBrokenMMF");
            AddParticles(broken, HexBurstLargePath);
            AddSound(broken, SfxWhimper, 1f);
            AddImpulse(broken);
            MMF_Player detonate = NewPlayer(feedbacks, "RitualDetonateMMF");
            AddParticles(detonate, HexBurstLargePath);
            AddSound(detonate, SfxBlastLarge, 1f);
            AddSound(detonate, SfxRitualBlast, 0.9f);
            AddImpulse(detonate);
            MMF_Player stagger = NewPlayer(feedbacks, "StaggerMMF");
            AddParticles(stagger, HexDazePath);
            AddSound(stagger, SfxUgh, 0.9f);

            SetRef(controller, "phaseRoarFeedback", roar);
            SetRef(controller, "boltCastFeedback", boltCast);
            SetRef(controller, "runeCastFeedback", runeCast);
            SetRef(controller, "blinkFeedback", blinkFx);
            SetRef(controller, "summonFeedback", summonFx);
            SetRef(controller, "ritualChannelFeedback", channel);
            SetRef(controller, "ritualBrokenFeedback", broken);
            SetRef(controller, "ritualDetonateFeedback", detonate);
            SetRef(controller, "staggerFeedback", stagger);
            SetRef(controller, "boltReleaseFeedback", boltRelease);
            SetRef(controller, "runeReleaseFeedback", runeRelease);

            // Ritual cast bar over his head (world-space; the controller billboards it).
            Transform bar = root.transform.Find("Ritual Cast Bar");
            if (bar != null) Object.DestroyImmediate(bar.gameObject);
            GameObject barGo = NestPrefab(CastBarPrefabPath, root.transform, "Ritual Cast Bar", new Vector3(0f, 2.2f, 0f), 1f);
            barGo.transform.localScale = Vector3.one * (0.01f / Mathf.Max(0.01f, root.transform.localScale.x));
            Transform fillT = FindDeep(barGo.transform, "CastBar_Fill");
            Transform textT = FindDeep(barGo.transform, "CastBar_Text");
            Image fillImage = fillT != null ? fillT.GetComponent<Image>() : null;
            if (fillImage != null) fillImage.color = HexGreen;
            SetRef(controller, "castBarRoot", barGo);
            SetRef(controller, "castBarFill", fillImage);
            SetRef(controller, "castBarText", textT != null ? textT.GetComponent<TMPro.TMP_Text>() : null);
            barGo.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, VariantPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    ///     Copies the goblin base prefab's bone ragdoll onto a replacement rig with the same bone names (the Sidekick
    ///     skeleton): Rigidbody, the non-trigger collider, CharacterJoint (reconnected to the new rig) and
    ///     RagdollBloodImpact, on the Ragdoll layer. Trigger colliders and VulnerableSpots are left to the rig.
    /// </summary>
    private static void CopyRagdoll(Transform targetRig)
    {
        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GoblinBasePrefabPath);
        if (basePrefab == null) throw new InvalidOperationException($"Goblin base prefab missing: {GoblinBasePrefabPath}");
        Animator sourceAnimator = basePrefab.GetComponentInChildren<Animator>(true);

        Dictionary<string, Transform> targetBones = new Dictionary<string, Transform>();
        foreach (Transform t in targetRig.GetComponentsInChildren<Transform>(true))
        {
            if (!targetBones.ContainsKey(t.name)) targetBones[t.name] = t;
        }

        List<KeyValuePair<CharacterJoint, CharacterJoint>> joints = new List<KeyValuePair<CharacterJoint, CharacterJoint>>();
        int copied = 0;
        foreach (Rigidbody sourceBody in sourceAnimator.GetComponentsInChildren<Rigidbody>(true))
        {
            if (!targetBones.TryGetValue(sourceBody.name, out Transform bone))
            {
                Debug.LogWarning($"[CaptainMograBuilder] Rig has no '{sourceBody.name}' bone; its ragdoll body is skipped.");
                continue;
            }
            bone.gameObject.layer = sourceBody.gameObject.layer;
            EditorUtility.CopySerialized(sourceBody, bone.gameObject.AddComponent<Rigidbody>());
            foreach (Collider sourceCollider in sourceBody.GetComponents<Collider>())
            {
                if (sourceCollider.isTrigger) continue;
                EditorUtility.CopySerialized(sourceCollider, bone.gameObject.AddComponent(sourceCollider.GetType()));
            }
            RagdollBloodImpact blood = sourceBody.GetComponent<RagdollBloodImpact>();
            if (blood != null) EditorUtility.CopySerialized(blood, bone.gameObject.AddComponent<RagdollBloodImpact>());
            CharacterJoint joint = sourceBody.GetComponent<CharacterJoint>();
            if (joint != null)
            {
                CharacterJoint copy = bone.gameObject.AddComponent<CharacterJoint>();
                EditorUtility.CopySerialized(joint, copy);
                joints.Add(new KeyValuePair<CharacterJoint, CharacterJoint>(joint, copy));
            }
            copied++;
        }

        foreach (KeyValuePair<CharacterJoint, CharacterJoint> pair in joints)
        {
            Rigidbody connected = pair.Key.connectedBody;
            pair.Value.connectedBody = connected != null && targetBones.TryGetValue(connected.name, out Transform bone) ? bone.GetComponent<Rigidbody>() : null;
        }
        Debug.Log($"[CaptainMograBuilder] Copied {copied} ragdoll bodies onto '{targetRig.name}'.");
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return t;
        }
        return null;
    }

    // ------------------------------------------------------------------ player dash animation

    /// <summary>
    ///     Adds a full-body "Dodge" override layer to the Player AC, above Melee and below Death: an empty
    ///     default state (the house layer pattern) and a forward dodge on the "Dodge" trigger, sped up to
    ///     roughly match the 0.2 s dash, then back to empty.
    /// </summary>
    private static void AddPlayerDodgeLayer()
    {
        AnimatorController ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
        if (ac == null) throw new InvalidOperationException($"Player AC missing: {PlayerControllerPath}");

        bool hasParam = false;
        foreach (AnimatorControllerParameter p in ac.parameters) if (p.name == "Dodge") hasParam = true;
        if (!hasParam) ac.AddParameter("Dodge", AnimatorControllerParameterType.Trigger);

        AnimatorControllerLayer[] layers = ac.layers;
        foreach (AnimatorControllerLayer l in layers)
        {
            if (l.name == "Dodge") return;
        }

        AnimatorStateMachine sm = new AnimatorStateMachine { name = "Dodge", hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(sm, ac);
        AnimatorState empty = sm.AddState("New State");
        empty.writeDefaultValues = true;
        sm.defaultState = empty;
        AnimatorState dodge = sm.AddState("Dodge");
        dodge.motion = LoadClip("A_MOD_SWD_Dodge_F_Neut");
        dodge.speed = 1.6f;
        dodge.writeDefaultValues = true;
        AnimatorStateTransition enter = sm.AddAnyStateTransition(dodge);
        enter.AddCondition(AnimatorConditionMode.If, 0f, "Dodge");
        enter.hasExitTime = false;
        enter.hasFixedDuration = true;
        enter.duration = 0.05f;
        enter.canTransitionToSelf = true;
        AnimatorStateTransition exit = dodge.AddTransition(empty);
        exit.hasExitTime = true;
        exit.exitTime = 0.8f;
        exit.hasFixedDuration = true;
        exit.duration = 0.15f;

        AnimatorControllerLayer layer = new AnimatorControllerLayer
        {
            name = "Dodge",
            defaultWeight = 1f,
            blendingMode = AnimatorLayerBlendingMode.Override,
            stateMachine = sm,
        };

        List<AnimatorControllerLayer> list = new List<AnimatorControllerLayer>(layers);
        int deathIndex = list.FindIndex(l => l.name == "Death");
        list.Insert(deathIndex >= 0 ? deathIndex : list.Count, layer);
        ac.layers = list.ToArray();
        EditorUtility.SetDirty(ac);
    }
}
