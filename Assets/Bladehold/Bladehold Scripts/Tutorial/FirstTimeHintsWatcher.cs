using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Watches for the moments <see cref="FirstTimeHints" /> covers and fires each once. Sits next to
///     <see cref="TutorialHintUI" /> in the HUD (and the Meta Area canvas); every trigger is a cheap check
///     that does nothing in scenes where its subject doesn't exist.
///     - Gate damaged: any <see cref="Gate" />'s health drops below max (waypoint on the gate).
///     - Gate repair: prep phase with the gate below max and a <see cref="GateRepairStation" /> (waypoint on it).
///     - Tower restock: any <see cref="DefenseStructure" /> at or under half supply (waypoint on it).
///     - Wave cards: the wave choice opens (<see cref="GameLoopManager.IsChoosingCard" />).
///     - Meta gift: the first-run gift has been paid and a <see cref="WeaponPedestal" /> has a weapon the
///       player can now afford (waypoint on it). Shown before the Meta Spirit hint, which waits for it.
///     - Meta Spirit: a <see cref="SpiritNPC" /> is in the scene and the player has Goblin Blood.
///     - Fishing Pond: a <see cref="FishingManager" /> is in the scene (free arrows, bow only).
/// </summary>
public class FirstTimeHintsWatcher : MonoBehaviour, IWaypointSource
{
    [SerializeField] private float hintSeconds = 8f;
    [SerializeField] private float checkInterval = 0.5f;

    [SerializeField] private TutorialHint gateDamagedHint = TutorialHint.Of("hint.gate_damaged",
        "The gate is under attack! Gate damage lasts the whole run. If it falls, the run is over.");
    [SerializeField] private TutorialHint gateRepairHint = TutorialHint.Of("hint.gate_repair",
        "Repair the gate with supply during prep. Supply spent here can't build towers.", "Interact");
    [SerializeField] private TutorialHint towerRestockHint = TutorialHint.Of("hint.tower_restock",
        "Towers run out of ammo. Use a tower to restock it with supply.", "Interact");
    [SerializeField] private TutorialHint waveCardsHint = TutorialHint.Of("hint.wave_cards",
        "Defence keeps you at the gate. Offence pays more, but leaves your towers to hold alone.");
    [SerializeField] private TutorialHint metaSpiritHint = TutorialHint.Of("hint.meta_spirit",
        "Spend Goblin Blood with the Spirit for permanent upgrades.");
    [SerializeField] private TutorialHint metaGiftHint = TutorialHint.Of("hint.meta_gift",
        "The Spirit has gifted you Goblin Blood and Orcish Metal. Forge a new weapon here, then see the Spirit for permanent upgrades.", "Interact");
    [Tooltip("The gift hint is longer to read, so it stays up longer than the others.")]
    [SerializeField] private float giftHintSeconds = 10f;
    [SerializeField] private TutorialHint fishingPondHint = TutorialHint.Of("hint.fishing_pond",
        "Arrows are unlimited here, so shoot freely! It's just you and your bow: no sword, mount or ultimate.", "Aim");
    [Tooltip("The Fishing Pond hint is longer to read, so it stays up longer than the others.")]
    [SerializeField] private float fishingHintSeconds = 10f;

    [SerializeField] private Color waypointTint = new Color(0.55f, 0.9f, 1f, 1f);

    private static FirstTimeHintsWatcher instance;
    private Transform pointTarget;
    private float pointUntil;
    private float nextCheck;

    private void OnEnable()
    {
        instance = this;
        ObjectiveWaypointTrackerUI.RegisterSource(this);
    }

    private void OnDisable()
    {
        ObjectiveWaypointTrackerUI.UnregisterSource(this);
        if (instance == this) instance = null;
    }

    /// <summary>Shows a HUD waypoint on <paramref name="target" /> for <paramref name="seconds" />.</summary>
    public static void PointAt(Transform target, float seconds)
    {
        if (instance == null) return;
        instance.pointTarget = target;
        instance.pointUntil = Time.unscaledTime + seconds;
    }

