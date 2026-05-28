using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// A single-file line / queue of otherwise-identical actors (NPCs waiting to
/// be served, items in a conveyor, etc.). Like <see cref="B_InteractableGroup"/>
/// it owns ONE state machine shared by every member — but unlike a group, it
/// serves members **sequentially**: only the head member acts, and after it is
/// consumed the rest tween up to the previous slot positions.
///
/// <para>
/// Typical setup:
/// <list type="bullet">
/// <item>Queue GameObject with this component (and a BoxCollider2D only if the
///     queue itself should be tappable — otherwise place a standalone
///     <see cref="B_DropZone"/> at slot[0] for drag targets).</item>
/// <item>Child GameObjects = members, each with a SpriteRenderer or
///     SkeletonAnimation.</item>
/// <item>Slot anchors = empty Transforms positioning each standing spot in
///     world space. slots[i] is where members[i] stands; slots[0] is the head.</item>
/// <item>One <see cref="ObjectData"/> asset authored with the "served" state
///     (DoAnimation, Disappear, etc.) that runs on the head on each serve.</item>
/// </list>
/// </para>
///
/// <para>
/// External triggers (e.g. a money interactable's DRAG state) advance the
/// queue via <see cref="ServeHead"/> — typically wired through the
/// <c>AdvanceQueue</c> action type on <see cref="StateAction"/>.
/// </para>
/// </summary>
public class B_InteractableQueue : MonoBehaviour
{
    // ============================================================
    //  INSPECTOR
    // ============================================================

    [Header("Identity")]
    [Tooltip("Optional id so actions / win conditions can reference this queue.")]
    [SerializeField] private string queueId;

    [Header("Members")]
    [Tooltip("Ordered list. Index 0 is the HEAD — the one that gets served next. " +
             "Each member is a plain GameObject with a SpriteRenderer or SkeletonAnimation.")]
    [SerializeField] private List<GameObject> members = new List<GameObject>();

    [Tooltip("Slot anchor transforms. slots[i] is where members[i] stands. " +
             "slots[0] is the 'head' position where the drop zone typically sits. " +
             "If you have tail followers, slots must be long enough to cover " +
             "members.Count + tailFollowers.Count (the followers occupy the " +
             "trailing slots).")]
    [SerializeField] private List<Transform> slots = new List<Transform>();

    [Tooltip("Optional. GameObjects that sit BEHIND the line and shift up with " +
             "every serve, but are never themselves served. Use for a character " +
             "that visually trails the queue (e.g. a princess walking behind a " +
             "line of beggars). Follower i stands at slots[members.Count + i] " +
             "at any given time, so as members shrink, followers move up too.")]
    [SerializeField] private List<GameObject> tailFollowers = new List<GameObject>();

    [Tooltip("If true, members snap to their slot positions on Awake so authoring can be sloppy.")]
    [SerializeField] private bool snapMembersToSlotsOnStart = true;

    [Header("Shift Tween")]
    [Tooltip("How long the shift-up tween takes after the head is consumed.")]
    [SerializeField] private float shiftDuration = 0.35f;

    [Tooltip("Easing for the shift-up tween.")]
    [SerializeField] private Ease shiftEase = Ease.InOutQuad;

    [Header("Layer / Visual")]
    [Tooltip("Sort order for this queue's own collider (if any). Ignored if the queue has no BoxCollider2D.")]
    [SerializeField] private int sortOrder;

    [Tooltip("Which renderer each member uses. All members share the mode. Sprite = SpriteRenderer. Spine = SkeletonAnimation.")]
    [SerializeField] private VisualMode visualMode = VisualMode.Sprite;

    [Header("Data")]
    [Tooltip("Shared state machine. The head member runs these actions when served.")]
    [SerializeField] private ObjectData data;

