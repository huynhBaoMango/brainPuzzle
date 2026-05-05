using UnityEngine;

/// <summary>
/// A non-interactable scene object (background, furniture, cover panel, etc.).
/// Visual can be EITHER a SpriteRenderer OR a Spine SkeletonAnimation —
/// exactly the same dual-mode rule as B_InteractableObject.
/// If it has a <c>Collider2D</c>, it participates in layer-based blocking.
/// If it has no collider, it's purely visual and blocks nothing.
///
/// No states, no gestures, no actions. Just a visual + sort order.
/// </summary>
public class B_StaticObject : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Optional id so actions and the level exporter can reference this object by name. Required if any action targets this object.")]
    [SerializeField] private string objectId;

    [Header("Visibility")]
    [Tooltip("If true, this object starts invisible (alpha 0) and with colliders disabled. Use an Appear action from another object to reveal it.")]
    [SerializeField] private bool startHidden;

    [Header("Visual")]
    [Tooltip("Which renderer drives this static. Sprite = uses SpriteRenderer on this GameObject. Spine = uses SkeletonAnimation. Picked explicitly so an object with BOTH assigned doesn't render twice.")]
    [SerializeField] private VisualMode visualMode = VisualMode.Sprite;

    [Tooltip("Optional Spine skeleton. Only used when Visual Mode = Spine.")]
    [SerializeField] private Spine.Unity.SkeletonAnimation skeleton;

    [Tooltip("Spine animation name played on spawn. Only used when Visual Mode = Spine. Leave empty to keep the skeleton's default pose.")]
    [SpineAnim]
    [SerializeField] private string initSpineAnim;

    [Tooltip("Whether the init Spine animation should loop.")]
    [SerializeField] private bool initSpineLoop = true;

    public string ObjectId => objectId;
    public bool StartHidden => startHidden;

    /// <summary>Optional Spine skeleton reference. Used by the level exporter.</summary>
    public Spine.Unity.SkeletonAnimation Skeleton => skeleton;

    /// <summary>Init spine animation name. Used by the level exporter.</summary>
    public string InitSpineAnim => initSpineAnim;

    /// <summary>Whether the init spine animation loops. Used by the level exporter.</summary>
    public bool InitSpineLoop => initSpineLoop;

    /// <summary>Authored visual mode (Sprite or Spine). Used by the level exporter.</summary>
    public VisualMode VisualMode => visualMode;

    private void ApplyVisualMode()
    {
        bool useSprite = visualMode == VisualMode.Sprite;
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = useSprite;
        if (skeleton != null)
        {
            MeshRenderer mr = skeleton.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = !useSprite;
        }
    }

    private void OnValidate()
    {
        ApplyVisualMode();
    }

    /// <summary>
    /// Returns the sorting order from the renderer that matches the
    /// authored <see cref="visualMode"/>.
    /// </summary>
    public int GetSortOrder()
    {
        if (visualMode == VisualMode.Spine && skeleton != null)
        {
            MeshRenderer mr = skeleton.GetComponent<MeshRenderer>();
            if (mr != null) return mr.sortingOrder;
        }
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        return sr != null ? sr.sortingOrder : 0;
    }

    private void Awake()
    {
        ApplyVisualMode();

        // Play the init spine animation only if actually running in Spine mode.
        if (visualMode == VisualMode.Spine
            && skeleton != null && !string.IsNullOrEmpty(initSpineAnim))
            B_InteractableObject.PlaySpineAnim(skeleton, initSpineAnim, initSpineLoop);

        if (startHidden) HideImmediate();
    }

    private void HideImmediate()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Color c = sr.color;
            sr.color = new Color(c.r, c.g, c.b, 0f);
        }
        if (skeleton != null && skeleton.Skeleton != null)
            skeleton.Skeleton.A = 0f;

        Collider2D[] cols = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;
    }
}
