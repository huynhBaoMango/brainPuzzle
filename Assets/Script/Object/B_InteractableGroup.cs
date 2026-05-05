using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// A pile/stack of identical objects (money bands, cards, etc.). Holds ONE
/// set of states + actions that apply to every member. When the player
/// interacts, the group picks the next child from the list and runs the
/// actions on that child's transform/sprite. Once a child is consumed
/// (destroyed or removed from the list), the next press picks the next one.
///
/// Children are plain GameObjects with a SpriteRenderer — no
/// B_InteractableObject needed. The group owns all the puzzle logic.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class B_InteractableGroup : MonoBehaviour
{
    public enum PickMode { First, Last, Random }

    // ============================================================
    //  INSPECTOR
    // ============================================================

    [Header("Members")]
    [Tooltip("Plain GameObjects (just sprites) in this group. The group picks one per interaction and runs its states/actions on that child.")]
    [SerializeField] private List<GameObject> members = new List<GameObject>();

    [Tooltip("How the next member is chosen.")]
    [SerializeField] private PickMode pickMode = PickMode.First;

    [Header("Layer")]
    [Tooltip("Sort order for layer-based interaction priority. Higher = on top. Must be above any background/static object that covers this group.")]
    [SerializeField] private int sortOrder;

    [Header("Data")]
    [Tooltip("Which renderer each member uses. All members in a group share this mode. Sprite = SpriteRenderer. Spine = SkeletonAnimation.")]
    [SerializeField] private VisualMode visualMode = VisualMode.Sprite;

    [Tooltip("States and actions — defined once here, applied to whichever child is picked.")]
    [SerializeField] private ObjectData data;

    [Header("Gesture Tuning")]
    [SerializeField] private float tapMaxDistance = 0.2f;
    [SerializeField] private float tapMaxDuration = 0.3f;
    [SerializeField] private float swipeMinDistance = 1.0f;
    [SerializeField] private float swipeMaxDuration = 0.4f;

    // ============================================================
    //  RUNTIME
    // ============================================================

    private GameObject activeChild;
    private SpriteRenderer activeRenderer;
    private Spine.Unity.SkeletonAnimation activeSkeleton;

    private Vector3 pointerDownWorld;
    private float pointerDownTime;
    private bool draggingFollow;
    private Vector3 dragGrabOffset;
    private Vector3 dragStartPosition;
    private Sprite spriteBeforeDrag;

    // ============================================================
    //  PUBLIC API (called by PickAt + B_PuzzleInput)
    // ============================================================

    public int GetSortOrder() => sortOrder;
    public ObjectData Data => data;
    public PickMode Mode => pickMode;
    public List<GameObject> Members => members;
    public VisualMode VisualMode => visualMode;

    /// <summary>
    /// Enables the renderer on each member that matches <see cref="visualMode"/>
    /// and disables the other. Runs at edit time and Awake so members with
    /// BOTH a sprite and spine assigned only show one.
    /// </summary>
    private void ApplyVisualModeToMembers()
    {
        if (members == null) return;
        bool useSprite = visualMode == VisualMode.Sprite;
        foreach (GameObject m in members)
        {
            if (m == null) continue;
            SpriteRenderer sr = m.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = useSprite;
            var skel = m.GetComponentInChildren<Spine.Unity.SkeletonAnimation>();
            if (skel != null)
            {
                MeshRenderer mr = skel.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = !useSprite;
            }
        }
    }

    private void OnValidate()
    {
        ApplyVisualModeToMembers();
    }

    private void Awake()
    {
        ApplyVisualModeToMembers();
    }

    public bool HasAvailableMembers()
    {
        if (members == null) return false;
        CleanNulls();
        return members.Count > 0;
    }

    public void HandlePress(Vector2 world)
    {
        activeChild = PickNextChild();
        if (activeChild == null) return;

        // Cache only the renderer matching the authored visual mode.
        if (visualMode == VisualMode.Spine)
        {
            activeRenderer = null;
            activeSkeleton = activeChild.GetComponentInChildren<Spine.Unity.SkeletonAnimation>();
        }
        else
        {
            activeRenderer = activeChild.GetComponent<SpriteRenderer>();
            activeSkeleton = null;
        }
        spriteBeforeDrag = null;

        pointerDownWorld = new Vector3(world.x, world.y, activeChild.transform.position.z);
        pointerDownTime = Time.time;
        dragStartPosition = activeChild.transform.position;

        if (HasEligibleDragState())
        {
            draggingFollow = true;
            dragGrabOffset = Vector3.zero;

            // Swap to drag sprite if set on the first DRAG state.
            if (activeRenderer != null && data?.states != null)
            {
                for (int i = 0; i < data.states.Count; i++)
                {
                    ObjectState s = data.states[i];
                    if (s.trigger != InteractType.DRAG) continue;
                    if (s.dragSprite != null)
                    {
                        spriteBeforeDrag = activeRenderer.sprite;
                        activeRenderer.sprite = s.dragSprite;
                    }
                    break;
                }
            }
        }
        else
        {
            draggingFollow = false;
        }
    }

    public void HandleDrag(Vector2 world)
    {
        if (!draggingFollow || activeChild == null) return;
        Vector3 target = new Vector3(world.x, world.y, 0f) + dragGrabOffset;
        target.z = activeChild.transform.position.z;
        activeChild.transform.position = target;
    }

    public void HandleRelease(Vector2 world)
    {
        if (activeChild == null) return;

        var (type, zoneId) = ClassifyGesture(world);
        bool activated = TryActivateMatching(type, zoneId);

        if (draggingFollow && !(type == InteractType.DRAG && activated))
        {
            activeChild.transform.position = dragStartPosition;

            // Revert to original sprite on snap-back.
            if (spriteBeforeDrag != null && activeRenderer != null)
                activeRenderer.sprite = spriteBeforeDrag;
        }

        spriteBeforeDrag = null;
        draggingFollow = false;
        if (!activated) activeChild = null;
    }

    // ============================================================
    //  CHILD PICKING
    // ============================================================

    private GameObject PickNextChild()
    {
        CleanNulls();
        if (members.Count == 0) return null;

        switch (pickMode)
        {
            case PickMode.Last:   return members[members.Count - 1];
            case PickMode.Random: return members[Random.Range(0, members.Count)];
            default:              return members[0];
        }
    }

    private void CleanNulls()
    {
        members.RemoveAll(m => m == null);
    }

    // ============================================================
    //  GESTURE CLASSIFICATION
    // ============================================================

    private (InteractType type, string zoneId) ClassifyGesture(Vector2 releaseWorld)
    {
        Vector3 delta = new Vector3(
            releaseWorld.x - pointerDownWorld.x,
            releaseWorld.y - pointerDownWorld.y, 0f);
        float distance = delta.magnitude;
        float duration = Time.time - pointerDownTime;

        if (distance <= tapMaxDistance && duration <= tapMaxDuration)
            return (InteractType.TAP, null);

        if (distance >= swipeMinDistance && duration <= swipeMaxDuration)
        {
            bool horizontal = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);
            if (horizontal)
                return (delta.x > 0f ? InteractType.SWIPE_RIGHT : InteractType.SWIPE_LEFT, null);
            return (delta.y > 0f ? InteractType.SWIPE_UP : InteractType.SWIPE_DOWN, null);
        }

        // Drop zone check at the CHILD's position (where it was dragged to).
        Vector2 dropPoint = activeChild != null
            ? (Vector2)activeChild.transform.position
            : releaseWorld;
        B_InteractableObject.LayerPick pick =
            B_InteractableObject.PickAt(dropPoint, transform);
        return (InteractType.DRAG, pick.dropZone != null ? pick.dropZone.ZoneId : null);
    }

    // ============================================================
    //  STATE MATCHING
    // ============================================================

    private bool HasEligibleDragState()
    {
        if (data == null || data.states == null) return false;
        for (int i = 0; i < data.states.Count; i++)
        {
            if (data.states[i].trigger == InteractType.DRAG) return true;
        }
        return false;
    }

    private bool TryActivateMatching(InteractType type, string zoneId)
    {
        if (data == null || data.states == null) return false;
        if (type == InteractType.NONE) return false;

        ObjectState almostMatched = null;

        for (int i = 0; i < data.states.Count; i++)
        {
            ObjectState s = data.states[i];
            // NO isDone check — the same state fires once per child.
            if (s.trigger != type) continue;

            if (type == InteractType.DRAG
                && !string.IsNullOrEmpty(s.requiredZoneId)
                && zoneId != s.requiredZoneId)
                continue;

            if (!RequirementsMet(s))
            {
                if (almostMatched == null) almostMatched = s;
                continue;
            }

            ActivateOnChild(s);
            return true;
        }

        if (almostMatched != null)
            B_InteractableObject.OnShowMessage?.Invoke(almostMatched.failMessageKey);

        return false;
    }

    /// <summary>Public wrapper so B_HintManager can test requirements.</summary>
    public bool CheckRequirements(ObjectState target) => RequirementsMet(target);

    private bool RequirementsMet(ObjectState target)
    {
        if (target.requirements == null || target.requirements.Count == 0) return true;
        for (int i = 0; i < target.requirements.Count; i++)
        {
            StateRequirement req = target.requirements[i];
            if (string.IsNullOrEmpty(req.objectId)) continue;

            bool done;
            B_InteractableObject other = B_InteractableObject.Find(req.objectId);
            if (other != null)
            {
                done = other.IsStateDone(req.stateId);
            }
            else
            {
                // Fall back to queues so requirements can reference queueIds.
                B_InteractableQueue queue = B_InteractableQueue.Find(req.objectId);
                if (queue == null) return false;
                done = queue.IsStateDone(req.stateId);
            }

            if (req.requireNotDone ? done : !done) return false;
        }
        return true;
    }

    // ============================================================
    //  ACTIVATION + ACTION RUNNER (targets the picked child)
    // ============================================================

    private void ActivateOnChild(ObjectState s)
    {
        B_InteractableObject.LockInput();
        StartCoroutine(ActivateOnChildRoutine(s));
    }

    private IEnumerator ActivateOnChildRoutine(ObjectState s)
    {
        try
        {
            // Spine: play the state anim on the active child's skeleton.
            // Sprite: swap the state sprite on the child.
            if (activeSkeleton != null && !string.IsNullOrEmpty(s.stateSpineAnim))
                B_InteractableObject.PlaySpineAnim(activeSkeleton, s.stateSpineAnim, s.stateSpineLoop);
            else if (activeRenderer != null && s.stateSprite != null)
                activeRenderer.sprite = s.stateSprite;

            B_InteractableObject.PlaySFXSafe(s.stateSFX);

            // Run actions on the child.
            if (s.actions != null && s.actions.Count > 0)
                yield return RunActions(s.actions);

            // Show success message + fire hook + Unity event.
            if (!string.IsNullOrEmpty(s.successMessageKey))
                B_InteractableObject.OnShowMessage?.Invoke(s.successMessageKey);

            // Consume the child — remove from list.
            if (activeChild != null)
                members.Remove(activeChild);
            activeChild = null;
            activeRenderer = null;
            activeSkeleton = null;
        }
        finally
        {
            B_InteractableObject.UnlockInput();
        }
    }

    private IEnumerator RunActions(List<StateAction> actions)
    {
        List<Coroutine> pending = new List<Coroutine>();
        for (int i = 0; i < actions.Count; i++)
        {
            StateAction a = actions[i];
            if (a == null) continue;

            Coroutine co = StartCoroutine(RunAction(a));
            if (a.runInParallel)
            {
                pending.Add(co);
            }
            else
            {
                yield return co;
                for (int p = 0; p < pending.Count; p++) yield return pending[p];
                pending.Clear();
            }
        }
        for (int p = 0; p < pending.Count; p++) yield return pending[p];
    }

    // ---- Target resolution ----
    // If actionTarget is set, the action operates on that GameObject.
    // Otherwise it operates on the picked child.

    private Transform ActionTransform(StateAction a) =>
        a.actionTarget != null ? a.actionTarget.transform
        : activeChild != null ? activeChild.transform
        : transform;

    private SpriteRenderer ActionRenderer(StateAction a) =>
        a.actionTarget != null ? a.actionTarget.GetComponent<SpriteRenderer>()
        : activeRenderer;

    private GameObject ActionGO(StateAction a) =>
        a.actionTarget != null ? a.actionTarget
        : activeChild != null ? activeChild
        : gameObject;

    // ---- Alpha helpers (sprite + spine aware) ----

    private static void SetAlphaShared(SpriteRenderer sr, Spine.Unity.SkeletonAnimation spine, float a)
    {
        if (sr != null)
        {
            Color c = sr.color;
            sr.color = new Color(c.r, c.g, c.b, a);
        }
        if (spine != null && spine.Skeleton != null)
            spine.Skeleton.A = a;
    }

    private static IEnumerator FadeAlphaShared(SpriteRenderer sr, Spine.Unity.SkeletonAnimation spine,
                                               float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            SetAlphaShared(sr, spine, a);
            yield return null;
        }
        SetAlphaShared(sr, spine, to);
    }

    // ---- Actions ----

    private IEnumerator RunAction(StateAction a)
    {
        switch (a.type)
        {
            case StateActionType.Wait:
                if (a.duration > 0f) yield return new WaitForSeconds(a.duration);
                break;

            case StateActionType.MoveTo:
                if (a.moveTarget != null)
                {
                    Transform t = ActionTransform(a);
                    Vector3 dest = a.moveTarget.position;
                    dest.z = t.position.z;
                    if (a.duration > 0f)
                        yield return t.DOMove(dest, a.duration).SetEase(a.ease).WaitForCompletion();
                    else
                        t.position = dest;
                }
                break;

            case StateActionType.Disappear:
            {
                GameObject go = ActionGO(a);
                SpriteRenderer sr = ActionRenderer(a);
                Spine.Unity.SkeletonAnimation spine = go != null
                    ? go.GetComponentInChildren<Spine.Unity.SkeletonAnimation>() : null;

                if (a.fadeOut && a.duration > 0f && (sr != null || spine != null))
                    yield return FadeAlphaShared(sr, spine, 1f, 0f, a.duration);
                else
                    SetAlphaShared(sr, spine, 0f);

                Collider2D[] cols = go.GetComponentsInChildren<Collider2D>(true);
                for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;
                if (a.destroyOnDisappear) Destroy(go);
                break;
            }

            case StateActionType.Appear:
            {
                GameObject go = ActionGO(a);
                SpriteRenderer sr = ActionRenderer(a);
                Spine.Unity.SkeletonAnimation spine = go != null
                    ? go.GetComponentInChildren<Spine.Unity.SkeletonAnimation>() : null;

                if (!go.activeSelf) go.SetActive(true);
                Collider2D[] cols = go.GetComponentsInChildren<Collider2D>(true);
                for (int i = 0; i < cols.Length; i++) cols[i].enabled = true;

                if (a.fadeIn && a.duration > 0f && (sr != null || spine != null))
                {
                    SetAlphaShared(sr, spine, 0f);
                    yield return FadeAlphaShared(sr, spine, 0f, 1f, a.duration);
                }
                else
                {
                    SetAlphaShared(sr, spine, 1f);
                }
                break;
            }

            case StateActionType.DoAnimation:
            {
                GameObject go = ActionGO(a);
                Spine.Unity.SkeletonAnimation spine = go.GetComponentInChildren<Spine.Unity.SkeletonAnimation>();
                if (spine != null && !string.IsNullOrEmpty(a.spineAnim))
                    B_InteractableObject.PlaySpineAnim(spine, a.spineAnim, a.spineLoop);
                if (a.duration > 0f) yield return new WaitForSeconds(a.duration);
                break;
            }

            case StateActionType.ActivateState:
                if (a.activateTarget != null)
                {
                    bool skipChain = a.chainGuards != null && a.chainGuards.Count > 0
                        && B_InteractableObject.AreRequirementsMet(a.chainGuards);
                    if (!skipChain)
                        a.activateTarget.ForceActivateState(a.activateStateId);
                }
                break;

            case StateActionType.AdvanceQueue:
                if (a.queueTarget != null)
                    a.queueTarget.ServeHead(a.queueServeStateId);
                break;

            case StateActionType.PlaySFX:
                B_InteractableObject.PlaySFXSafe(a.sfxClip);
                if (a.duration > 0f) yield return new WaitForSeconds(a.duration);
                break;

            case StateActionType.SkinChange:
                ApplySkinChange(a);
                break;
        }
    }

    private static void ApplySkinChange(StateAction a)
    {
        if (a.skinTarget == null || string.IsNullOrEmpty(a.skinName)) return;
        var set = a.skinTarget.GetComponent<B_SpineSkinSet>();
        if (set == null) return;
        switch (a.skinOp)
        {
            case SkinOp.Add:    set.AddSkin(a.skinName);    break;
            case SkinOp.Remove: set.RemoveSkin(a.skinName); break;
            case SkinOp.Toggle: set.ToggleSkin(a.skinName); break;
        }
    }
}