    [Header("Empty Chain (optional)")]
    [Tooltip("When the LAST member is served and the queue empties, this state is force-activated on the target. Leave empty for no chain.")]
    [SerializeField] private B_InteractableObject queueEmptyTarget;
    [SerializeField] private string queueEmptyStateId;

    // ============================================================
    //  PUBLIC ACCESSORS
    // ============================================================

    // ============================================================
    //  REGISTRY (for cross-object requirement lookup)
    // ============================================================

    private static readonly Dictionary<string, B_InteractableQueue> registry =
        new Dictionary<string, B_InteractableQueue>();

    /// <summary>Looks up a queue by its QueueId. Mirrors B_InteractableObject.Find.</summary>
    public static B_InteractableQueue Find(string queueId)
    {
        if (string.IsNullOrEmpty(queueId)) return null;
        registry.TryGetValue(queueId, out var inst);
        return inst;
    }

    public string QueueId => queueId;
    public int GetSortOrder() => sortOrder;
    public ObjectData Data => data;
    public List<GameObject> Members => members;
    public List<Transform> Slots => slots;
    public List<GameObject> TailFollowers => tailFollowers;
    public VisualMode VisualMode => visualMode;
    public float ShiftDuration => shiftDuration;
    public Ease ShiftEase => shiftEase;
    public B_InteractableObject QueueEmptyTarget => queueEmptyTarget;
    public string QueueEmptyStateId => queueEmptyStateId;
    public bool SnapMembersToSlotsOnStart => snapMembersToSlotsOnStart;

    // ============================================================
    //  LIFECYCLE
    // ============================================================

    private void OnValidate()
    {
        ApplyVisualModeToMembers();
    }

    // Delta-based shift state. Captured once at Awake.
    // - followerInitialPositions[i]: where the follower's transform was
    //   when the scene loaded — i.e. wherever the designer placed her.
    //   We don't move her at start; we just remember this point.
    // - initialMemberCount: members.Count at Awake (after CleanNulls).
    //   Used to compute how many "slot steps" the queue has advanced.
    //
    // On every shift, the follower's transform moves by exactly
    // (SlotPosClamped(currentSlotIdx) - SlotPosClamped(initialSlotIdx)) —
    // so her visual relationship to the line is preserved no matter what
    // her bone / collider / pivot offset is. Authoring rule: place her
    // visually where you want her initial position; the queue handles
    // the rest.
    private Vector3[] followerInitialPositions;
    private int initialMemberCount;

    private void Awake()
    {
        ApplyVisualModeToMembers();
        CleanNulls();
        CaptureFollowerOffsets();
        if (snapMembersToSlotsOnStart) SnapToSlots();
        if (!string.IsNullOrEmpty(queueId)) registry[queueId] = this;
    }

    private void CaptureFollowerOffsets()
    {
        initialMemberCount = members != null ? members.Count : 0;
        if (tailFollowers == null || tailFollowers.Count == 0)
        {
            followerInitialPositions = null;
            return;
        }
        followerInitialPositions = new Vector3[tailFollowers.Count];
        for (int i = 0; i < tailFollowers.Count; i++)
        {
            GameObject f = tailFollowers[i];
            followerInitialPositions[i] = f != null
                ? f.transform.position : Vector3.zero;
        }
    }

    /// <summary>
    /// Returns the position of slot[idx], extrapolating past the array
    /// using the last two slots' delta when the index runs out of range.
    /// </summary>
    private Vector3 SlotPosClamped(int idx)
    {
        if (slots == null || slots.Count == 0) return Vector3.zero;
        if (idx < 0) idx = 0;
        if (idx < slots.Count)
        {
            Transform s = slots[idx];
            return s != null ? s.position : Vector3.zero;
        }
        Transform last = slots[slots.Count - 1];
        if (last == null) return Vector3.zero;
        if (slots.Count < 2) return last.position;
        Transform prev = slots[slots.Count - 2];
        if (prev == null) return last.position;
        Vector3 step = last.position - prev.position;
        return last.position + step * (idx - (slots.Count - 1));
    }

