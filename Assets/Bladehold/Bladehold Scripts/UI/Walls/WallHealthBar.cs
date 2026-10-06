using MoreMountains.Tools;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     World-space HP bar floating over a <see cref="WallStructure" /> (plan 17): an
///     <see cref="MMProgressBar" /> (with its delayed bar, so a hit visibly drains), the tier name and HP,
///     and the element's colour on the frame. It fades in while the player is near or for a few seconds
///     after the wall is hit, and always faces the camera. Lives on a world-space canvas child of the
///     wall prefab.
/// </summary>
public class WallHealthBar : MonoBehaviour
{
    [SerializeField] private WallStructure wall;
    [SerializeField] private MMProgressBar progressBar;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI label;
    [Tooltip("Frame/accent image tinted to the wall's element (white for none).")]
    [SerializeField] private Image elementAccent;
    [SerializeField] private float showWithinDistance = 14f;
    [SerializeField] private float showAfterHitSeconds = 4f;
    [SerializeField] private float fadeSpeed = 4f;
    [Tooltip("Minimum height above the wall's base. The bar also rises to clear the top of the wall's visible model (see Clearance Above Model), so a taller tier or a hand-fitted gatehouse never pokes through it.")]
    [SerializeField] private float height = 7.5f;
    [Tooltip("Gap kept between the top of the wall's tallest visible mesh and the bar.")]
    [SerializeField] private float clearanceAboveModel = 1.5f;

    private float lastHitTime = -999f;
    private float lastHealth = -1f;
    private bool anyError;
    private Camera cam;

    private void OnValidate()
    {
        if (wall == null) wall = GetComponentInParent<WallStructure>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Start()
    {
        if (wall == null) { Debug.LogError($"[WallHealthBar] {name}: wall is not assigned.", this); anyError = true; }
        if (progressBar == null) { Debug.LogError($"[WallHealthBar] {name}: progressBar is not assigned.", this); anyError = true; }
        if (canvasGroup == null) { Debug.LogError($"[WallHealthBar] {name}: canvasGroup is not assigned.", this); anyError = true; }
        if (label == null) Debug.LogError($"[WallHealthBar] {name}: label is not assigned.", this);
        if (anyError) return;

        wall.OnStateChanged += HandleStateChanged;
        canvasGroup.alpha = 0f;
        PlaceAboveModel();
        HandleStateChanged(wall);
    }

    private void OnDestroy()
    {
        if (wall != null) wall.OnStateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(WallStructure w)
    {
        if (anyError || w.Health == null) return;
        float cur = w.IsStanding ? w.Health.CurrentHealth : 0f;
        float max = Mathf.Max(1f, w.Health.MaxHealth);
        if (lastHealth >= 0f && cur < lastHealth) lastHitTime = Time.time;
        lastHealth = cur;

        progressBar.UpdateBar(cur, 0f, max);
        if (label != null)
        {
            string door = w.IsStanding && !w.IsBlocking ? "  <color=#9CFF9C>OPEN</color>" : "";
            label.text = w.IsStanding ? $"{w.TierName} Wall  {Mathf.CeilToInt(cur)}/{Mathf.CeilToInt(max)}{door}" : "<color=#FF6B6B>BREACHED</color>";
        }
        if (elementAccent != null) elementAccent.color = w.Upgrades.HasElement ? w.Upgrades.element.Tint() : Color.white;
    }

    /// <summary>
    ///     Puts the bar at <see cref="height" /> over the wall's base, then raises it until it clears the top
    ///     of the tallest visible mesh under the wall by <see cref="clearanceAboveModel" />.
    /// </summary>
    private void PlaceAboveModel()
    {
        transform.localPosition = new Vector3(0f, height, 0f);
        float top = float.NegativeInfinity;
        foreach (Renderer r in wall.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || !(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
            top = Mathf.Max(top, r.bounds.max.y);
        }
        if (float.IsNegativeInfinity(top)) return;
        float wanted = top + clearanceAboveModel;
        if (transform.position.y < wanted) transform.position += Vector3.up * (wanted - transform.position.y);
    }

    private void LateUpdate()
    {
        if (anyError) return;
        if (cam == null) cam = Camera.main;

        bool near = Player.Instance != null &&
                    (Player.Instance.transform.position - wall.transform.position).sqrMagnitude < showWithinDistance * showWithinDistance;
        bool recentlyHit = Time.time - lastHitTime < showAfterHitSeconds;
        bool show = wall.IsStanding && (near || recentlyHit || wall.HealthFraction < 0.999f && near);
        // Re-fit each time the bar fades in: the model assembles from the ground when built and swaps
        // on tier upgrades, so its height at Start isn't final.
        if (show && canvasGroup.alpha <= 0f) PlaceAboveModel();
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, show ? 1f : 0f, fadeSpeed * Time.deltaTime);

        if (cam != null && canvasGroup.alpha > 0f)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);
        }
    }
}
