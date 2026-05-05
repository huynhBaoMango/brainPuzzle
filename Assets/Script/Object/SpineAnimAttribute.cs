using UnityEngine;

/// <summary>
/// Marks a <c>string</c> field as holding a Spine animation name. The
/// editor renders it as a dropdown of animations on the owning object's
/// SkeletonAnimation. Runtime behaviour is unchanged — the string is
/// still a plain animation name.
/// </summary>
public class SpineAnimAttribute : PropertyAttribute
{
}
