using UnityEngine;

/// <summary>
///     T2's last beat (plan 21 phase 6): the Ultimate Trial. On begin it grants <see cref="trialCores" /> free
///     Arcane Cores (<see cref="TutorialRun.GrantTrialArcaneCores" />), lifts the scene's ultimate block for
///     itself (<see cref="SceneAbilityRules.AllowUltimateFor" />) and sends in a horde. The hint walks the
///     player through it: open the wheel, fire the sword ultimate, fire the bow ultimate, then the closing
///     line on where cores come from while they finish the horde off. Whenever the horde is wiped out while an
///     ultimate is still to be tried (one ultimate can clear it), a fresh horde comes in, so there's always
///     something to use the next one on. It completes once both ultimates have fired and the last horde is
///     dead, and takes back any trial cores left over.
///     If the player runs short of cores for the ultimates still to fire (e.g. fired the sword twice), it tops
///     them up with more trial cores so the step can always finish.
/// </summary>
public class UltimateTrialStep : TutorialStep
{
    [Header("Ultimate Trial")]
    [SerializeField] private TutorialEncounter horde;
    [Tooltip("Free Arcane Cores granted on begin. Stripped again when the step completes.")]
    [Min(1)] [SerializeField] private int trialCores = 2;

    [Tooltip("Once the wheel has been opened, until the melee ultimate fires.")]
    [SerializeField] private TutorialHint meleeHint = TutorialHint.Of("tutorial.ultimate_melee", "Unleash your sword ultimate", "Ultimate");
    [Tooltip("Once the melee ultimate has fired, until the ranged one does.")]
    [SerializeField] private TutorialHint rangedHint = TutorialHint.Of("tutorial.ultimate_ranged", "Now your bow ultimate", "Ultimate");
    [Tooltip("Both fired: shown while the player finishes off the horde.")]
    [SerializeField] private TutorialHint closingHint = TutorialHint.Of("tutorial.ultimate_cost",
        "Each ultimate costs an Arcane Core. Earn them from captains and Arcane Fish, or buy them in the shop.");

    private PlayerUltimateController ultimates;
    private TutorialHint shownHint;
    private string counter;
    private bool wheelOpened;
    private bool meleeUsed;
    private bool rangedUsed;
    private bool anyError;

    private void Start()
    {
        if (horde == null)
        {
            Debug.LogError($"[UltimateTrialStep] {name}: horde is not assigned.", this);
            anyError = true;
        }
    }

    protected override void OnBegin()
    {
        if (anyError) return;
        // PlayerUltimateController sits on the player root; Player.Instance is on the Synty character child.
        ultimates = Player.Instance != null ? Player.Instance.transform.root.GetComponentInChildren<PlayerUltimateController>(true) : null;
        if (ultimates == null)
        {
            Debug.LogError($"[UltimateTrialStep] {name}: no PlayerUltimateController on the player.", this);
            return;
        }

        // A retry after dying in the arena: take back the last attempt's leftovers before granting afresh.
        TutorialRun.StripTrialArcaneCores();
        TutorialRun.GrantTrialArcaneCores(trialCores);
        SceneAbilityRules.AllowUltimateFor(this);

        // A weapon with no ultimate can't be taught, so don't wait on it.
        meleeUsed = !ultimates.HasUltimate(UltimateSlot.Melee);
        rangedUsed = !ultimates.HasUltimate(UltimateSlot.Ranged);
        if (meleeUsed || rangedUsed) Debug.LogWarning($"[UltimateTrialStep] {name}: a held weapon has no ultimate (melee {!meleeUsed}, ranged {!rangedUsed}); skipping it.", this);

        ultimates.OnUltimateActivated += HandleUltimateActivated;
        horde.OnAliveCountChanged += HandleAliveChanged;
        horde.OnAllDead += HandleAllDead;
        horde.Spawn();
    }

    protected override void OnEnd()
    {
        SceneAbilityRules.ReleaseUltimateFor(this);
        if (ultimates != null) ultimates.OnUltimateActivated -= HandleUltimateActivated;
        if (horde == null) return;
        horde.OnAliveCountChanged -= HandleAliveChanged;
        horde.OnAllDead -= HandleAllDead;
    }

    private void Update()
    {
        if (!IsActive || ultimates == null) return;
        if (!wheelOpened && UltimateWheelUI.IsOpen)
        {
            wheelOpened = true;
            RefreshHint();
        }
        TopUpCores();
    }

    private void TopUpCores()
    {
        int needed = (meleeUsed ? 0 : 1) + (rangedUsed ? 0 : 1);
        if (RunSession.ArcaneCores < needed) TutorialRun.GrantTrialArcaneCores(needed - RunSession.ArcaneCores);
    }

    private void HandleUltimateActivated()
    {
        if (ultimates.ActiveSlot == UltimateSlot.Melee) meleeUsed = true;
        else rangedUsed = true;
        wheelOpened = true;
        RefreshHint();
    }

    private void RefreshHint()
    {
        TutorialHint next;
        if (meleeUsed && rangedUsed) next = closingHint;
        else if (!wheelOpened) next = Hint;
        else next = meleeUsed ? rangedHint : meleeHint;
        if (next == shownHint) return;
        shownHint = next;
        Director.SetHint(next);
        // Showing a hint clears the counter line, so put the horde count back.
        if (counter != null) Director.SetCounter(counter);
    }

    private void HandleAliveChanged(int alive, int total)
    {
        counter = $"{total - alive}/{total}";
        Director.SetCounter(counter);
    }

    private void HandleAllDead()
    {
        if (!IsActive) return;
        if (!meleeUsed || !rangedUsed)
        {
            // Wiped out with an ultimate still to try: send in another horde.
            horde.SpawnAgain();
            return;
        }
        TutorialRun.StripTrialArcaneCores();
        Complete();
    }
}
