using UnityEngine;

/// <summary>Tunables for <see cref="TowerHexer" />.</summary>
[CreateAssetMenu(fileName = "TowerHexerSO", menuName = "Scriptable Objects/Enemies/Tower Hexer")]
public class TowerHexerSO : ScriptableObject
{
    [Header("Finding a tower")]
    [Tooltip("Only towers within this distance are considered. Beyond it the Hexer fights like a normal goblin.")]
    public float searchRadius = 45f;
    [Tooltip("Seconds between looks for a new tower while it has none.")]
    public float rescanInterval = 0.5f;

    [Header("Channel")]
    [Tooltip("Flat distance from the tower at which the Hexer plants itself and channels, so the hero has to go to it.")]
    public float channelRange = 11f;
    [Tooltip("The channel drops if the tower gets further than channelRange + this (e.g. the Hexer got knocked away).")]
    public float breakRangeMargin = 2f;
    [Tooltip("Seconds the channel stays broken after the hero hits the Hexer. Tower hits don't interrupt it.")]
    public float interruptDuration = 1.5f;
    [Tooltip("Seconds between cast animation pulses while channelling.")]
    public float castPulseInterval = 1.2f;
    [Tooltip("Animator trigger fired on each cast pulse.")]
    public string castTrigger = "Attack";

    [Header("Beam")]
    [Tooltip("Authored beam prefab with a LightningSystemChain (a SineVFX LS_Chain). Its two chain points are set at runtime.")]
    public GameObject beamPrefab;
    [Tooltip("Beam start height above the Hexer's feet.")]
    public float beamOriginHeight = 1.6f;
    [Tooltip("Beam end height above the tower's base.")]
    public float beamTargetHeight = 2.5f;
}