    /// <summary>
    /// Where follower <paramref name="i"/>'s transform should sit RIGHT NOW.
    /// Returns the authored initial position offset by the slot-delta
    /// accumulated since Awake. The follower's bone / collider / pivot
    /// offset is implicitly preserved because we never move her relative
    /// to her authored position — only by the same vector every member
    /// would shift.
    /// </summary>
    private Vector3 ComputeFollowerTargetPos(int i)
    {
        if (followerInitialPositions == null || i >= followerInitialPositions.Length)
            return Vector3.zero;

        int currentMemberCount = members != null ? members.Count : 0;
        int initialSlotIdx = initialMemberCount + i;
        int currentSlotIdx = currentMemberCount + i;

        Vector3 slotDelta = SlotPosClamped(currentSlotIdx) - SlotPosClamped(initialSlotIdx);
        return followerInitialPositions[i] + slotDelta;
    }

    private void OnDestroy()
    {
        if (!string.IsNullOrEmpty(queueId)
            && registry.TryGetValue(queueId, out var cur) && cur == this)
            registry.Remove(queueId);
    }

    // ============================================================
    //  STATE "IS DONE" QUERY (for cross-object requirements)
    // ============================================================

    /// <summary>
    /// Queue-specific "is done" semantic, built to match designers' mental
    /// model of a line:
    ///
    /// <list type="bullet">
    /// <item><b>init state id</b> → <c>true</c> only while the queue is
    ///     untouched (no state has fired yet).</item>
    /// <item><b>Any other state id</b> → <c>true</c> only when the queue is
    ///     EMPTY <em>and</em> that state has fired at least once. I.e. all
    ///     members have been served through this state. This differs from
    ///     <see cref="B_InteractableObject.IsStateDone"/>, where one activation
    ///     is enough — a queue needs every member served before it counts.</item>
    /// </list>
    ///
    /// Designers checking "at least one served" should use
    /// <c>init is Not Done</c>.
    /// </summary>
    public bool IsStateDone(string stateId)
    {
        if (data == null || string.IsNullOrEmpty(stateId)) return false;

        if (!string.IsNullOrEmpty(data.initStateId) && stateId == data.initStateId)
        {
            if (data.states == null) return true;
            for (int i = 0; i < data.states.Count; i++)
                if (data.states[i].isDone) return false;
            return true;
        }

        // Non-init: the queue must be empty AND this state must have fired.
        CleanNulls();
        if (members != null && members.Count > 0) return false;

        if (data.states != null)
        {
            for (int i = 0; i < data.states.Count; i++)
            {
                ObjectState st = data.states[i];
                if (st != null && st.stateId == stateId) return st.isDone;
            }
        }
        return false;
    }

    /// <summary>
    /// Raw "has this serve-state fired at least once" check, WITHOUT the
    /// empty-queue gate that <see cref="IsStateDone"/> applies. Used by
    /// milestone counting (Required Count > 0) so partial progress is
    /// visible as members are served one by one — e.g. "after 3 foods fed"
    /// fires on the 3rd serve instead of only once the whole queue empties.
    /// </summary>
    public bool HasStateFired(string stateId)
    {
        if (data == null || data.states == null || string.IsNullOrEmpty(stateId))
            return false;
        for (int i = 0; i < data.states.Count; i++)
        {
            ObjectState st = data.states[i];
            if (st != null && st.stateId == stateId) return st.isDone;
        }
        return false;
    }

