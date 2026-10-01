using UnityEngine;

/// <summary>Completes when the player enters this trigger volume (needs a trigger Collider on this object).</summary>
[RequireComponent(typeof(Collider))]
public class ReachAreaStep : TutorialStep
{
    private bool playerInside;

    private void OnValidate()
    {
        Collider c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    protected override void OnBegin()
    {
        if (playerInside) Complete();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;
        playerInside = true;
        if (IsActive) Complete();
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other)) playerInside = false;
    }

    private static bool IsPlayer(Collider other)
    {
        return Player.Instance != null && other.GetComponentInParent<Player>() == Player.Instance;
    }
}
