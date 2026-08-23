using UnityEngine;

/// <summary>
///     Scene marker for fort defense placement. Defines socket type (WallSlit, GateOverhead, GroundBarricade)
///     and tracks the active defense occupant installed at this location.
/// </summary>
public class FortDefenseSocket : MonoBehaviour
{
    [Header("Socket Configuration")]
    [SerializeField] private FortSocketType socketType = FortSocketType.WallSlit;
    [SerializeField] private FortDefense defaultDefensePrefab;
    [SerializeField] private bool autoDeployOnStart = false;

    public FortSocketType SocketType => socketType;
    public FortDefense CurrentDefense { get; private set; }
    public bool IsOccupied => CurrentDefense != null;

    private void OnEnable()
    {
        if (FortDefenseManager.Instance != null)
        {
            FortDefenseManager.Instance.RegisterSocket(this);
        }
    }

    private void OnDisable()
    {
        if (FortDefenseManager.Instance != null)
        {
            FortDefenseManager.Instance.UnregisterSocket(this);
        }
    }

    private void Start()
    {
        if (autoDeployOnStart && defaultDefensePrefab != null && CurrentDefense == null)
        {
            InstallDefense(defaultDefensePrefab.gameObject, 1);
        }
    }

    /// <summary>
    ///     Instantiates and mounts a defense prefab at this socket location.
    /// </summary>
    public FortDefense InstallDefense(GameObject defensePrefab, int initialLevel = 1)
    {
        if (defensePrefab == null)
        {
            Debug.LogWarning($"[FortDefenseSocket] Cannot install null prefab on socket '{name}'.");
            return null;
        }

        if (CurrentDefense != null)
        {
            Destroy(CurrentDefense.gameObject);
            CurrentDefense = null;
        }

        GameObject instance = Instantiate(defensePrefab, transform.position, transform.rotation, transform);
        CurrentDefense = instance.GetComponent<FortDefense>();
        if (CurrentDefense != null)
        {
            CurrentDefense.SetLevel(initialLevel);
        }

        return CurrentDefense;
    }

    /// <summary>
    ///     Clears and destroys the active occupant on this socket.
    /// </summary>
    public void ClearDefense()
    {
        if (CurrentDefense != null)
        {
            Destroy(CurrentDefense.gameObject);
            CurrentDefense = null;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Color color = socketType switch
        {
            FortSocketType.WallSlit => Color.cyan,
            FortSocketType.GateOverhead => Color.red,
            FortSocketType.GroundBarricade => new Color(1f, 0.5f, 0f), // Orange
            FortSocketType.Courtyard => Color.yellow,
            _ => Color.white
        };

        Gizmos.color = color;
        Gizmos.DrawWireCube(transform.position, new Vector3(1.2f, 0.5f, 1.2f));

        // Draw facing vector
        Gizmos.color = Color.white;
        Gizmos.DrawRay(transform.position, transform.forward * 2f);
    }
#endif
}
