/// <summary>
/// Explicit visual mode for interactables, statics, and groups. Decides
/// whether the runtime uses the SpriteRenderer path or the Spine
/// SkeletonAnimation path — even if BOTH are assigned on the same
/// GameObject. Exporter writes this mode to JSON so the LibGDX runtime
/// can make the same choice.
/// </summary>
public enum VisualMode
{
    Sprite,
    Spine,
}