    private void SnapToSlots()
    {
        if (slots == null) return;
        int memberN = members != null ? Mathf.Min(members.Count, slots.Count) : 0;
        for (int i = 0; i < memberN; i++)
        {
            if (members[i] == null || slots[i] == null) continue;
            Vector3 p = slots[i].position;
            p.z = members[i].transform.position.z;
            members[i].transform.position = p;
        }

        // Tail followers — preserve each one's authored position and
        // only apply the slot DELTA accumulated since Awake. This keeps
        // the visual position consistent across spine/sprite/whatever
        // and tolerates short slot arrays gracefully.
        if (tailFollowers == null) return;
        for (int i = 0; i < tailFollowers.Count; i++)
        {
            GameObject f = tailFollowers[i];
            if (f == null) continue;
            Vector3 p = ComputeFollowerTargetPos(i);
            p.z = f.transform.position.z;
            f.transform.position = p;
        }
    }

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

    private void CleanNulls()
    {
        if (members != null) members.RemoveAll(m => m == null);
    }

    // ============================================================
    //  SERVING
    // ============================================================

    /// <summary>True if there is at least one member left to serve.</summary>
    public bool HasAvailableMembers()
    {
        CleanNulls();
        return members != null && members.Count > 0;
    }

    /// <summary>
    /// Serves the head of the queue: plays the named state's visual / SFX /
    /// actions on members[0], removes it from the list, then tweens every
    /// remaining member up one slot. If <paramref name="stateId"/> is empty,
    /// uses the first non-initStateId state on <see cref="data"/>.
    /// </summary>
    public void ServeHead(string stateId = null)
    {
        if (!HasAvailableMembers()) return;
        ObjectState s = ResolveServedState(stateId);
        if (s == null) return;
        B_InteractableObject.LockInput();
        StartCoroutine(ServeRoutine(s));
    }

    private ObjectState ResolveServedState(string stateId)
    {
        if (data == null || data.states == null || data.states.Count == 0) return null;
        if (!string.IsNullOrEmpty(stateId))
        {
            for (int i = 0; i < data.states.Count; i++)
                if (data.states[i] != null && data.states[i].stateId == stateId)
                    return data.states[i];
            return null;
        }
        // Default: first state whose id differs from initStateId, else states[0].
        for (int i = 0; i < data.states.Count; i++)
        {
            ObjectState candidate = data.states[i];
            if (candidate == null) continue;
            if (data.initStateId != null && candidate.stateId == data.initStateId) continue;
            return candidate;
        }
        return data.states[0];
    }

    private IEnumerator ServeRoutine(ObjectState s)
    {
        // Detach the head FIRST. If an action in the state destroys it
        // (Disappear with destroyOnDisappear), Unity's destroyed-object
        // equality would make a later members.Remove(head) silently no-op
        // and leave a null entry at index 0 — which in turn makes ShiftUp
        // skip slot 0 and nobody visibly moves up. By popping up front we
        // keep the remaining members at indices 0..n-1 so ShiftUp does
        // exactly one tween per surviving member.
        GameObject head = members[0];
        members.RemoveAt(0);

        SpriteRenderer headSr = head != null ? head.GetComponent<SpriteRenderer>() : null;
        Spine.Unity.SkeletonAnimation headSpine = head != null
            ? head.GetComponentInChildren<Spine.Unity.SkeletonAnimation>() : null;

        try
        {
            // State visuals on the head.
            if (visualMode == VisualMode.Spine && headSpine != null
                && !string.IsNullOrEmpty(s.stateSpineAnim))
                B_InteractableObject.PlaySpineAnim(headSpine, s.stateSpineAnim, s.stateSpineLoop);
            else if (headSr != null && s.stateSprite != null)
                headSr.sprite = s.stateSprite;

            B_InteractableObject.PlaySFXSafe(s.stateSFX);

            // Run actions on the head — ShiftUp waits for this to finish,
            // including any actions flagged runInParallel (RunActions drains
            // pending coroutines before returning).
            if (s.actions != null && s.actions.Count > 0)
                yield return RunActions(s.actions, head);

            if (!string.IsNullOrEmpty(s.successMessageKey))
                B_InteractableObject.OnShowMessage?.Invoke(s.successMessageKey);

            // Mark this state as done so other objects can check it via
            // StateRequirement.objectId = <queueId>, stateId = <this state>.
            // Queue state "done" semantics: "this state has fired at least once".
            s.isDone = true;

            // Shift everyone up one slot.
            yield return ShiftUp();

            // Empty-queue chain.
            if (members.Count == 0 && queueEmptyTarget != null
                && !string.IsNullOrEmpty(queueEmptyStateId))
                queueEmptyTarget.ForceActivateState(queueEmptyStateId);
        }
        finally
        {
            B_InteractableObject.UnlockInput();
        }
    }

