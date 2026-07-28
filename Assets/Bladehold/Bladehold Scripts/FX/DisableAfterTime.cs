using UnityEngine;

/// <summary>
/// Disables the GameObject after a given time. Useful for returning pooled prefabs 
/// (like MMF_Player particle impacts) back to the pool automatically.
/// </summary>
public class DisableAfterTime : MonoBehaviour
{
    [Tooltip("Seconds before disabling the GameObject.")]
    [SerializeField] private float lifetimeSeconds = 1f;
    private float timer;

    private void OnEnable()
    {
        timer = lifetimeSeconds;
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            gameObject.SetActive(false);
        }
    }
}
