using DG.Tweening;
using UnityEngine;

/// <summary>
/// Catalogue of side-effects an ObjectState can run when it activates.
/// New action kinds: add an enum value, add the runtime branch in
/// B_InteractableObject.RunAction, and add the field group in
/// StateActionDrawer.
/// </summary>
public enum StateActionType
{
    Wait,
    MoveTo,
    Disappear,
    Appear,
    DoAnimation,
    ActivateState,
    AdvanceQueue,
    PlaySFX,
    SkinChange,
    ScaleTo,
    AttachToBone,
    DetachFromBone,
    EnqueueMember,
}

/// <summary>
/// Operation applied by <see cref="StateActionType.SkinChange"/> on a
/// <see cref="B_SpineSkinSet"/>. Add inserts the skin into the active set
/// (no-op if already present), Remove takes it out, Toggle flips it.
/// </summary>
public enum SkinOp
{
    Add,
    Remove,
    Toggle,
}

/// <summary>
/// One scripted side-effect attached to an ObjectState. The runtime walks
/// the list in order and waits for each action to finish before starting
/// the next, unless <see cref="runInParallel"/> is set.
/// </summary>
[System.Serializable]
public class StateAction
{
    [Tooltip("Which kind of action this entry performs. The fields below change to match.")]
    public StateActionType type;

    [Tooltip("If true, the next action starts immediately without waiting for this one to finish.")]
    public bool runInParallel;

    [Tooltip("Optional. If set, this action operates on the target GameObject instead of the object that owns the state. Use this to Appear/Disappear/Move static objects, doors, overlays, etc.")]
    public GameObject actionTarget;

    [Tooltip("Generic duration in seconds. Used by Wait, MoveTo, Disappear, and DoAnimation.")]
    public float duration = 0.4f;

    // ---- MoveTo ----

    [Tooltip("Transform to move this object to. Used by MoveTo.")]
    public Transform moveTarget;

    [Tooltip("Easing curve for tweens.")]
    public Ease ease = Ease.OutQuad;

    [Tooltip("If true, also tween the subject's Z rotation to match moveTarget.eulerAngles.z over the same Duration with the same Ease. Useful when the destination implies a new facing — e.g. character tilts as it slides into place.")]
    public bool rotateToMatchTarget;

    // ---- ScaleTo ----

    [Tooltip("Target uniform scale for ScaleTo. 1 = original size, 0.5 = half, 2 = double. Duration and Ease are reused from the MoveTo group above.")]
    public float scaleTarget = 1f;

    // ---- Disappear ----

    [Tooltip("If true, the sprite fades out over Duration. Otherwise the object hides instantly.")]
    public bool fadeOut = true;

    [Tooltip("If true, the GameObject is destroyed once hidden. If false, it is just deactivated.")]
    public bool destroyOnDisappear = true;

    // ---- Appear ----

    [Tooltip("If true, the sprite fades in over Duration. Otherwise the object show instantly.")]
    public bool fadeIn = true;

    // ---- DoAnimation (Spine) ----

    [Tooltip("Spine animation name to play on the target's SkeletonAnimation.")]
    [SpineAnim]
    public string spineAnim;

    [Tooltip("Whether the Spine animation should loop.")]
    public bool spineLoop;

    // ---- ActivateState ----

    [Tooltip("The interactable whose state should be force-activated.")]
    public B_InteractableObject activateTarget;

    [Tooltip("State Id on the target interactable to activate.")]
    public string activateStateId;

    [Tooltip("Optional fallback guards. When set, the chain is SKIPPED if ALL listed requirements are satisfied. Use this to implement \"fallback to stance unless everything is done\" — list the terminal-state conditions here, and the chain stops firing once they're all met.")]
    public System.Collections.Generic.List<StateRequirement> chainGuards;

    // ---- AdvanceQueue ----

    [Tooltip("Queue whose head should be served. Runs the authored 'served' state on the head, then shifts the rest up one slot.")]
    public B_InteractableQueue queueTarget;

    [Tooltip("Optional state id on the queue's ObjectData to run when serving. Leave empty for the default (first non-init state).")]
    public string queueServeStateId;

    // ---- PlaySFX ----

    [Tooltip("Audio clip to play when this action runs. Routed through B_AudioManager if present, else falls back to AudioSource.PlayClipAtPoint.")]
    public AudioClip sfxClip;

    // ---- SkinChange ----

    [Tooltip("Interactable that owns the B_SpineSkinSet whose skins should change. The runtime calls GetComponent<B_SpineSkinSet>() on this object.")]
    public B_InteractableObject skinTarget;

    [Tooltip("Skin name on the target's SkeletonDataAsset to add / remove / toggle.")]
    public string skinName;

    [Tooltip("Operation to apply to the named skin: Add inserts, Remove takes out, Toggle flips current state.")]
    public SkinOp skinOp = SkinOp.Toggle;

    // ---- AttachToBone / DetachFromBone ----

    [Tooltip("AttachToBone: the spine object whose bone the subject follows (e.g. the man). The SUBJECT that gets attached is 'Target Object' if set, otherwise the object owning this state (e.g. the dragged bucket).")]
    public GameObject boneSource;

    [Tooltip("AttachToBone: bone name on the Bone Source's skeleton to follow (e.g. hand_R). Pick it from the dropdown once Bone Source is assigned.")]
    public string boneName;

    [Tooltip("AttachToBone: if true, keep the subject's current offset from the bone at attach time (it stays where it was dropped and rides the bone). If false, the subject's pivot snaps exactly onto the bone.")]
    public bool keepBoneOffset = true;
}
