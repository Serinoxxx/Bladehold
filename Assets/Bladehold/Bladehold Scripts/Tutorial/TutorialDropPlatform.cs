using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     The chasm platform held up by ropes: once every rope <see cref="Health" /> has died,
///     <see cref="dropFeedback" /> plays (its Position/Rotation feedbacks swing the platform down into the
///     gap, plus creak, thud, dust and impulse). After <see cref="landDelay" /> the walkable collider turns
///     on and the invisible gap barrier turns off.
/// </summary>
public class TutorialDropPlatform : MonoBehaviour
{
    [SerializeField] private List<Health> ropes = new List<Health>();

    [Tooltip("Swings/drops the platform and plays the landing. Its duration should match landDelay.")]
    [SerializeField] private MMF_Player dropFeedback;

    [Tooltip("Seconds from the last rope snapping to the platform being walkable.")]
    [Min(0f)] [SerializeField] private float landDelay = 1.2f;

    [Tooltip("The platform's walkable collider, off until it lands.")]
    [SerializeField] private Collider walkCollider;

    [Tooltip("Invisible wall across the gap, removed once the platform lands.")]
    [SerializeField] private GameObject gapBarrier;

    private bool dropped;
    private bool anyError;

    private void Start()
    {
        if (ropes.Count == 0) { Debug.LogError($"[TutorialDropPlatform] {name}: no ropes assigned.", this); anyError = true; }
        if (dropFeedback == null) Debug.LogError($"[TutorialDropPlatform] {name}: dropFeedback is not assigned.", this);
        if (walkCollider == null) { Debug.LogError($"[TutorialDropPlatform] {name}: walkCollider is not assigned.", this); anyError = true; }
        if (anyError) return;

        walkCollider.enabled = false;
        foreach (Health rope in ropes)
        {
            if (rope != null) rope.OnDied += HandleRopeDied;
        }
    }

    private void OnDestroy()
    {
        foreach (Health rope in ropes)
        {
            if (rope != null) rope.OnDied -= HandleRopeDied;
        }
    }

    private void HandleRopeDied()
    {
        if (dropped) return;
        foreach (Health rope in ropes)
        {
            if (rope != null && !rope.IsDead) return;
        }
        dropped = true;
        StartCoroutine(DropRoutine());
    }

    private IEnumerator DropRoutine()
    {
        if (dropFeedback != null) dropFeedback.PlayFeedbacks(transform.position);
        yield return new WaitForSeconds(landDelay);
        walkCollider.enabled = true;
        if (gapBarrier != null) gapBarrier.SetActive(false);
    }
}
