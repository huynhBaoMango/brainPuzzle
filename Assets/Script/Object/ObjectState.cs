using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// One independent state an interactable object can enter. States are not
/// chained: any state can fire as soon as its trigger gesture matches and
/// its requirements are satisfied. Once activated, <see cref="isDone"/>
/// flips true and the state will not re-fire.
/// </summary>
[System.Serializable]
public class ObjectState
{
    [Header("Identity")]
    [Tooltip("Unique id within this object. Other states (on any object) reference this string in their Requirements.")]
    public string stateId;

    //[Header("Visual & Audio")]
    [Tooltip("Sprite shown immediately when the player starts dragging (before release/activation). Reverts on snap-back. Only used for DRAG trigger states. Leave empty to keep the current sprite during drag.")]
    public Sprite dragSprite;

    [Tooltip("Sprite the renderer swaps to when this state activates (on successful release). Ignored if the owning object is Spine-based.")]
    public Sprite stateSprite;

    [Tooltip("Spine animation name played on state activation. Only used if the owning object has a SkeletonAnimation. Leave empty to skip.")]
    [SpineAnim]
    public string stateSpineAnim;

    [Tooltip("Whether the state's Spine animation should loop.")]
    public bool stateSpineLoop;

    [Tooltip("Audio clip played once when this state activates. Drag any AudioClip asset here.")]
    public AudioClip stateSFX;

    [Header("Activation")]
    [Tooltip("The gesture the player must perform on this object to activate this state.")]
    public InteractType trigger;

    [Tooltip("Only used when Trigger is DRAG. The drop zone the player must release the object over. Leave empty to accept any drop.")]
    public string requiredZoneId;

    [Tooltip("Other states (on this or any other object) that must already be done before this state can activate. All requirements are AND-ed.")]
    public List<StateRequirement> requirements;

    [Tooltip("Milestone counter. 0 (default) = ALL Requirements must be met (normal AND logic). If > 0, this state fires when AT LEAST this many of the Requirements above are met — use for 'after N foods eaten' style milestones. Add several REQUIREMENT_MET states sharing the same Requirements list but with increasing Required Count for progressive milestones.")]
    public int requiredCount;

    [Header("Actions")]
    [Tooltip("Structured side-effects that run in order when this state activates. Player input is locked until they finish.")]
    public List<StateAction> actions;

    [Header("Messages")]
    [Tooltip("Localization key shown when this state activates successfully. Looked up from the localization table. Leave empty for no message.")]
    public string successMessageKey;

    [Tooltip("Localization key shown when the player does the right gesture but requirements aren't met. Leave empty for no message.")]
    public string failMessageKey;

    [Tooltip("Localization key shown by the Hint system when this state is the next solvable one (requirements met, not done). Leave empty to skip this state in hint rotation.")]
    public string hintMessageKey;

    [Header("Runtime")]
    [Tooltip("If true, this state can fire again on every matching gesture even after it's been activated. isDone is still set on first activation so requirements work, but the state is NOT skipped on future inputs.")]
    public bool repeatable;

    [Tooltip("Set to true automatically when this state activates. Other states reference it via Requirements. You normally leave this false in the inspector.")]
    public bool isDone;
}

/// <summary>
/// A reference to another state by (object id, state id). Used in
/// <see cref="ObjectState.requirements"/>. By default, the referenced
/// state must be DONE for this requirement to pass. Set
/// <see cref="requireNotDone"/> to invert — i.e. the referenced state
/// must NOT be done.
/// </summary>
[System.Serializable]
public struct StateRequirement
{
    [Tooltip("The Object Id of the interactable that owns the required state.")]
    public string objectId;

    [Tooltip("The State Id on that interactable.")]
    public string stateId;

    [Tooltip("If true, the requirement passes only when the referenced state is NOT done (inverted check). Default false = requires the state to be done.")]
    public bool requireNotDone;
}
