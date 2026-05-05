using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime helper that combines multiple Spine skins into one and applies
/// them to a SkeletonAnimation. Spine's <c>Initial Skin</c> field is single-
/// select, so when an artist authors three skins (e.g. hair + jacket + bag)
/// only one shows by default. Drop this component next to the
/// <see cref="Spine.Unity.SkeletonAnimation"/>, list the skins to start
/// with, and play — all selected skins render together.
///
/// <para>
/// Designers toggle individual skins on/off at runtime via
/// <see cref="StateActionType.SkinChange"/> actions, which call
/// <see cref="AddSkin"/> / <see cref="RemoveSkin"/> / <see cref="ToggleSkin"/>.
/// Each mutation rebuilds the combined skin and re-applies it so the next
/// frame reflects the change with the breathing / idle animation
/// uninterrupted.
/// </para>
/// </summary>
[RequireComponent(typeof(Spine.Unity.SkeletonAnimation))]
public class B_SpineSkinSet : MonoBehaviour
{
    [Tooltip("Skins enabled when the scene starts. Edit this list and Spine combines them at runtime. Each entry is a dropdown of skin names from the sibling SkeletonAnimation's data asset.")]
    [SpineSkin]
    [SerializeField] private List<string> initialSkins = new List<string>();

    private Spine.Unity.SkeletonAnimation sa;
    private HashSet<string> active;

    /// <summary>The set authored at edit time. Used by the level exporter.</summary>
    public IReadOnlyList<string> InitialSkins => initialSkins;

    /// <summary>The set currently combined and rendered.</summary>
    public IEnumerable<string> ActiveSkins => active;

    private void Awake()
    {
        sa = GetComponent<Spine.Unity.SkeletonAnimation>();
        active = new HashSet<string>(initialSkins ?? new List<string>());
        Apply();
    }

    public void AddSkin(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        if (active == null) active = new HashSet<string>();
        if (active.Add(name)) Apply();
    }

    public void RemoveSkin(string name)
    {
        if (string.IsNullOrEmpty(name) || active == null) return;
        if (active.Remove(name)) Apply();
    }

    public void ToggleSkin(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        if (active == null) active = new HashSet<string>();
        if (!active.Add(name)) active.Remove(name);
        Apply();
    }

    public bool HasSkin(string name) => active != null && active.Contains(name);

    private void Apply()
    {
        if (sa == null || sa.Skeleton == null) return;

        var data = sa.Skeleton.Data;
        var combined = new Spine.Skin("combined_runtime");
        if (active != null)
        {
            foreach (string n in active)
            {
                if (string.IsNullOrEmpty(n)) continue;
                Spine.Skin s = data.FindSkin(n);
                if (s != null) combined.AddSkin(s);
            }
        }

        sa.Skeleton.SetSkin(combined);
        sa.Skeleton.SetSlotsToSetupPose();
        sa.AnimationState.Apply(sa.Skeleton);
    }
}