    public void GetWaypointTargets(List<ObjectiveWaypointTarget> results)
    {
        if (pointTarget == null || Time.unscaledTime > pointUntil) return;
        Sprite icon = ObjectiveWaypointTrackerUI.Instance != null ? ObjectiveWaypointTrackerUI.Instance.DefaultObjectiveIcon : null;
        results.Add(new ObjectiveWaypointTarget(pointTarget, new Vector3(0f, 2.5f, 0f), icon, waypointTint, null));
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + checkInterval;
        if (TutorialHintUI.Instance == null) return;

        CheckWaveCards();
        CheckGateDamaged();
        CheckGateRepair();
        CheckTowerRestock();
        CheckMetaGift();
        CheckMetaSpirit();
        CheckFishingPond();
    }

    private void CheckWaveCards()
    {
        GameLoopManager loop = GameLoopManager.Instance;
        if (loop != null && loop.IsChoosingCard)
        {
            FirstTimeHints.TryShow(FirstTimeHints.WaveCards, waveCardsHint, hintSeconds);
        }
    }

    private void CheckGateDamaged()
    {
        if (FirstTimeHints.HasSeen(FirstTimeHints.GateDamaged)) return;
        foreach (Gate gate in Gate.All)
        {
            if (gate == null || gate.IsDestroyed || gate.Health == null) continue;
            if (gate.Health.CurrentHealth < gate.Health.MaxHealth - 0.01f)
            {
                FirstTimeHints.TryShow(FirstTimeHints.GateDamaged, gateDamagedHint, hintSeconds, gate.transform);
                return;
            }
        }
    }

    private void CheckGateRepair()
    {
        if (FirstTimeHints.HasSeen(FirstTimeHints.GateRepair)) return;
        GameLoopManager loop = GameLoopManager.Instance;
        if (loop == null || !loop.IsPrepPhase || loop.IsChoosingCard) return;

        GateRepairStation station = GateRepairStation.Instance;
        if (station != null && station.CanInteract)
        {
            FirstTimeHints.TryShow(FirstTimeHints.GateRepair, gateRepairHint, hintSeconds, station.transform);
        }
    }

    private void CheckTowerRestock()
    {
        if (FirstTimeHints.HasSeen(FirstTimeHints.TowerRestock)) return;
        foreach (DefenseStructure def in DefenseStructure.AllActive)
        {
            if (def == null || def.MaxSupply <= 0) continue;
            if (def.CurrentSupply * 2 <= def.MaxSupply)
            {
                FirstTimeHints.TryShow(FirstTimeHints.TowerRestock, towerRestockHint, hintSeconds, def.transform);
                return;
            }
        }
    }

    private void CheckFishingPond()
    {
        if (FishingManager.Instance == null || FirstTimeHints.HasSeen(FirstTimeHints.FishingPond)) return;
        FirstTimeHints.TryShow(FirstTimeHints.FishingPond, fishingPondHint, fishingHintSeconds);
    }

    private void CheckMetaGift()
    {
        if (FirstTimeHints.HasSeen(FirstTimeHints.MetaGift) || TutorialHintUI.Instance.IsShowing) return;
        if (!FirstRunGift.IsGranted) return;
        foreach (WeaponPedestal pedestal in WeaponPedestal.All)
        {
            if (pedestal != null && pedestal.CanAffordUnlock)
            {
                FirstTimeHints.TryShow(FirstTimeHints.MetaGift, metaGiftHint, giftHintSeconds, pedestal.transform);
                return;
            }
        }
    }

    private void CheckMetaSpirit()
    {
        // Waits for the gift hint (or any other) to finish rather than replacing it.
        if (FirstTimeHints.HasSeen(FirstTimeHints.MetaSpirit) || TutorialHintUI.Instance.IsShowing) return;
        if (SaveSystem.Load().goblinBlood <= 0) return;
        SpiritNPC spirit = SpiritNPC.Instance;
        if (spirit != null)
        {
            FirstTimeHints.TryShow(FirstTimeHints.MetaSpirit, metaSpiritHint, hintSeconds, spirit.transform);
        }
    }
}
