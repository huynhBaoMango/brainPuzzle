using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Shared editor utilities for puzzle drawers — scene-scanning helpers
/// and a popup control that highlights invalid / unrecognised values.
/// </summary>
public static class PuzzleEditorHelper
{
    // ================================================================
    //  SPINE ANIMATION NAMES
    // ================================================================

    /// <summary>
    /// Returns the union of animation names across all SkeletonDataAssets
    /// reachable from the given owner:
    ///  - B_InteractableObject / B_StaticObject → their single skeleton
    ///  - B_InteractableGroup → skeletons on any member GameObject
    ///  - GameObject → skeleton on the GameObject or any child
    /// Returns an empty array if no skeletons are found.
    /// </summary>
    public static string[] GetSpineAnimNamesForOwner(Object owner)
    {
        if (owner == null) return new string[0];

        var assets = new List<Spine.Unity.SkeletonDataAsset>();

        if (owner is B_InteractableObject io && io.Skeleton != null)
            TryAdd(assets, io.Skeleton.SkeletonDataAsset);
        else if (owner is B_StaticObject so && so.Skeleton != null)
            TryAdd(assets, so.Skeleton.SkeletonDataAsset);
        else if (owner is B_InteractableGroup grp && grp.Members != null)
        {
            foreach (GameObject m in grp.Members)
            {
                if (m == null) continue;
                var s = m.GetComponentInChildren<Spine.Unity.SkeletonAnimation>();
                if (s != null) TryAdd(assets, s.SkeletonDataAsset);
            }
        }
        else if (owner is B_InteractableQueue q && q.Members != null)
        {
            foreach (GameObject m in q.Members)
            {
                if (m == null) continue;
                var s = m.GetComponentInChildren<Spine.Unity.SkeletonAnimation>();
                if (s != null) TryAdd(assets, s.SkeletonDataAsset);
            }
        }
        else if (owner is GameObject go)
        {
            // Generic GameObject - look for SkeletonAnimation on self or children
            var s = go.GetComponentInChildren<Spine.Unity.SkeletonAnimation>();
            if (s != null) TryAdd(assets, s.SkeletonDataAsset);
        }

        if (assets.Count == 0) return new string[0];

        var names = new SortedSet<string>();
        foreach (var a in assets)
        {
            var sd = a.GetSkeletonData(true);
            if (sd == null) continue;
            foreach (var anim in sd.Animations)
                names.Add(anim.Name);
        }

        string[] result = new string[names.Count];
        names.CopyTo(result);
        return result;
    }

    private static void TryAdd(List<Spine.Unity.SkeletonDataAsset> list, Spine.Unity.SkeletonDataAsset a)
    {
        if (a != null && !list.Contains(a)) list.Add(a);
    }

    /// <summary>
    /// Returns the union of skin names across all SkeletonDataAssets
    /// reachable from the given owner. Mirrors
    /// <see cref="GetSpineAnimNamesForOwner"/>:
    /// <list type="bullet">
    /// <item>B_SpineSkinSet → its sibling SkeletonAnimation's data</item>
    /// <item>B_InteractableObject / B_StaticObject → their single skeleton</item>
    /// <item>B_InteractableGroup / B_InteractableQueue → skeletons on members</item>
    /// </list>
    /// </summary>
    public static string[] GetSpineSkinNamesForOwner(Object owner)
    {
        if (owner == null) return new string[0];

        var assets = new List<Spine.Unity.SkeletonDataAsset>();

        if (owner is B_SpineSkinSet skinSet)
        {
            var skel = skinSet.GetComponent<Spine.Unity.SkeletonAnimation>();
            if (skel != null) TryAdd(assets, skel.SkeletonDataAsset);
        }
        else if (owner is B_InteractableObject io && io.Skeleton != null)
        {
            TryAdd(assets, io.Skeleton.SkeletonDataAsset);
        }
        else if (owner is B_StaticObject so && so.Skeleton != null)
        {
            TryAdd(assets, so.Skeleton.SkeletonDataAsset);
        }
        else if (owner is B_InteractableGroup grp && grp.Members != null)
        {
            foreach (GameObject m in grp.Members)
            {
                if (m == null) continue;
                var s = m.GetComponentInChildren<Spine.Unity.SkeletonAnimation>();
                if (s != null) TryAdd(assets, s.SkeletonDataAsset);
            }
        }
        else if (owner is B_InteractableQueue q && q.Members != null)
        {
            foreach (GameObject m in q.Members)
            {
                if (m == null) continue;
                var s = m.GetComponentInChildren<Spine.Unity.SkeletonAnimation>();
                if (s != null) TryAdd(assets, s.SkeletonDataAsset);
            }
        }
        else if (owner is GameObject go)
        {
            var s = go.GetComponent<Spine.Unity.SkeletonAnimation>();
            if (s != null) TryAdd(assets, s.SkeletonDataAsset);
        }

        if (assets.Count == 0) return new string[0];

        var names = new SortedSet<string>();
        foreach (var a in assets)
        {
            var sd = a.GetSkeletonData(true);
            if (sd == null) continue;
            foreach (var skin in sd.Skins)
                names.Add(skin.Name);
        }

        string[] result = new string[names.Count];
        names.CopyTo(result);
        return result;
    }

    // ================================================================
    //  OWNER VISUAL MODE
    // ================================================================

    /// <summary>
    /// Resolves the VisualMode of the MonoBehaviour that owns a nested
    /// ObjectData / ObjectState / StateAction. Used by drawers to hide the
    /// fields that don't apply in the active mode (sprite fields under Spine
    /// mode, and vice versa).
    /// </summary>
    public static VisualMode GetOwnerVisualMode(Object owner)
    {
        if (owner is B_InteractableObject io) return io.VisualMode;
        if (owner is B_InteractableGroup g) return g.VisualMode;
        if (owner is B_InteractableQueue q) return q.VisualMode;
        if (owner is B_StaticObject s) return s.VisualMode;
        return VisualMode.Sprite; // safe default
    }

