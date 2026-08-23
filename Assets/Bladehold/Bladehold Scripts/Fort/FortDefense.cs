using UnityEngine;

/// <summary>
///     Base class for in-run fort defenses (Arrow Slits, Boiling Oil, Spikes).
///     Tracks upgrade level, deployment state, and provides common targeting / feedback helpers.
/// </summary>
public abstract class FortDefense : MonoBehaviour
{
    [Header("Defense Config")]
    [SerializeField] protected FortDefenseType defenseType;
    [SerializeField] protected int currentLevel = 1;

    public FortDefenseType DefenseType => defenseType;
    public int Level => currentLevel;

    public virtual void SetLevel(int level)
    {
        currentLevel = Mathf.Max(1, level);
        OnUpgraded(currentLevel);
    }

    public virtual void Upgrade()
    {
        currentLevel++;
        OnUpgraded(currentLevel);
    }

    protected virtual void Start()
    {
        OnDeployed();
    }

    protected virtual void OnDeployed()
    {
    }

    protected virtual void OnUpgraded(int newLevel)
    {
    }
}