    private IEnumerator ShiftUp()
    {
        if (slots == null || slots.Count == 0) yield break;

        List<Tween> tweens = new List<Tween>();
        int memberN = members != null ? Mathf.Min(members.Count, slots.Count) : 0;
        for (int i = 0; i < memberN; i++)
        {
            GameObject m = members[i];
            Transform dest = slots[i];
            if (m == null || dest == null) continue;

            Vector3 p = dest.position;
            p.z = m.transform.position.z;
            if (shiftDuration > 0f)
                tweens.Add(m.transform.DOMove(p, shiftDuration).SetEase(shiftEase));
            else
                m.transform.position = p;
        }

        // Tail followers — delta-based shift from their authored
        // initial position. Tolerates short slot arrays (clamps to last
        // valid slot, so they hold position until members thin enough).
        if (tailFollowers != null)
        {
            for (int i = 0; i < tailFollowers.Count; i++)
            {
                GameObject f = tailFollowers[i];
                if (f == null) continue;
                Vector3 p = ComputeFollowerTargetPos(i);
                p.z = f.transform.position.z;
                if (shiftDuration > 0f)
                    tweens.Add(f.transform.DOMove(p, shiftDuration).SetEase(shiftEase));
                else
                    f.transform.position = p;
            }
        }

        for (int i = 0; i < tweens.Count; i++)
            if (tweens[i] != null) yield return tweens[i].WaitForCompletion();
    }

    // ============================================================
    //  ACTION RUNNER (reuses most of Group's logic but head-scoped)
    // ============================================================