    // ================================================================
    //  SCENE-SCANNING
    // ================================================================

    /// <summary>
    /// Returns sorted, non-empty objectIds of every B_InteractableObject AND
    /// queueIds of every B_InteractableQueue currently in the scene. Used by
    /// the StateRequirement drawer so requirements can reference either kind.
    /// </summary>
    public static string[] GetAllObjectIds()
    {
        var ids = new List<string>();

        var objs = Object.FindObjectsByType<B_InteractableObject>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < objs.Length; i++)
        {
            string id = objs[i].ObjectId;
            if (!string.IsNullOrEmpty(id) && !ids.Contains(id))
                ids.Add(id);
        }

        var queues = Object.FindObjectsByType<B_InteractableQueue>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < queues.Length; i++)
        {
            string id = queues[i].QueueId;
            if (!string.IsNullOrEmpty(id) && !ids.Contains(id))
                ids.Add(id);
        }

        ids.Sort();
        return ids.ToArray();
    }

    /// <summary>
    /// Returns all state ids (including initStateId if non-empty) for the
    /// B_InteractableObject whose objectId matches <paramref name="objectId"/>.
    /// </summary>
    public static string[] GetStateIds(string objectId)
    {
        if (string.IsNullOrEmpty(objectId)) return new string[0];

        // Look up as interactable first.
        var objs = Object.FindObjectsByType<B_InteractableObject>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < objs.Length; i++)
        {
            if (objs[i].ObjectId != objectId) continue;
            return GetStateIdsFromData(objs[i].Data);
        }

        // Fall back to queues so requirements / ActivateState popups can
        // target queue states too.
        var queues = Object.FindObjectsByType<B_InteractableQueue>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < queues.Length; i++)
        {
            if (queues[i].QueueId != objectId) continue;
            return GetStateIdsFromData(queues[i].Data);
        }

        return new string[0];
    }

    /// <summary>
    /// Returns all state ids on an <see cref="ObjectData"/> asset
    /// (including the init state id if set). Used by drawers that have a
    /// direct component reference rather than looking up by object id.
    /// </summary>
    public static string[] GetStateIdsFromData(ObjectData data)
    {
        if (data == null) return new string[0];

        var ids = new List<string>();
        if (!string.IsNullOrEmpty(data.initStateId))
            ids.Add(data.initStateId);

        if (data.states != null)
        {
            for (int s = 0; s < data.states.Count; s++)
            {
                string sid = data.states[s].stateId;
                if (!string.IsNullOrEmpty(sid) && !ids.Contains(sid))
                    ids.Add(sid);
            }
        }
        return ids.ToArray();
    }

    /// <summary>
    /// Returns sorted, non-empty zoneIds of every B_DropZone in the scene.
    /// </summary>
    public static string[] GetAllZoneIds()
    {
        var zones = Object.FindObjectsByType<B_DropZone>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        var ids = new List<string>();
        for (int i = 0; i < zones.Length; i++)
        {
            string id = zones[i].ZoneId;
            if (!string.IsNullOrEmpty(id) && !ids.Contains(id))
                ids.Add(id);
        }
        ids.Sort();
        return ids.ToArray();
    }

    // ================================================================
    //  POPUP WITH WARNING
    // ================================================================

    /// <summary>
    /// Draws a string popup backed by <paramref name="options"/>.
    /// The first entry is always <paramref name="noneLabel"/> (maps to "").
    /// If the current value doesn't match any option, it's shown with a
    /// ⚠ prefix in orange so typos are immediately visible.
    /// </summary>
    public static void StringPopupField(
        Rect rect,
        SerializedProperty prop,
        string[] options,
        string noneLabel)
    {
        string current = prop.stringValue;

        // Build display array: [noneLabel, option0, option1, ...]
        string[] display = new string[options.Length + 1];
        display[0] = noneLabel;
        for (int i = 0; i < options.Length; i++)
            display[i + 1] = options[i];

        // Find selected index.
        int selectedIndex = 0;
        if (!string.IsNullOrEmpty(current))
        {
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] == current)
                {
                    selectedIndex = i + 1;
                    break;
                }
            }
        }

        // If current value isn't in the list, show it as a warning entry.
        bool isUnknown = !string.IsNullOrEmpty(current) && selectedIndex == 0;
        if (isUnknown)
        {
            // Prepend a warning entry at index 1.
            var extended = new string[display.Length + 1];
            extended[0] = display[0]; // noneLabel
            extended[1] = $"\u26a0 {current}"; // ⚠ unknown_value
            for (int i = 1; i < display.Length; i++)
                extended[i + 1] = display[i];
            display = extended;
            selectedIndex = 1; // point to the warning entry

            // Draw with orange tint.
            Color prev = GUI.color;
            GUI.color = new Color(1f, 0.75f, 0.3f);
            int chosen = EditorGUI.Popup(rect, selectedIndex, display);
            GUI.color = prev;

            if (chosen != selectedIndex)
            {
                if (chosen == 0)
                    prop.stringValue = "";
                else if (chosen == 1)
                    { } // re-selected the warning entry — keep current
                else
                    prop.stringValue = options[chosen - 2];
            }
        }
        else
        {
            int chosen = EditorGUI.Popup(rect, selectedIndex, display);
            if (chosen != selectedIndex)
            {
                prop.stringValue = chosen == 0 ? "" : options[chosen - 1];
            }
        }
    }
}
