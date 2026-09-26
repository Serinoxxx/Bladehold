using System;
using System.Collections;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEngine;

/// <summary>
///     Prisoner NPC inside a cage. When released, plays a cheer animation and sound,
///     triggers spring feedback, and vanishes in a puff of smoke after a short delay.
/// </summary>
public class RescuedPrisoner : MonoBehaviour
{
    [Header("Animation & Juiciness")]
    [Tooltip("Animator driving the prisoner model.")]
    [SerializeField] private Animator animator;

    [Tooltip("Animator trigger parameter for cheering (e.g. 'Cheer').")]
    [SerializeField] private string cheerTriggerName = "Cheer";

    [Tooltip("MMF_Player played when released (cheering voice).")]
    [SerializeField] private MMF_Player cheerFeedback;

    [Tooltip("MMF_Player played when the prisoner vanishes (poof sound + smoke puff).")]
    [SerializeField] private MMF_Player poofFeedback;

    [Tooltip("Seconds the prisoner cheers before vanishing in smoke.")]
    [SerializeField] private float cheerDuration = 2.5f;

    private bool isReleased;

    public bool IsReleased => isReleased;
    public event Action<RescuedPrisoner> OnReleased;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    private void Start()
    {
        if (cheerFeedback == null) Debug.LogError("[RescuedPrisoner] cheerFeedback is not assigned.", this);
        if (poofFeedback == null) Debug.LogError("[RescuedPrisoner] poofFeedback is not assigned.", this);
    }

    /// <summary>Called when the surrounding cage is broken.</summary>
    public void Release()
    {
        if (isReleased) return;
        isReleased = true;

        // Trigger cheer animation
        if (animator != null)
        {
            animator.SetTrigger(cheerTriggerName);
        }

        // Trigger MMF feedback
        if (cheerFeedback != null)
        {
            cheerFeedback.PlayFeedbacks(transform.position);
        }

        OnReleased?.Invoke(this);
        StartCoroutine(CheerAndDespawnRoutine());
    }

    private IEnumerator CheerAndDespawnRoutine()
    {
        yield return new WaitForSeconds(cheerDuration);

        if (poofFeedback != null)
        {
            poofFeedback.PlayFeedbacks(transform.position);
        }

        Destroy(gameObject);
    }
}
