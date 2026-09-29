using UnityEngine;

/// <summary>Tunables for <see cref="WardenEscort" />: how many goblins form up around the Dome Warden and when they break off.</summary>
[CreateAssetMenu(fileName = "WardenEscortSO", menuName = "Scriptable Objects/Enemies/Warden Escort")]
public class WardenEscortSO : ScriptableObject
{
    [Tooltip("Formation slots around the Warden.")]
    [Min(0)] public int maxEscorts = 12;
    [Tooltip("Enemies within this distance of the Warden can be recruited into a free slot.")]
    public float recruitRadius = 16f;
    [Tooltip("Roster ids that may join the formation. Specials keep their own jobs.")]
    public string[] recruitRosterIds = { "goblin" };
    [Tooltip("Seconds between refilling free slots.")]
    public float recruitInterval = 0.5f;

    [Header("Formation")]
    [Tooltip("Radius of the escort ring. Keep it inside the dome radius (5m) so escorts stay covered.")]
    public float ringRadius = 3.5f;
    [Tooltip("When the player comes this close to the Warden (flat distance), every escort breaks formation and attacks. Keep it under the Warden's 9m stand-off so they hold until the player closes in.")]
    public float engageRadius = 6f;
}
