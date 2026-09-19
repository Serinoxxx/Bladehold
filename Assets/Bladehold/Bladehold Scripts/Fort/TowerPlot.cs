using System;
using UnityEngine;

/// <summary>
///     Statically placed foundation plot on the battlefield where defenses can be constructed.
///     In the preparation phase, the player can interact with the plot to bring up the Build Wheel.
///     When occupied, player interactions route directly to the defense structure for resupply/upgrading.
/// </summary>
public class TowerPlot : MonoBehaviour, IInteractable
{
    [Header("Plot Settings")]
    [SerializeField] private int plotIndex = 0;
    [SerializeField] private Transform buildAnchor;
    [SerializeField] private GameObject holyLightVfxPrefab;
    [SerializeField] private AudioClip woodImpactSfx;

    [Header("Prefabs for Defenses")]
    [SerializeField] private GameObject arrowTowerPrefab;
    [SerializeField] private GameObject catapultPrefab;
    [SerializeField] private GameObject ballistaPrefab;
    [SerializeField] private GameObject netThrowerPrefab;
    [SerializeField] private GameObject spikeTrapPrefab;
    [SerializeField] private GameObject oilVatPrefab;

    public int PlotIndex
    {
        get => plotIndex;
        set => plotIndex = value;
    }

    public DefenseStructure CurrentDefense { get; private set; }
    public bool IsOccupied => CurrentDefense != null;
    public bool IsBuilding { get; private set; } = false;

    public Vector3 BuildPosition => buildAnchor != null ? buildAnchor.position : transform.position;
    public Vector3 InteractionPosition => BuildPosition;

    public string PromptText
    {
        get
        {
            if (IsOccupied || IsBuilding) return "";
            if (GameLoopManager.Instance != null && !GameLoopManager.Instance.IsPrepPhase)
            {
                return "Locked (Wave Active)";
            }
            return "[E] Build Defence";
        }
    }

    public bool CanInteract
    {
        get
        {
            if (IsOccupied || IsBuilding) return false;
            if (GameLoopManager.Instance != null && !GameLoopManager.Instance.IsPrepPhase)
            {
                return false;
            }
            return true;
        }
    }

    private void Awake()
    {
        if (buildAnchor == null) buildAnchor = transform;
    }

    private void Start()
    {
        if (TowerPlotManager.Instance != null)
        {
            TowerPlotManager.Instance.RegisterPlot(this);
        }
    }

    private void OnDestroy()
    {
        if (TowerPlotManager.Instance != null)
        {
            TowerPlotManager.Instance.UnregisterPlot(this);
        }
    }

    public void Interact(Player player)
    {
        if (!CanInteract) return;

        if (BuildWheelUI.Instance != null)
        {
            BuildWheelUI.Instance.Open(this);
        }
        else
        {
            Debug.LogWarning("[TowerPlot] BuildWheelUI.Instance is not found in the scene.");
        }
    }

    public void BuildDefense(FortDefenseType type, int level = 1, int supply = -1, bool instant = false)
    {
        if (IsOccupied || IsBuilding) return;

        GameObject prefab = GetPrefabForType(type);
        if (prefab == null)
        {
            Debug.LogWarning($"[TowerPlot] No prefab assigned for defense type: {type}");
            return;
        }

        if (instant)
        {
            GameObject defObj = Instantiate(prefab, BuildPosition, transform.rotation);
            DefenseStructure structure = defObj.GetComponent<DefenseStructure>();
            if (structure != null)
            {
                CurrentDefense = structure;
                structure.OwnerPlot = this;
                structure.PlotIndex = plotIndex;
                structure.InitState(level, supply, -1);
            }
            return;
        }

        IsBuilding = true;
        GameObject animObj = new GameObject($"AssemblyAnim_Plot_{plotIndex}");
        DefenseAssemblyAnimation anim = animObj.AddComponent<DefenseAssemblyAnimation>();

        // Pass VFX/SFX references to anim if assigned
        // Then launch assembly
        anim.PlayAssembly(BuildPosition, transform.rotation, prefab, (structure) =>
        {
            IsBuilding = false;
            CurrentDefense = structure;
            if (structure != null)
            {
                structure.OwnerPlot = this;
                structure.PlotIndex = plotIndex;
                structure.InitState(level, supply, -1);
            }
        });
    }

    public void OnDefenseDestroyed(DefenseStructure structure)
    {
        if (CurrentDefense == structure)
        {
            CurrentDefense = null;
            IsBuilding = false;
        }
    }

    public void ClearDefense()
    {
        if (CurrentDefense != null)
        {
            Destroy(CurrentDefense.gameObject);
            CurrentDefense = null;
        }
        IsBuilding = false;
    }

    public GameObject GetPrefabForType(FortDefenseType type)
    {
        return type switch
        {
            FortDefenseType.ArrowSlits => arrowTowerPrefab,
            FortDefenseType.Catapult => catapultPrefab,
            FortDefenseType.Ballista => ballistaPrefab,
            FortDefenseType.NetThrower => netThrowerPrefab,
            FortDefenseType.Spikes => spikeTrapPrefab,
            FortDefenseType.BurningOil => oilVatPrefab,
            _ => null
        };
    }

    public void SetPrefabs(GameObject arrow, GameObject cat, GameObject bal, GameObject net, GameObject spikes, GameObject oil)
    {
        arrowTowerPrefab = arrow;
        catapultPrefab = cat;
        ballistaPrefab = bal;
        netThrowerPrefab = net;
        spikeTrapPrefab = spikes;
        oilVatPrefab = oil;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = IsOccupied ? Color.green : Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(2f, 0.2f, 2f));
    }
#endif
}
