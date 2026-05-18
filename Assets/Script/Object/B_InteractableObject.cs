using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Runtime for any puzzle object. Translates pointer gestures (tap, swipe,
/// drag) into state activations defined by its <see cref="ObjectData"/>.
/// State activations run as coroutines so structured Actions can play out
/// before player input is re-enabled.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class B_InteractableObject : MonoBehaviour
{
    // ============================================================
    //  INSPECTOR
    // ============================================================

    [Header("Identity")]
    [Tooltip("Unique id for this object. Other objects' state requirements reference it (e.g. \"princess\", \"dress\").")]
    [SerializeField] private string objectId;

    [Header("Data")]
    [Tooltip("All states this object can enter and the gestures that activate them.")]
    [SerializeField] private ObjectData data;

    [Header("References")]
    [Tooltip("Which renderer path to use. Sprite = swap stateSprite on SpriteRenderer. Spine = play stateSpineAnim on SkeletonAnimation. Picked explicitly so an object that has BOTH assigned doesn't accidentally render both at once.")]
    [SerializeField] private VisualMode visualMode = VisualMode.Sprite;

    [Tooltip("The renderer that swaps sprites when a state activates. Auto-found from this GameObject if left empty. Only used when Visual Mode = Sprite.")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Tooltip("Optional Spine skeleton. Only used when Visual Mode = Spine. State activations play stateSpineAnim, spawn plays initSpineAnim.")]
    [SerializeField] private Spine.Unity.SkeletonAnimation skeleton;

    [Header("Visibility")]
    [Tooltip("If true, this object starts invisible and non-interactive (alpha 0, colliders disabled). Use an Appear action from another object to reveal it at runtime.")]
    [SerializeField] private bool startHidden;

    [Header("Gesture Tuning (world units / seconds)")]
    [Tooltip("Max world distance the pointer can move and still count as a TAP.")]
    [SerializeField] private float tapMaxDistance = 0.2f;

    [Tooltip("Max time between press and release for a TAP.")]
    [SerializeField] private float tapMaxDuration = 0.3f;

    [Tooltip("Min world distance the pointer must move to count as a SWIPE.")]
    [SerializeField] private float swipeMinDistance = 1.0f;

    [Tooltip("Max time for a SWIPE. Slower than this and the gesture is treated as a DRAG.")]
    [SerializeField] private float swipeMaxDuration = 0.4f;

    // ============================================================
    //  RUNTIME STATE
    // ============================================================

    private Vector3 pointerDownWorld;
    private float pointerDownTime;
    private bool draggingFollow;
    private Vector3 dragGrabOffset;
    private Vector3 dragStartPosition;
    private Sprite spriteBeforeDrag;

    // Lift-to-top during drag: save current sort order on press, jack the
    // renderer's sortingOrder to a value higher than any authored layer
    // so the dragged object draws above everything else. Restored on
    // release regardless of drag success / snap-back.
    private const int DragSortOrderLift = 30000;
    private int sortOrderBeforeDrag;
    private bool sortOrderLifted;

    // ============================================================
    //  GLOBAL ACTION LOCK
    // ============================================================

    // Incremented while any state's actions are running on any object.
    // While > 0, all interactables ignore new pointer input.
    private static int actionLockCount;

    /// <summary>
    /// External "freeze input" flag — set by systems that want to suspend
    /// new player input WITHOUT also marking the level as ended (which
    /// would suppress outcome evaluation). Used by B_LevelTimerRunner the
    /// moment its countdown hits 0: input is blocked immediately so no
    /// new action chains start, then the lose state is force-activated
    /// once any in-flight action settles. Reset on each B_LevelConfig.Awake.
    /// </summary>
    public static bool InputSuspended;

    /// <summary>
    /// True while at least one state somewhere is mid-activation, OR the
    /// level has ended (win/lose), OR input is externally suspended (e.g.
    /// waiting for a queued lose to fire). B_PuzzleInput skips pointer
    /// handling while this is true.
    /// </summary>
    public static bool ActionsRunning =>
        actionLockCount > 0 || B_LevelConfig.LevelEnded || InputSuspended;

    /// <summary>
    /// Read-only view of the raw action-lock counter. True only while a
    /// state's action chain is in flight — independent of LevelEnded and
    /// InputSuspended. Used by systems (e.g. B_LevelTimerRunner) that need
    /// to wait specifically for current actions to finish without their
    /// own input-block flag confusing the check.
    /// </summary>
    public static bool AnyActionChainRunning => actionLockCount > 0;

    /// <summary>
    /// Fired whenever a state wants to show a localized message (success or
    /// fail). Subscribe from your UI to display a toast/popup. The string
    /// is a localization key — look it up via
    /// <c>LocalizationSettings.StringDatabase.GetLocalizedString("table_data", key)</c>.
    /// </summary>
    public static System.Action<string> OnShowMessage;

    private static void ShowMessage(string localeKey)
    {
        if (string.IsNullOrEmpty(localeKey)) return;
        OnShowMessage?.Invoke(localeKey);
    }

    /// <summary>Increment the global action lock. Used by B_InteractableGroup to share the same lock.</summary>
    public static void LockInput() { actionLockCount++; }

    /// <summary>
    /// Decrement the global action lock. When it returns to zero — i.e. every
    /// in-flight state chain (interactable, group, OR queue) has settled —
    /// re-evaluate REQUIREMENT_MET states so newly-satisfied reactive states
    /// fire automatically. Previously this check was hard-coded inside
    /// <see cref="ActivateStateRoutine"/>, which meant groups and queues
    /// never triggered reactive states when they finished.
    /// </summary>

    /// <summary>Decrement the global action lock.</summary>
    public static void UnlockInput()
    {
        actionLockCount--;
        if (actionLockCount == 0)
        {
            CheckReactiveStates();
            // After every settled state activation, re-check the win/lose
            // conditions on the active level config so the outcome event
            // fires the moment the gating state flips done.
            B_LevelConfig.EvaluateOutcome();
        }
    }

    /// <summary>
    /// Plays an AudioClip safely. Prefers B_AudioManager (respects global
    /// SFX toggle + uses the pooled AudioSources) when present, otherwise
    /// falls back to AudioSource.PlayClipAtPoint so SFX still works when
    /// testing a puzzle scene directly without booting through the Bao
    /// loading scene.
    /// </summary>
    public static void PlaySFXSafe(AudioClip clip)
    {
        if (clip == null) return;
        if (B_AudioManager.Instance != null)
        {
            B_AudioManager.Instance.PlaySFX(clip);
            return;
        }
        Camera cam = Camera.main;
        Vector3 pos = cam != null ? cam.transform.position : Vector3.zero;
        AudioSource.PlayClipAtPoint(clip, pos);
    }

    // ============================================================
    //  STATIC REGISTRY (id -> instance)
    // ============================================================

    private static readonly Dictionary<string, B_InteractableObject> registry =
        new Dictionary<string, B_InteractableObject>();

    /// <summary>Looks up another interactable by its Object Id.</summary>
    public static B_InteractableObject Find(string objectId)
    {
        if (string.IsNullOrEmpty(objectId)) return null;
        registry.TryGetValue(objectId, out var inst);
        return inst;
    }

    public string ObjectId => objectId;

    /// <summary>Read-only access to the inline ObjectData. Used by the level exporter.</summary>
    public ObjectData Data => data;

    /// <summary>Whether this object starts hidden. Used by the level exporter.</summary>
    public bool StartHidden => startHidden;

    /// <summary>Optional Spine skeleton. Used by the level exporter.</summary>
    public Spine.Unity.SkeletonAnimation Skeleton => skeleton;

    /// <summary>Authored visual mode (Sprite or Spine). Used by the level exporter.</summary>
    public VisualMode VisualMode => visualMode;

    /// <summary>
    /// Returns the sorting order from the renderer that matches the
    /// authored <see cref="visualMode"/>. Mirrors
    /// <see cref="B_StaticObject.GetSortOrder"/> so PickAt can rank
    /// Spine-mode interactables correctly — without this, a Spine-mode
    /// interactable would be treated as <c>order = 0</c> because there's
    /// no SpriteRenderer to read.
    /// </summary>
    public int GetSortOrder()
    {
        // Spine mode with explicit skeleton ref — read the skeleton's MeshRenderer.
        if (visualMode == VisualMode.Spine && skeleton != null)
        {
            MeshRenderer mr = skeleton.GetComponent<MeshRenderer>();
            if (mr != null) return mr.sortingOrder;
        }

        // Sprite mode (or Spine mode without a wired skeleton ref) — try SpriteRenderer.
        SpriteRenderer sr = spriteRenderer != null ? spriteRenderer : GetComponent<SpriteRenderer>();
        if (sr != null) return sr.sortingOrder;

        // Fallback: any MeshRenderer in the hierarchy. Handles the
        // "Spine GameObject (X)" pattern — a GO originally spawned by
        // Spine-Unity that still carries a SkeletonAnimation+MeshRenderer
        // even though visualMode was switched to Sprite (or skeleton ref
        // was nulled). Without this fallback PickAt would treat the
        // interactable as order 0 and any sibling with order > 0 would
        // shadow it. Mirrors the exporter's ResolveSortOrder so the JSON
        // value and the runtime value agree.
        MeshRenderer fallbackMr = GetComponentInChildren<MeshRenderer>();
        return fallbackMr != null ? fallbackMr.sortingOrder : 0;
    }

    /// <summary>
    /// Enables the renderer that matches <see cref="visualMode"/> and
    /// disables the other. Called from OnValidate (edit time) and Awake
    /// (runtime) so the scene view and the game both show exactly one
    /// visual even if both are assigned.
    /// </summary>
    private void ApplyVisualMode()
    {
        bool useSprite = visualMode == VisualMode.Sprite;
        if (spriteRenderer != null) spriteRenderer.enabled = useSprite;
        if (skeleton != null)
        {
            MeshRenderer mr = skeleton.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = !useSprite;
        }
    }

    private void OnValidate()
    {
        // Auto-populate spriteRenderer from the GameObject so designers don't
        // have to drag it every time.
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        // Auto-fill ObjectData.initSprite from the SpriteRenderer when Sprite
        // mode is active and the field is empty. Gives the designer a sensible
        // default — they can still overwrite it explicitly.
        if (visualMode == VisualMode.Sprite && data != null
            && data.initSprite == null
            && spriteRenderer != null && spriteRenderer.sprite != null)
        {
            data.initSprite = spriteRenderer.sprite;
        }

        ApplyVisualMode();
    }

    /// <summary>True if at least one state on this object hasn't fired yet.</summary>
    public bool HasEligibleStates()
    {
        if (data == null || data.states == null) return false;
        for (int i = 0; i < data.states.Count; i++)
        {
            if (!data.states[i].isDone) return true;
        }
        return false;
    }

    /// <summary>
    /// True if a state with the given id has already been activated on this
    /// object. For the special init state (ObjectData.initStateId), returns
    /// true only while NO other state has fired yet — i.e. the object is
    /// still in its starting condition.
    /// </summary>
    public bool IsStateDone(string stateId)
    {
        if (data == null || string.IsNullOrEmpty(stateId)) return false;

        // Init-state check: "done" means "still untouched".
        if (!string.IsNullOrEmpty(data.initStateId) && stateId == data.initStateId)
        {
            if (data.states == null) return true; // no states at all → always init
            for (int i = 0; i < data.states.Count; i++)
            {
                if (data.states[i].isDone) return false; // something fired → no longer init
            }
            return true;
        }

        // Normal state check.
        if (data.states == null) return false;
        for (int i = 0; i < data.states.Count; i++)
        {
            if (data.states[i].stateId == stateId) return data.states[i].isDone;
        }
        return false;
    }

    // ============================================================
    //  UNITY LIFECYCLE
    // ============================================================

    private void Reset()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Awake()
    {
        if (!string.IsNullOrEmpty(objectId)) registry[objectId] = this;
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        ApplyInitState();
    }

    private void OnDestroy()
    {
        if (!string.IsNullOrEmpty(objectId)
            && registry.TryGetValue(objectId, out var existing)
            && existing == this)
        {
            registry.Remove(objectId);
        }
    }

    private void ApplyInitState()
    {
        if (data == null) return;

        ApplyVisualMode();

        if (visualMode == VisualMode.Spine)
        {
            if (skeleton != null && !string.IsNullOrEmpty(data.initSpineAnim))
                PlaySpineAnim(skeleton, data.initSpineAnim, data.initSpineLoop);
        }
        else
        {
            if (spriteRenderer != null && data.initSprite != null)
                spriteRenderer.sprite = data.initSprite;
        }

        PlaySFXSafe(data.initSFX);

        if (startHidden) HideImmediate();
    }

    /// <summary>
    /// Plays a named animation on a spine skeleton on track 0. Safe against
    /// missing animations (logs once and keeps the previous anim).
    /// </summary>
    public static void PlaySpineAnim(Spine.Unity.SkeletonAnimation anim, string name, bool loop)
    {
        if (anim == null || string.IsNullOrEmpty(name)) return;
        if (anim.Skeleton == null || anim.AnimationState == null) return;

        if (anim.Skeleton.Data.FindAnimation(name) == null)
        {
            Debug.LogWarning($"[Spine] Animation '{name}' not found on '{anim.name}'.");
            return;
        }
        anim.AnimationState.SetAnimation(0, name, loop);
    }

    /// <summary>Sets alpha to 0 and disables colliders. The object stays active and registered.</summary>
    private void HideImmediate()
    {
        SetAlpha(spriteRenderer, skeleton, 0f);
        Collider2D[] cols = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;
    }

    // ============================================================
    //  INPUT (driven by B_PuzzleInput)
    // ============================================================

    /// <summary>Called by the input dispatcher when this instance wins the press pick.</summary>
    public void HandlePress(Vector2 world)
    {
        if (data == null) return;

        pointerDownWorld = new Vector3(world.x, world.y, transform.position.z);
        pointerDownTime = Time.time;
        dragStartPosition = transform.position;
        spriteBeforeDrag = null;

        if (HasEligibleDragState())
        {
            draggingFollow = true;
            dragGrabOffset = transform.position - pointerDownWorld;

            // Lift the active renderer above everything else for the drag.
            LiftSortOrderForDrag();

            // Swap to the drag sprite if one is set on the first undone DRAG state.
            if (spriteRenderer != null && data.states != null)
            {
                for (int i = 0; i < data.states.Count; i++)
                {
                    ObjectState s = data.states[i];
                    if (s.isDone || s.trigger != InteractType.DRAG) continue;
                    if (s.dragSprite != null)
                    {
                        spriteBeforeDrag = spriteRenderer.sprite;
                        spriteRenderer.sprite = s.dragSprite;
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

    /// <summary>Called every frame the press is held, after HandlePress has claimed this instance.</summary>
    public void HandleDrag(Vector2 world)
    {
        if (!draggingFollow) return;

        Vector3 target = new Vector3(world.x, world.y, 0f) + dragGrabOffset;
        target.z = transform.position.z;
        transform.position = target;
    }

    /// <summary>Called once when the press is released. Classifies the gesture and activates a matching state.</summary>
    public void HandleRelease(Vector2 world)
    {
        if (data == null) return;

        var (type, zoneId) = ClassifyGesture(world);
        bool activated = TryActivateMatching(type, zoneId);

        bool dragSucceeded = (type == InteractType.DRAG && activated);
        if (draggingFollow && !dragSucceeded)
        {
            transform.position = dragStartPosition;

            // Revert to the sprite before drag on snap-back.
            if (spriteBeforeDrag != null && spriteRenderer != null)
                spriteRenderer.sprite = spriteBeforeDrag;
        }

        spriteBeforeDrag = null;
        draggingFollow = false;

        // Always restore the original sort order — whether the drag
        // succeeded (object will Disappear / MoveTo at its real layer)
        // or snapped back (already at start position, just needs its
        // original z-order).
        RestoreSortOrderAfterDrag();
    }

    private void LiftSortOrderForDrag()
    {
        if (sortOrderLifted) return;

        if (visualMode == VisualMode.Spine && skeleton != null)
        {
            MeshRenderer mr = skeleton.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                sortOrderBeforeDrag = mr.sortingOrder;
                mr.sortingOrder = DragSortOrderLift;
                sortOrderLifted = true;
                return;
            }
        }

        SpriteRenderer sr = spriteRenderer != null ? spriteRenderer : GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sortOrderBeforeDrag = sr.sortingOrder;
            sr.sortingOrder = DragSortOrderLift;
            sortOrderLifted = true;
        }
    }

    private void RestoreSortOrderAfterDrag()
    {
        if (!sortOrderLifted) return;
        sortOrderLifted = false;

        if (visualMode == VisualMode.Spine && skeleton != null)
        {
            MeshRenderer mr = skeleton.GetComponent<MeshRenderer>();
            if (mr != null) { mr.sortingOrder = sortOrderBeforeDrag; return; }
        }

        SpriteRenderer sr = spriteRenderer != null ? spriteRenderer : GetComponent<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = sortOrderBeforeDrag;
    }

    // ============================================================
    //  LAYER PICKER
    // ============================================================

    /// <summary>
    /// Result of <see cref="PickAt"/>: the topmost interactable and the
    /// topmost drop zone at a point. Either or both may be null.
    /// </summary>
    public struct LayerPick
    {
        public B_InteractableObject interactable;
        public B_InteractableGroup group;
        public B_DropZone dropZone;
    }

    /// <summary>
    /// Walks every collider at <paramref name="point"/> and returns the
    /// highest-sorted interactable and the highest-sorted drop zone. Both
    /// compare via sort order (interactables via SpriteRenderer.sortingOrder,
    /// zones via B_DropZone.SortOrder) — they share the same number line.
    ///
    /// Shadow rule: if the best interactable's sort order is equal to or
    /// greater than the best drop zone's sort order AND that interactable is
    /// NOT the zone's own parent (nested-zone exposure), the drop zone is
    /// cleared because a solid object is visually covering it.
    ///
    /// Pass <paramref name="ignoreSelf"/> = the object being dragged so it
    /// can't shadow itself and its own nested zones aren't picked.
    /// </summary>
    /// <summary>Convenience overload — pass a Transform to ignore (used by B_InteractableGroup).</summary>
    public static LayerPick PickAt(Vector2 point, Transform ignoreRoot)
    {
        return PickAtInternal(point, ignoreRoot);
    }

    public static LayerPick PickAt(Vector2 point, B_InteractableObject ignoreSelf)
    {
        return PickAtInternal(point, ignoreSelf != null ? ignoreSelf.transform : null);
    }

    private static LayerPick PickAtInternal(Vector2 point, Transform ignoreRoot)
    {
        Collider2D[] hits = Physics2D.OverlapPointAll(point, ~0);

        // Best interactable OR group candidate (they compete on the same line).
        B_InteractableObject bestObj = null;
        B_InteractableGroup bestGroup = null;
        int bestObjOrder = int.MinValue;
        int bestObjId = int.MinValue;

        // Best drop-zone candidate.
        B_DropZone bestZone = null;
        int bestZoneOrder = int.MinValue;

        // Highest sort order across ALL solid things at this point
        // (interactables + static objects with colliders). Anything with a
        // lower sort order is considered "behind" the blocker and gets
        // cancelled. The blocker's owning interactable (if any) is tracked
        // so the nested-zone-exposure rule still works.
        int bestBlockerOrder = int.MinValue;
        B_InteractableObject blockerOwner = null;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];

            // ---- Drop-zone candidate ----
            // A single collider can carry both B_DropZone and
            // B_InteractableObject, so no early continue here.
            B_DropZone zone = hit.GetComponent<B_DropZone>();
            if (zone != null)
            {
                bool skipZone = ignoreRoot != null
                    && zone.transform.IsChildOf(ignoreRoot);
                if (!skipZone && zone.SortOrder > bestZoneOrder)
                {
                    bestZoneOrder = zone.SortOrder;
                    bestZone = zone;
                }
            }

            // ---- Static-object blocker ----
            B_StaticObject staticObj = hit.GetComponent<B_StaticObject>();
            if (staticObj != null)
            {
                int sOrder = staticObj.GetSortOrder();
                if (sOrder > bestBlockerOrder)
                {
                    bestBlockerOrder = sOrder;
                    blockerOwner = null;
                }
                continue;
            }

            // ---- Group candidate ----
            B_InteractableGroup grp = hit.GetComponent<B_InteractableGroup>();
            if (grp != null && grp.HasAvailableMembers())
            {
                int gOrder = grp.GetSortOrder();
                int gId = grp.GetInstanceID();

                bool gWins =
                    gOrder > bestObjOrder
                    || (gOrder == bestObjOrder && gId > bestObjId);

                if (gWins)
                {
                    bestObj = null;   // group takes priority over solo interactable
                    bestGroup = grp;
                    bestObjOrder = gOrder;
                    bestObjId = gId;
                }

                if (gOrder > bestBlockerOrder)
                {
                    bestBlockerOrder = gOrder;
                    blockerOwner = null;
                }
                continue;
            }

            // ---- Interactable candidate ----
            B_InteractableObject obj = hit.GetComponentInParent<B_InteractableObject>();
            if (obj == null) continue;
            if (ignoreRoot != null && obj.transform == ignoreRoot) continue;

            // Read the sort order from the renderer that matches the
            // interactable's authored visual mode — Spine-mode objects have
            // no SpriteRenderer and would otherwise rank as 0.
            int order = obj.GetSortOrder();
            int id = obj.GetInstanceID();

            bool wins =
                order > bestObjOrder
                || (order == bestObjOrder && id > bestObjId);

            if (wins)
            {
                bestObj = obj;
                bestGroup = null; // solo interactable outranks any prior group
                bestObjOrder = order;
                bestObjId = id;
            }

            // Interactables are also blockers.
            if (order > bestBlockerOrder)
            {
                bestBlockerOrder = order;
                blockerOwner = obj;
            }
        }

        // ---- Shadow resolution ----
        // The highest-sorted blocker cancels everything below it:
        //   - drop zones with sortOrder <= blocker are hidden
        //   - interactables with sortOrder < blocker are hidden
        // Exception: a blocker never hides its own nested drop zones.

        // Shadow drop zones.
        if (bestZone != null && bestBlockerOrder >= bestZoneOrder)
        {
            B_InteractableObject zoneParent =
                bestZone.GetComponentInParent<B_InteractableObject>();
            bool blockerIsZoneParent =
                blockerOwner != null && blockerOwner == zoneParent;
            if (!blockerIsZoneParent)
                bestZone = null;
        }

        // Shadow interactables/groups: a static object (or a higher thing)
        // on top blocks taps on everything underneath.
        if (bestBlockerOrder > bestObjOrder)
        {
            bestObj = null;
            bestGroup = null;
        }

        return new LayerPick { interactable = bestObj, group = bestGroup, dropZone = bestZone };
    }

    // ============================================================
    //  GESTURE CLASSIFICATION
    // ============================================================

    private (InteractType type, string zoneId) ClassifyGesture(Vector2 releaseWorld)
    {
        Vector3 delta = new Vector3(
            releaseWorld.x - pointerDownWorld.x,
            releaseWorld.y - pointerDownWorld.y,
            0f);
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

        // Drag: find the topmost drop zone where this object's collider
        // currently sits. Use Collider2D.bounds.center, NOT transform
        // position — for Spine interactables (and any object whose
        // BoxCollider2D has a non-zero offset), the visual + collider
        // float relative to the GameObject, so transform.position lands
        // somewhere off the actual visual and PickAt never finds the
        // zone the player dragged onto. Fall back to releaseWorld if the
        // collider is missing.
        Collider2D myCol = GetComponent<Collider2D>();
        Vector2 dropPoint = myCol != null
            ? (Vector2)myCol.bounds.center
            : releaseWorld;
        LayerPick pick = PickAt(dropPoint, this);
        return (InteractType.DRAG, pick.dropZone != null ? pick.dropZone.ZoneId : null);
    }

    // ============================================================
    //  STATE MATCHING & ACTIVATION
    // ============================================================

    private bool HasEligibleDragState()
    {
        if (data == null || data.states == null) return false;
        for (int i = 0; i < data.states.Count; i++)
        {
            ObjectState s = data.states[i];
            if (s.isDone && !s.repeatable) continue;
            if (s.trigger != InteractType.DRAG) continue;
            // Don't check requirements here — let the player drag freely.
            // If requirements aren't met on release, TryActivateMatching
            // rejects and the object snaps back. Better UX than refusing to move.
            return true;
        }
        return false;
    }

    private bool TryActivateMatching(InteractType type, string zoneId)
    {
        if (data == null || data.states == null) return false;
        if (type == InteractType.NONE) return false;

        // Track the first state that matched gesture+zone but failed
        // requirements, so we can show its fail message as a hint.
        ObjectState almostMatched = null;

        for (int i = 0; i < data.states.Count; i++)
        {
            ObjectState s = data.states[i];

            if (s.isDone && !s.repeatable) continue;
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

            ActivateState(s);
            return true;
        }

        // Right gesture, right zone, but requirements not met — show hint.
        if (almostMatched != null)
            ShowMessage(almostMatched.failMessageKey);

        return false;
    }

    /// <summary>Public wrapper so B_HintManager (and other editor tools) can test requirements.</summary>
    public bool CheckRequirements(ObjectState target) => RequirementsMet(target);

    /// <summary>
    /// Static requirements check used by BOTH ObjectState.requirements
    /// (gating state activation) AND StateAction.chainGuards (gating
    /// conditional ActivateState chains). Empty / null list → true.
    /// Resolves objectIds through the interactable registry first, then
    /// the queue registry — matching the runtime behavior of
    /// <see cref="RequirementsMet"/>.
    /// </summary>
    public static bool AreRequirementsMet(List<StateRequirement> reqs)
    {
        if (reqs == null || reqs.Count == 0) return true;

        for (int i = 0; i < reqs.Count; i++)
        {
            StateRequirement req = reqs[i];
            if (string.IsNullOrEmpty(req.objectId)) continue;

            bool done;
            B_InteractableObject other = Find(req.objectId);
            if (other != null)
            {
                done = other.IsStateDone(req.stateId);
            }
            else
            {
                B_InteractableQueue queue = B_InteractableQueue.Find(req.objectId);
                if (queue == null) return false;
                done = queue.IsStateDone(req.stateId);
            }

            if (req.requireNotDone ? done : !done) return false;
        }
        return true;
    }

    private bool RequirementsMet(ObjectState target)
    {
        if (target.requirements == null || target.requirements.Count == 0) return true;

        for (int i = 0; i < target.requirements.Count; i++)
        {
            StateRequirement req = target.requirements[i];
            if (string.IsNullOrEmpty(req.objectId)) continue;

            bool done;
            B_InteractableObject other = Find(req.objectId);
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

            // requireNotDone=false → must be done. requireNotDone=true → must NOT be done.
            if (req.requireNotDone ? done : !done) return false;
        }
        return true;
    }

    /// <summary>Marks the state as the new visual + starts its action coroutine.</summary>
    private void ActivateState(ObjectState s)
    {
        // If we were deactivated by a prior Disappear action, re-enable
        // the GameObject so StartCoroutine has a live MonoBehaviour.
        // Without this, chained ActivateState → Appear silently fails
        // because coroutines cannot be started on inactive objects.
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        // Reserve the lock synchronously so concurrent inputs in the same
        // frame are blocked even before the coroutine's first yield.
        actionLockCount++;
        StartCoroutine(ActivateStateRoutine(s));
    }

    /// <summary>
    /// Public entry point for the ActivateState action: forces a state on
    /// this object to activate even though no player gesture matched.
    /// Skips the trigger / zone / requirement checks (the caller has
    /// already decided this should happen).
    /// </summary>
    public void ForceActivateState(string stateId)
    {
        if (data == null || data.states == null || string.IsNullOrEmpty(stateId)) return;
        for (int i = 0; i < data.states.Count; i++)
        {
            ObjectState s = data.states[i];
            if (s.isDone && !s.repeatable) continue;
            if (s.stateId != stateId) continue;
            ActivateState(s);
            return;
        }
    }

    private IEnumerator ActivateStateRoutine(ObjectState s)
    {
        try
        {
            if (visualMode == VisualMode.Spine)
            {
                if (skeleton != null && !string.IsNullOrEmpty(s.stateSpineAnim))
                    PlaySpineAnim(skeleton, s.stateSpineAnim, s.stateSpineLoop);
            }
            else
            {
                if (spriteRenderer != null && s.stateSprite != null)
                    spriteRenderer.sprite = s.stateSprite;
            }

            PlaySFXSafe(s.stateSFX);

            s.isDone = true;

            if (s.actions != null && s.actions.Count > 0)
                yield return RunActions(s.actions);

            ShowMessage(s.successMessageKey);

        }
        finally
        {
            actionLockCount--;

            // Only check reactive states when the entire action chain has
            // settled (lock == 0). This prevents mid-chain cascade where
            // unrelated NONE-trigger states fire during someone else's actions.
            if (actionLockCount == 0)
            {
                CheckReactiveStates();
                B_LevelConfig.EvaluateOutcome();
            }
        }
    }

    /// <summary>Call once at level start so REQUIREMENT_MET states with already-satisfied requirements fire (e.g. intro animations).</summary>
    public static void CheckReactiveStatesOnce() => CheckReactiveStates();

    /// <summary>
    /// Walks every registered interactable and force-activates the first
    /// REQUIREMENT_MET-trigger state whose requirements are now met. Repeats until
    /// no more reactive states fire (handles chains: A satisfies B which
    /// satisfies C).
    /// </summary>
    private static void CheckReactiveStates()
    {
        bool anyFired = true;
        while (anyFired)
        {
            anyFired = false;
            foreach (var kvp in registry)
            {
                B_InteractableObject obj = kvp.Value;
                if (obj == null || obj.data == null || obj.data.states == null) continue;

                for (int i = 0; i < obj.data.states.Count; i++)
                {
                    ObjectState s = obj.data.states[i];
                    if (s.isDone && !s.repeatable) continue;
                    if (s.trigger != InteractType.REQUIREMENT_MET) continue;
                    if (!obj.RequirementsMet(s)) continue;

                    obj.ActivateState(s);
                    anyFired = true;
                    break; // restart the outer loop to re-check everything
                }
                if (anyFired) break;
            }
        }
    }

    // ============================================================
    //  ACTION RUNNER
    // ============================================================

    private IEnumerator RunActions(List<StateAction> actions)
    {
        List<Coroutine> pendingParallel = new List<Coroutine>();

        for (int i = 0; i < actions.Count; i++)
        {
            StateAction a = actions[i];
            if (a == null) continue;

            Coroutine co = StartCoroutine(RunAction(a));

            if (a.runInParallel)
            {
                pendingParallel.Add(co);
            }
            else
            {
                yield return co;
                for (int p = 0; p < pendingParallel.Count; p++)
                    yield return pendingParallel[p];
                pendingParallel.Clear();
            }
        }

        for (int p = 0; p < pendingParallel.Count; p++)
            yield return pendingParallel[p];
    }

    private IEnumerator RunAction(StateAction a)
    {
        switch (a.type)
        {
            case StateActionType.Wait:
                if (a.duration > 0f) yield return new WaitForSeconds(a.duration);
                break;

            case StateActionType.MoveTo:
                yield return ActionMoveTo(a);
                break;

            case StateActionType.Disappear:
                yield return ActionDisappear(a);
                break;

            case StateActionType.Appear:
                yield return ActionAppear(a);
                break;

            case StateActionType.DoAnimation:
                yield return ActionDoAnimation(a);
                break;

            case StateActionType.ActivateState:
                if (a.activateTarget != null)
                {
                    // When chainGuards are set, skip the chain if ALL guards
                    // are satisfied — lets the action's ending pose persist
                    // instead of falling back to a downstream state.
                    bool skipChain = a.chainGuards != null && a.chainGuards.Count > 0
                        && AreRequirementsMet(a.chainGuards);
                    if (!skipChain)
                        a.activateTarget.ForceActivateState(a.activateStateId);
                }
                // The chained activation increments the lock itself, so the
                // outer ActivateStateRoutine stays held until it finishes.
                break;

            case StateActionType.AdvanceQueue:
                if (a.queueTarget != null)
                    a.queueTarget.ServeHead(a.queueServeStateId);
                break;

            case StateActionType.PlaySFX:
                PlaySFXSafe(a.sfxClip);
                if (a.duration > 0f) yield return new WaitForSeconds(a.duration);
                break;

            case StateActionType.SkinChange:
                ApplySkinChange(a);
                break;

            case StateActionType.ScaleTo:
                yield return ActionScaleTo(a);
                break;
        }
    }

    private IEnumerator ActionScaleTo(StateAction a)
    {
        Transform t = ActionTransform(a);
        Vector3 dest = new Vector3(a.scaleTarget, a.scaleTarget, t.localScale.z);
        if (a.duration <= 0f)
        {
            t.localScale = dest;
            yield break;
        }
        Tween tw = t.DOScale(dest, a.duration).SetEase(a.ease);
        yield return tw.WaitForCompletion();
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

    // ---- Target resolution ----
    // If actionTarget is set, the action operates on that GameObject.
    // Otherwise it operates on self.

    private Transform ActionTransform(StateAction a) =>
        a.actionTarget != null ? a.actionTarget.transform : transform;

    private SpriteRenderer ActionRenderer(StateAction a) =>
        a.actionTarget != null ? a.actionTarget.GetComponent<SpriteRenderer>() : spriteRenderer;

    private GameObject ActionGameObject(StateAction a) =>
        a.actionTarget != null ? a.actionTarget : gameObject;

    // ---- Actions ----

    private IEnumerator ActionMoveTo(StateAction a)
    {
        if (a.moveTarget == null) yield break;

        Transform t = ActionTransform(a);
        Vector3 destination = a.moveTarget.position;
        destination.z = t.position.z;

        // Optional simultaneous rotation tween to match moveTarget's Z.
        float targetZ = a.moveTarget.eulerAngles.z;

        if (a.duration <= 0f)
        {
            t.position = destination;
            if (a.rotateToMatchTarget)
            {
                Vector3 e = t.eulerAngles;
                e.z = targetZ;
                t.eulerAngles = e;
            }
            yield break;
        }

        Tween posTween = t.DOMove(destination, a.duration).SetEase(a.ease);
        Tween rotTween = null;
        if (a.rotateToMatchTarget)
            rotTween = t.DORotate(new Vector3(0f, 0f, targetZ), a.duration,
                                  RotateMode.Fast).SetEase(a.ease);

        yield return posTween.WaitForCompletion();
        if (rotTween != null) yield return rotTween.WaitForCompletion();
    }

    private IEnumerator ActionDisappear(StateAction a)
    {
        GameObject go = ActionGameObject(a);
        SpriteRenderer sr = ActionRenderer(a);
        Spine.Unity.SkeletonAnimation spine = go != null ? go.GetComponentInChildren<Spine.Unity.SkeletonAnimation>() : null;

        if (a.fadeOut && a.duration > 0f && (sr != null || spine != null))
        {
            yield return FadeAlpha(sr, spine, 1f, 0f, a.duration);
        }
        else
        {
            SetAlpha(sr, spine, 0f);
            if (a.duration > 0f) yield return new WaitForSeconds(a.duration);
        }

        Collider2D[] cols = go.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;

        if (a.destroyOnDisappear) Destroy(go);
    }

    private IEnumerator ActionAppear(StateAction a)
    {
        GameObject go = ActionGameObject(a);
        SpriteRenderer sr = ActionRenderer(a);
        Spine.Unity.SkeletonAnimation spine = go != null ? go.GetComponentInChildren<Spine.Unity.SkeletonAnimation>() : null;

        if (!go.activeSelf) go.SetActive(true);

        Collider2D[] cols = go.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < cols.Length; i++) cols[i].enabled = true;

        if (a.fadeIn && a.duration > 0f && (sr != null || spine != null))
        {
            SetAlpha(sr, spine, 0f);
            yield return FadeAlpha(sr, spine, 0f, 1f, a.duration);
        }
        else
        {
            SetAlpha(sr, spine, 1f);
            if (a.duration > 0f) yield return new WaitForSeconds(a.duration);
        }
    }

    // ---- Alpha helpers (sprite + spine aware) ----

    /// <summary>
    /// Sets alpha on whichever visual the target uses:
    /// SpriteRenderer.color.a for sprite objects, Spine Skeleton.A for
    /// spine objects. Both can be set if both exist (harmless).
    /// </summary>
    private static void SetAlpha(SpriteRenderer sr, Spine.Unity.SkeletonAnimation spine, float a)
    {
        if (sr != null)
        {
            Color c = sr.color;
            sr.color = new Color(c.r, c.g, c.b, a);
        }
        if (spine != null && spine.Skeleton != null)
            spine.Skeleton.A = a;
    }

    /// <summary>
    /// Tweens alpha from `from` to `to` over `duration` on whichever
    /// visual the target uses. Spine skeletons need their color re-applied
    /// each frame via their tint, so we use DOTween.To with a float setter.
    /// </summary>
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

    private IEnumerator ActionDoAnimation(StateAction a)
    {
        GameObject go = ActionGameObject(a);

        Spine.Unity.SkeletonAnimation spine = go.GetComponentInChildren<Spine.Unity.SkeletonAnimation>();
        if (spine != null && !string.IsNullOrEmpty(a.spineAnim))
            PlaySpineAnim(spine, a.spineAnim, a.spineLoop);

        if (a.duration > 0f) yield return new WaitForSeconds(a.duration);
    }

}
