using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Debug helper: at scene start, hides every renderer whose GameObject (or
/// any ancestor) is NOT registered as a puzzle component
/// (<see cref="B_InteractableObject"/>, <see cref="B_StaticObject"/>,
/// <see cref="B_InteractableGroup"/>, <see cref="B_InteractableQueue"/>,
/// <see cref="B_DropZone"/>, <see cref="B_LevelConfig"/>) and isn't a
/// member / slot of a Group or Queue.
///
/// <para>
/// Why: LibGDX runtime only renders objects that come from level JSON,
/// which only includes things with the above components. If a designer
/// forgets to add the marker component on Unity side, the visual still
/// shows in the editor (because Unity renders the SpriteRenderer /
/// SkeletonAnimation directly) — but it disappears in the LibGDX game.
/// This component reproduces that hiding behavior in Unity Play mode so
/// missing components are caught immediately, before export.
/// </para>
///
/// Drop on any GameObject in the scene (or attach to B_LevelConfig).
/// Inspector buttons let you preview / run from edit time too.
/// </summary>
public class B_OrphanVisualHider : MonoBehaviour
{
    [Tooltip("Run automatically when the scene starts.")]
    [SerializeField] private bool runOnAwake = true;

    [Tooltip("Print a warning per hidden renderer to the Console (with object reference for double-click navigation).")]
    [SerializeField] private bool logWarnings = true;

    [Tooltip("Skip GameObjects that live under a Canvas. UI graphics are never part of the LibGDX puzzle scene.")]
    [SerializeField] private bool skipUI = true;

    private void Awake()
    {
        if (runOnAwake) HideOrphans();
    }

    /// <summary>
    /// Walk the scene, find renderers that don't belong to any puzzle
    /// component, and disable them. Returns the number hidden.
    /// </summary>
    [ContextMenu("Hide Orphan Visuals Now")]
    public int HideOrphans()
    {
        HashSet<GameObject> valid = BuildValidSet();
        int hidden = 0;

        // SpriteRenderer
        var sprites = Object.FindObjectsByType<SpriteRenderer>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < sprites.Length; i++)
        {
            SpriteRenderer sr = sprites[i];
            if (skipUI && sr.GetComponentInParent<Canvas>() != null) continue;
            if (IsAncestorValid(sr.gameObject, valid)) continue;

            sr.enabled = false;
            hidden++;
            if (logWarnings)
                Debug.LogWarning(
                    $"[OrphanHider] Hid SpriteRenderer on '{sr.name}' — " +
                    "no puzzle component on it or any ancestor. Add one of " +
                    "B_InteractableObject / B_StaticObject / register it as " +
                    "a Group/Queue member, or it won't appear in LibGDX either.",
                    sr);
        }

        // SkeletonAnimation (Spine) — disable its MeshRenderer.
        var skels = Object.FindObjectsByType<Spine.Unity.SkeletonAnimation>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < skels.Length; i++)
        {
            var sa = skels[i];
            if (IsAncestorValid(sa.gameObject, valid)) continue;

            MeshRenderer mr = sa.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
            hidden++;
            if (logWarnings)
                Debug.LogWarning(
                    $"[OrphanHider] Hid Spine '{sa.name}' — no puzzle " +
                    "component on it or any ancestor. Add B_InteractableObject / " +
                    "B_StaticObject (Spine mode + skeleton ref) or register as " +
                    "Group/Queue member.",
                    sa);
        }

        if (hidden > 0)
            Debug.LogWarning($"[OrphanHider] Hid {hidden} orphan visual(s) at scene start. Add the missing puzzle components to make them export.");

        return hidden;
    }

    private static bool IsAncestorValid(GameObject go, HashSet<GameObject> valid)
    {
        Transform t = go != null ? go.transform : null;
        while (t != null)
        {
            if (valid.Contains(t.gameObject)) return true;
            t = t.parent;
        }
        return false;
    }

    private static HashSet<GameObject> BuildValidSet()
    {
        var set = new HashSet<GameObject>();

        foreach (var io in Object.FindObjectsByType<B_InteractableObject>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
            set.Add(io.gameObject);

        foreach (var so in Object.FindObjectsByType<B_StaticObject>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
            set.Add(so.gameObject);

        foreach (var grp in Object.FindObjectsByType<B_InteractableGroup>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            set.Add(grp.gameObject);
            if (grp.Members != null)
                foreach (var m in grp.Members)
                    if (m != null) set.Add(m);
        }

        foreach (var q in Object.FindObjectsByType<B_InteractableQueue>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            set.Add(q.gameObject);
            if (q.Members != null)
                foreach (var m in q.Members)
                    if (m != null) set.Add(m);
            if (q.Slots != null)
                foreach (var s in q.Slots)
                    if (s != null) set.Add(s.gameObject);
        }

        foreach (var dz in Object.FindObjectsByType<B_DropZone>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
            set.Add(dz.gameObject);

        foreach (var cfg in Object.FindObjectsByType<B_LevelConfig>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
            set.Add(cfg.gameObject);

        return set;
    }
}
