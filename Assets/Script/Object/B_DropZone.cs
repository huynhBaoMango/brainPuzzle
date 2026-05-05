using UnityEngine;

/// <summary>
/// Marks a Collider2D as a named drop target. Interactable states whose
/// trigger is DRAG can require the player to release over a zone with a
/// matching <see cref="ZoneId"/>.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class B_DropZone : MonoBehaviour
{
    [Tooltip("Identifier that interactable states match against in their 'Required Zone Id' field. Case-sensitive.")]
    [SerializeField] private string zoneId;

    [Tooltip("When drop zones overlap at the drop point, the one with the HIGHEST Sort Order wins. Raise this on zones that should be 'on top' of others.")]
    [SerializeField] private int sortOrder;

    public string ZoneId => zoneId;
    public int SortOrder => sortOrder;

    // Drop-zone lookup is now centralised in B_InteractableObject.PickAt,
    // which resolves interactables and drop zones against the same sort
    // order number line. This class is pure data + scene gizmos.

    /// <summary>
    /// Called by Unity when the component is first added or the user clicks
    /// Reset. Copies the SpriteRenderer's sorting order (if present) so the
    /// drop zone's layer matches the visual by default.
    /// </summary>
    private void Reset()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sortOrder = sr.sortingOrder;
    }

    // ---- Scene-view visualization (editor only) ----

    private void OnDrawGizmos()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return;

        Color fill = new Color(0.25f, 0.85f, 1f, 0.18f);
        Color outline = new Color(0.25f, 0.85f, 1f, 0.9f);

        Bounds b = col.bounds;
        Gizmos.color = fill;
        Gizmos.DrawCube(b.center, b.size);
        Gizmos.color = outline;
        Gizmos.DrawWireCube(b.center, b.size);

#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(zoneId))
        {
            GUIStyle style = new GUIStyle(UnityEditor.EditorStyles.boldLabel);
            style.normal.textColor = outline;
            style.alignment = TextAnchor.MiddleCenter;
            UnityEditor.Handles.Label(b.center, $"⚓ {zoneId}", style);
        }
#endif
    }
}
