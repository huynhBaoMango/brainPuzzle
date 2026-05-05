using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure data describing an interactable object: its starting visual and the
/// independent states it can enter via player gestures. Designed to be edited
/// inline on a B_InteractableObject and serialized cleanly by a level editor.
/// </summary>
[System.Serializable]
public class ObjectData
{
    [Header("Initial Look")]
    [Tooltip("Optional id for the init state. Other objects can require this to check 'object is still in its starting state (no states have fired yet)'.")]
    public string initStateId = "init";

    [Tooltip("Sprite displayed before any state has been activated. Ignored if the owning object is Spine-based.")]
    public Sprite initSprite;

    [Tooltip("Spine animation name played on spawn. Only used if the owning object has a SkeletonAnimation. Leave empty to skip.")]
    [SpineAnim]
    public string initSpineAnim;

    [Tooltip("Whether the init Spine animation should loop.")]
    public bool initSpineLoop = true;

    [Tooltip("Audio clip played once when the object spawns. Drag any AudioClip asset here.")]
    public AudioClip initSFX;

    [Header("States")]
    [Tooltip("All states this object can enter. Order matters: when a gesture happens, the first matching unfinished state wins.")]
    public List<ObjectState> states;
}
