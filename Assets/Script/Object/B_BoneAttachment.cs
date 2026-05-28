using UnityEngine;
using Spine.Unity;

/// <summary>
/// Runtime-only helper that attaches a Transform (e.g. a bucket) to a Spine
/// bone of another skeleton (e.g. a man's hand) so it follows the bone as the
/// animation plays. Backed by Spine-Unity's <see cref="BoneFollower"/>:
///
///  - An empty "anchor" GameObject is created as a DIRECT CHILD of the
///    skeleton (the recommended BoneFollower setup, which then tracks the bone
///    via localPosition each LateUpdate).
///  - The subject is re-parented under that anchor. With keepOffset = true the
///    subject keeps its current world position at attach time (so its offset
///    from the bone is preserved); with keepOffset = false the subject's pivot
///    snaps onto the bone.
///
/// This component lives on the SUBJECT and only records what's needed to undo
/// the attachment (the anchor it created + the subject's original parent), so
/// <see cref="Detach"/> can restore the subject and clean up the anchor.
///
/// It is created purely at runtime by the AttachToBone / DetachFromBone state
/// actions and is never serialized into the level JSON. The LibGDX runtime
/// mirrors this with its own bone-follow (Bone.getWorldX/Y + worldRotationX).
/// </summary>
[DisallowMultipleComponent]
public class B_BoneAttachment : MonoBehaviour
{
    private GameObject anchor;
    private Transform originalParent;

    /// <summary>
    /// Attaches <paramref name="subject"/> to the named bone of
    /// <paramref name="skel"/>. If the subject is already attached, the old
    /// attachment is released first.
    /// </summary>
    public static void Attach(Transform subject, SkeletonAnimation skel,
                              string boneName, bool keepOffset)
    {
        if (subject == null || skel == null || string.IsNullOrEmpty(boneName)) return;

        // Reuse an existing record if the subject is already attached (so we
        // don't fall foul of [DisallowMultipleComponent] vs. Unity's deferred
        // Destroy). Keep the TRUE original parent across re-attachments.
        B_BoneAttachment rec = subject.GetComponent<B_BoneAttachment>();
        Transform original;
        if (rec != null)
        {
            original = rec.originalParent;             // preserve first-seen parent
            if (rec.anchor != null) Destroy(rec.anchor);
        }
        else
        {
            original = subject.parent;
            rec = subject.gameObject.AddComponent<B_BoneAttachment>();
            rec.originalParent = original;
        }

        // Anchor as a direct child of the skeleton → BoneFollower uses the
        // fast localPosition path and stays glued to the bone.
        GameObject anchorGo = new GameObject($"_boneAnchor_{boneName}");
        anchorGo.transform.SetParent(skel.transform, false);

        BoneFollower bf = anchorGo.AddComponent<BoneFollower>();
        bf.skeletonRenderer = skel;
        bf.boneName = boneName;
        bf.followXYPosition = true;
        bf.followZPosition = false;
        bf.followBoneRotation = true;
        bf.followSkeletonFlip = true;
        bf.followLocalScale = false;
        bf.Initialize();
        // Snap the anchor onto the bone NOW so the offset we capture below is
        // measured against the bone's real position, not the anchor's origin.
        bf.LateUpdate();

        // keepOffset → worldPositionStays = true: subject holds its current
        // world pose, and its local pose under the anchor encodes the offset.
        subject.SetParent(anchorGo.transform, keepOffset);
        if (!keepOffset)
        {
            subject.localPosition = Vector3.zero;
            subject.localRotation = Quaternion.identity;
        }

        rec.anchor = anchorGo;
    }

    /// <summary>
    /// Detaches <paramref name="subject"/> if it's currently bone-attached,
    /// restoring it to its original parent at its current on-screen position
    /// and destroying the anchor. No-op if not attached.
    /// </summary>
    public static void Detach(Transform subject)
    {
        if (subject == null) return;
        B_BoneAttachment rec = subject.GetComponent<B_BoneAttachment>();
        if (rec == null) return;

        // Restore to the original parent keeping the current world pose so the
        // subject doesn't jump when released.
        subject.SetParent(rec.originalParent, true);
        if (rec.anchor != null) Destroy(rec.anchor);
        Destroy(rec);
    }
}