    private IEnumerator RunActions(List<StateAction> actions, GameObject head)
    {
        List<Coroutine> pending = new List<Coroutine>();
        for (int i = 0; i < actions.Count; i++)
        {
            StateAction a = actions[i];
            if (a == null) continue;

            Coroutine co = StartCoroutine(RunAction(a, head));
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

    private IEnumerator RunAction(StateAction a, GameObject head)
    {
        GameObject subject = a.actionTarget != null ? a.actionTarget : head;
        if (subject == null) subject = gameObject;

        switch (a.type)
        {
            case StateActionType.Wait:
                if (a.duration > 0f) yield return new WaitForSeconds(a.duration);
                break;

            case StateActionType.MoveTo:
                if (a.moveTarget != null)
                {
                    Transform t = subject.transform;
                    Vector3 dest = a.moveTarget.position;
                    dest.z = t.position.z;
                    float targetZ = a.moveTarget.eulerAngles.z;

                    if (a.duration > 0f)
                    {
                        Tween posT = t.DOMove(dest, a.duration).SetEase(a.ease);
                        Tween rotT = a.rotateToMatchTarget
                            ? t.DORotate(new Vector3(0f, 0f, targetZ), a.duration,
                                         RotateMode.Fast).SetEase(a.ease)
                            : null;
                        yield return posT.WaitForCompletion();
                        if (rotT != null) yield return rotT.WaitForCompletion();
                    }
                    else
                    {
                        t.position = dest;
                        if (a.rotateToMatchTarget)
                        {
                            Vector3 e = t.eulerAngles;
                            e.z = targetZ;
                            t.eulerAngles = e;
                        }
                    }
                }
                break;

            case StateActionType.Disappear:
            {
                SpriteRenderer sr = subject.GetComponent<SpriteRenderer>();
                Spine.Unity.SkeletonAnimation spine = subject.GetComponentInChildren<Spine.Unity.SkeletonAnimation>();

                if (a.fadeOut && a.duration > 0f && (sr != null || spine != null))
                    yield return FadeAlpha(sr, spine, 1f, 0f, a.duration);
                else
                    SetAlpha(sr, spine, 0f);

                Collider2D[] cols = subject.GetComponentsInChildren<Collider2D>(true);
                for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;
                if (a.destroyOnDisappear) Destroy(subject);
                break;
            }

            case StateActionType.Appear:
            {
                SpriteRenderer sr = subject.GetComponent<SpriteRenderer>();
                Spine.Unity.SkeletonAnimation spine = subject.GetComponentInChildren<Spine.Unity.SkeletonAnimation>();

                if (!subject.activeSelf) subject.SetActive(true);
                Collider2D[] cols = subject.GetComponentsInChildren<Collider2D>(true);
                for (int i = 0; i < cols.Length; i++) cols[i].enabled = true;

                if (a.fadeIn && a.duration > 0f && (sr != null || spine != null))
                {
                    SetAlpha(sr, spine, 0f);
                    yield return FadeAlpha(sr, spine, 0f, 1f, a.duration);
                }
                else
                {
                    SetAlpha(sr, spine, 1f);
                }
                break;
            }

            case StateActionType.DoAnimation:
            {
                Spine.Unity.SkeletonAnimation spine = subject.GetComponentInChildren<Spine.Unity.SkeletonAnimation>();
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
                if (a.queueTarget != null) a.queueTarget.ServeHead(a.queueServeStateId);
                break;

            case StateActionType.PlaySFX:
                B_InteractableObject.PlaySFXSafe(a.sfxClip);
                if (a.duration > 0f) yield return new WaitForSeconds(a.duration);
                break;

            case StateActionType.SkinChange:
                ApplySkinChange(a);
                break;

            case StateActionType.ScaleTo:
            {
                Transform t = subject.transform;
                Vector3 dest = new Vector3(a.scaleTarget, a.scaleTarget, t.localScale.z);
                if (a.duration > 0f)
                    yield return t.DOScale(dest, a.duration).SetEase(a.ease).WaitForCompletion();
                else
                    t.localScale = dest;
                break;
            }

            case StateActionType.AttachToBone:
            {
                if (a.boneSource != null && !string.IsNullOrEmpty(a.boneName))
                {
                    var skel = a.boneSource.GetComponentInChildren<Spine.Unity.SkeletonAnimation>();
                    if (skel != null && subject != null)
                        B_BoneAttachment.Attach(subject.transform, skel, a.boneName, a.keepBoneOffset);
                }
                break;
            }

            case StateActionType.DetachFromBone:
                if (subject != null) B_BoneAttachment.Detach(subject.transform);
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

    // ---- Alpha helpers (sprite + spine aware) ----

    private static void SetAlpha(SpriteRenderer sr, Spine.Unity.SkeletonAnimation spine, float a)
    {
        if (sr != null)
        {
            Color c = sr.color;
            sr.color = new Color(c.r, c.g, c.b, a);
        }
        if (spine != null && spine.Skeleton != null) spine.Skeleton.A = a;
    }

    private static IEnumerator FadeAlpha(SpriteRenderer sr, Spine.Unity.SkeletonAnimation spine,
                                         float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            SetAlpha(sr, spine, a);
            yield return null;
        }
        SetAlpha(sr, spine, to);
    }

    // ============================================================
    //  GIZMOS (slot visualization)
    // ============================================================

    private void OnDrawGizmosSelected()
    {
        if (slots == null || slots.Count == 0) return;
        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.9f);
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null) continue;
            Gizmos.DrawWireSphere(slots[i].position, 0.15f);
            if (i + 1 < slots.Count && slots[i + 1] != null)
                Gizmos.DrawLine(slots[i].position, slots[i + 1].position);
        }
    }
}
