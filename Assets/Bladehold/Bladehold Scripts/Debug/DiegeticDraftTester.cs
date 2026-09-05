
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
///     Spawns a diegetic (in-world) row of interactable pedestals for every draft skill in the game.
///     Enforces draft rules (elemental combos, ultimate exclusivity, max level) dynamically.
/// </summary>
public class DiegeticDraftTester : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private Vector3 startPosition = new Vector3(-20f, 0f, -10f);
    [SerializeField] private float spacingX = 3f;
    [SerializeField] private float spacingZ = 4f;
    
    [Header("Pedestal Settings")]
    [SerializeField] private float pedestalScale = 0.5f;
    [SerializeField] private float textHeight = 1.5f;

    private class Pedestal
    {
        public DraftUpgradeDefinition def;
        public GameObject root;
        public Interactable interactable;
        public TextMeshPro text;
        public Renderer renderer;
    }

    private readonly List<Pedestal> pedestals = new List<Pedestal>();

    private void Start()
    {
        DraftUpgradeService service = DraftUpgradeService.GetOrCreateInstance();
        if (service == null || service.AllDefinitions == null) return;

        int row = 0;
        int col = 0;
        int maxCols = 10;

        foreach (var def in service.AllDefinitions)
        {
            Vector3 pos = startPosition + new Vector3(col * spacingX, 0f, row * spacingZ);
            CreatePedestal(def, pos);

            col++;
            if (col >= maxCols)
            {
                col = 0;
                row++;
            }
        }
    }

    private void CreatePedestal(DraftUpgradeDefinition def, Vector3 pos)
    {
        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
        root.name = $"DraftPedestal_{def.id}";
        root.transform.position = pos;
        root.transform.localScale = new Vector3(pedestalScale, pedestalScale, pedestalScale);
        
        // Remove standard collider to replace with trigger for Interactable, or just leave it.
        // Interactable in PlayerInteraction uses Physics.OverlapSphere which requires a collider.
        Collider col = root.GetComponent<Collider>();
        col.isTrigger = false; 

        Renderer r = root.GetComponent<Renderer>();

        Interactable interactable = root.AddComponent<Interactable>();
        interactable.PromptText = $"Take {def.displayName}";
        interactable.OnInteractedEvent += (p) => OnPedestalInteracted(def);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(root.transform);
        textObj.transform.localPosition = new Vector3(0f, textHeight / pedestalScale, 0f);
        
        // Angle text slightly up
        textObj.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);

        TextMeshPro text = textObj.AddComponent<TextMeshPro>();
        text.text = def.displayName;
        text.fontSize = 4f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.rectTransform.sizeDelta = new Vector2(10f, 2f);

        pedestals.Add(new Pedestal
        {
            def = def,
            root = root,
            interactable = interactable,
            text = text,
            renderer = r
        });
    }

    private void OnPedestalInteracted(DraftUpgradeDefinition def)
    {
        if (CanDraft(def, out string _))
        {
            DraftUpgradeService.Instance.ApplyUpgrade(def);
        }
    }

    private void Update()
    {
        UpdatePedestalsState();
    }

    private void UpdatePedestalsState()
    {
        foreach (var p in pedestals)
        {
            bool canDraft = CanDraft(p.def, out string reason);
            int currentLevel = RunSession.GetUpgradeLevel(p.def.id);
            
            p.interactable.CanInteract = canDraft;

            string title = string.IsNullOrEmpty(p.def.displayName) ? p.def.id : p.def.displayName;
            string levelText = $"(Lv {currentLevel}/{p.def.maxLevel})";
            
            if (canDraft)
            {
                p.text.text = $"{title}\n<size=60%>{levelText}</size>";
                p.text.color = Color.white;
                if (p.renderer != null) p.renderer.material.color = Color.gray;
                p.interactable.PromptText = $"Take {title}";
            }
            else
            {
                p.text.text = $"{title}\n<size=50%><color=red>{reason}</color></size>";
                p.text.color = Color.red;
                if (p.renderer != null) p.renderer.material.color = Color.black;
                p.interactable.PromptText = $"Locked: {reason}";
            }
        }
    }

    private bool CanDraft(DraftUpgradeDefinition def, out string reason)
    {
        reason = "";

        if (def == null) return false;

        // 1. Max Level
        if (RunSession.GetUpgradeLevel(def.id) >= def.maxLevel)
        {
            reason = "Max Level";
            return false;
        }

        // 2. Ultimate exclusivity
        if (def.isUltimate && !string.IsNullOrEmpty(RunSession.ActiveUltimateId))
        {
            if (RunSession.ActiveUltimateId != def.id)
            {
                reason = "Already have Ultimate";
                return false;
            }
        }

        // 3. Elemental Duo Prereq
        if (def.category == DraftCategory.Elemental && def.isDuo)
        {
            HashSet<string> activeElements = RunSession.GetActiveElements();
            foreach (var prereq in def.prerequisiteElements)
            {
                if (!activeElements.Contains(prereq))
                {
                    reason = $"Needs {prereq}";
                    return false;
                }
            }
        }

        // 4. Weapon match
        if (def.category == DraftCategory.Weapon && !string.IsNullOrEmpty(def.weapon))
        {
            string equippedMelee = "sword";
            string equippedRanged = "bow";
            if (PlayerWeaponManager.Instance != null)
            {
                equippedMelee = PlayerWeaponManager.Instance.CurrentMeleeId.ToLowerInvariant();
                equippedRanged = PlayerWeaponManager.Instance.CurrentRangedId.ToLowerInvariant();
            }

            bool matchesEquipped = def.weapon.Equals(equippedMelee, System.StringComparison.OrdinalIgnoreCase) ||
                                   def.weapon.Equals(equippedRanged, System.StringComparison.OrdinalIgnoreCase);
            
            if (!matchesEquipped)
            {
                reason = $"Needs {def.weapon}";
                return false;
            }
        }

        return true;
    }
}

