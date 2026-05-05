using UnityEngine;

/// <summary>
/// Marks a <c>string</c> field (or <c>List&lt;string&gt;</c> per-element)
/// as holding a Spine skin name. The editor renders it as a dropdown of
/// skins available on the owning object's SkeletonAnimation. Runtime
/// behaviour is unchanged — the string is still a plain skin name.
/// </summary>
public class SpineSkinAttribute : PropertyAttribute
{
}
