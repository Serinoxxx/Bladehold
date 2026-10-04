using UnityEngine;

/// <summary>
///     The Spearman's upper-body stance. It marches with spears held high and levels them forward
///     (braced) when the player closes in — from much further out when the player is mounted, so a
///     charging rider sees the wall of points before hitting it. The poses are static clips on the
///     animator's <see cref="SpearmanStanceSO.poseLayerName" /> layer (arms only, so legs keep
///     walking); this component just flips the bool and fades the layer out while the body is dead
///     or knocked down, where a frozen arm pose would look wrong. Stopping and hurting the horse is
///     the sibling <see cref="MountStopper" />'s job — this is purely the read.
/// </summary>
public class SpearmanStance : MonoBehaviour
{
    [SerializeField] private SpearmanStanceSO data;
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;
    [SerializeField] private KnockbackReceiver knockbackReceiver;

    private int poseLayer = -1;
    private int spearsForwardHash;
    private PlayerMount playerMount;
    private bool anyError = false;

    /// <summary>True while the spears are levelled forward.</summary>
    public bool IsBraced { get; private set; }

    private void OnValidate()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        if (health == null)
        {
            health = GetComponent<Health>();
        }
        if (knockbackReceiver == null)
        {
            knockbackReceiver = GetComponent<KnockbackReceiver>();
        }
    }

    private void Start()
    {
        if (data == null)
        {
            Debug.LogError("[SpearmanStance] SpearmanStanceSO is not assigned in the inspector.", this);
            anyError = true;
        }
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogError("[SpearmanStance] No Animator found in children.", this);
                anyError = true;
            }
        }
        if (health == null)
        {
            health = GetComponent<Health>();
            if (health == null)
            {
                Debug.LogError("[SpearmanStance] Health component is missing.", this);
                anyError = true;
            }
        }
        if (knockbackReceiver == null)
        {
            knockbackReceiver = GetComponent<KnockbackReceiver>();
        }

        if (anyError)
        {
            return;
        }

        poseLayer = animator.GetLayerIndex(data.poseLayerName);
        if (poseLayer < 0)
        {
            Debug.LogError($"[SpearmanStance] Animator controller has no '{data.poseLayerName}' layer — run Bladehold > Spearman > Build Pose Animations.", this);
            anyError = true;
            return;
        }
        spearsForwardHash = Animator.StringToHash(data.spearsForwardParam);

        if (Player.Instance != null)
        {
            playerMount = Player.Instance.GetComponent<PlayerMount>();
        }
    }

    private void Update()
    {
        if (anyError) return;

        bool canHold = !health.IsDead && (knockbackReceiver == null || !knockbackReceiver.IsIncapacitated);
        float weight = Mathf.MoveTowards(animator.GetLayerWeight(poseLayer), canHold ? 1f : 0f, data.layerFadeSpeed * Time.deltaTime);
        animator.SetLayerWeight(poseLayer, weight);

        bool braced = canHold && PlayerInBraceRange();
        if (braced != IsBraced)
        {
            IsBraced = braced;
            animator.SetBool(spearsForwardHash, braced);
        }
    }

    private bool PlayerInBraceRange()
    {
        Player player = Player.Instance;
        if (player == null || player.Health == null || player.Health.IsDead) return false;

        float range = playerMount != null && playerMount.IsMounted ? data.braceRangeMounted : data.braceRangeOnFoot;
        Vector3 offset = player.transform.position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= range * range;
    }
}
