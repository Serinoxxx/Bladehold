using UnityEngine;

/// <summary>
///     The Dome Warden's escort: recruits up to <see cref="WardenEscortSO.maxEscorts" /> nearby goblins
///     into a ring inside its dome, so the dome reads as "that group is protected". Escorts walk with
///     the Warden and ignore the player until the player comes within
///     <see cref="WardenEscortSO.engageRadius" />; then they all break formation at once. Escorts that
///     die free their slot for the next recruit. The formation dissolves when the Warden dies.
///
///     Targeting goes through <see cref="AITargetSelector.SetEscortLeader" />, so movement and attacks
///     need no changes. Tower targets (the Sapper) still beat formation.
/// </summary>
public class WardenEscort : MonoBehaviour
{
    [SerializeField] private WardenEscortSO data;
    [SerializeField] private Health health;
    [SerializeField] private LayerMask enemyLayers = ~0;

    private AITargetSelector[] slots;
    private Health[] slotHealth;
    private float nextRecruitTime;
    private bool isDead;
    private bool anyError;

    public bool IsLeading => !isDead && !anyError;

    /// <summary>True while the player is close enough that escorts should fight instead of holding formation.</summary>
    public bool EscortsEngage
    {
        get
        {
            Player player = Player.Instance;
            if (player == null || player.Health == null || player.Health.IsDead) return false;
            Vector3 offset = player.transform.position - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= data.engageRadius * data.engageRadius;
        }
    }

    public int EscortCount
    {
        get
        {
            if (slots == null) return 0;
            int count = 0;
            for (int i = 0; i < slots.Length; i++) if (IsSlotHeld(i)) count++;
            return count;
        }
    }

    private void OnValidate()
    {
        if (health == null) health = GetComponent<Health>();
    }

    private void Start()
    {
        if (data == null)
        {
            Debug.LogError($"{name}: WardenEscort.data (WardenEscortSO) is not assigned.", this);
            anyError = true;
        }
        if (health == null)
        {
            Debug.LogError($"{name}: WardenEscort.health is not assigned or found.", this);
            anyError = true;
        }
        if (anyError) return;

        slots = new AITargetSelector[data.maxEscorts];
        slotHealth = new Health[data.maxEscorts];

        int mask = enemyLayers.value;
        if (mask == ~0 || mask == 0)
        {
            mask = LayerMask.GetMask("Enemy");
            if (mask == 0) mask = 1 << 7;
        }
        enemyLayers = mask;

        health.OnDied += HandleDied;
    }

    private void OnDestroy()
    {
        if (health != null) health.OnDied -= HandleDied;
        ReleaseAll();
    }

    private void Update()
    {
        if (anyError || isDead) return;
        if (Time.time < nextRecruitTime) return;
        nextRecruitTime = Time.time + data.recruitInterval;
        Recruit();
    }

    /// <summary>World position of formation slot <paramref name="slot" />, turning with the Warden.</summary>
    public Vector3 GetSlotPosition(int slot)
    {
        int count = Mathf.Max(1, data.maxEscorts);
        float angle = transform.eulerAngles.y + 360f * slot / count;
        return transform.position + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * data.ringRadius;
    }

    private bool IsSlotHeld(int i)
    {
        return slots[i] != null && slotHealth[i] != null && !slotHealth[i].IsDead && slots[i].EscortLeader == this;
    }

    private void Recruit()
    {
        int free = -1;
        for (int i = 0; i < slots.Length; i++)
        {
            if (IsSlotHeld(i)) continue;
            slots[i] = null;
            slotHealth[i] = null;
            if (free < 0) free = i;
        }
        if (free < 0) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, data.recruitRadius, enemyLayers, QueryTriggerInteraction.Collide);
        foreach (Collider hit in hits)
        {
            if (hit == null) continue;
            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if (enemy == null || enemy.transform == transform || !IsRecruitable(enemy.RosterId)) continue;

            AITargetSelector selector = enemy.GetComponent<AITargetSelector>();
            Health enemyHealth = enemy.GetComponent<Health>();
            if (selector == null || enemyHealth == null || enemyHealth.IsDead) continue;
            if (selector.EscortLeader != null || selector.TowerTarget != null) continue;

            selector.SetEscortLeader(this, free);
            slots[free] = selector;
            slotHealth[free] = enemyHealth;

            free = NextFreeSlot(free + 1);
            if (free < 0) return;
        }
    }

    private int NextFreeSlot(int from)
    {
        for (int i = from; i < slots.Length; i++)
        {
            if (slots[i] == null) return i;
        }
        return -1;
    }

    private bool IsRecruitable(string rosterId)
    {
        if (string.IsNullOrEmpty(rosterId) || data.recruitRosterIds == null) return false;
        foreach (string id in data.recruitRosterIds)
        {
            if (id == rosterId) return true;
        }
        return false;
    }

    private void HandleDied()
    {
        isDead = true;
        ReleaseAll();
    }

    private void ReleaseAll()
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].EscortLeader == this) slots[i].SetEscortLeader(null, 0);
            slots[i] = null;
            slotHealth[i] = null;
        }
    }
}
