using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Trigger volume in the chasm: a player who falls in is put back at <see cref="respawnPoint" /> (the
///     chasm's near edge), or the current step's waypoint when that's unset
///     (<see cref="TutorialDirector.RespawnPoint" />). No damage, so the dungeon can't be failed.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TutorialFallRespawn : MonoBehaviour
{
    [Tooltip("Where a fall puts the player back. Empty = the current step's waypoint.")]
    [SerializeField] private Transform respawnPoint;

    [Tooltip("Optional: played at the respawn point (whoosh, dust). Leave empty for none.")]
    [SerializeField] private MMF_Player respawnFeedback;

    private void OnValidate()
    {
        Collider c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        Player player = other.GetComponentInParent<Player>();
        if (player == null || player != Player.Instance) return;

        Transform point = respawnPoint != null
            ? respawnPoint
            : (TutorialDirector.Instance != null ? TutorialDirector.Instance.RespawnPoint : null);
        if (point == null)
        {
            Debug.LogError($"[TutorialFallRespawn] {name}: no respawn point (respawnPoint unset, no director step waypoint).", this);
            return;
        }

        // A CharacterController overwrites a direct position change unless it's disabled for the move.
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.transform.SetPositionAndRotation(point.position, point.rotation);
        if (cc != null) cc.enabled = true;

        if (respawnFeedback != null) respawnFeedback.PlayFeedbacks(point.position);
    }
}
