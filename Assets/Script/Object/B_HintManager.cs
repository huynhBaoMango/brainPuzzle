using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fires the hint for every win condition when the player taps the Hint
/// button. Each win condition's target state can have a
/// <c>hintMessageKey</c>; all non-empty ones are emitted via
/// <see cref="B_InteractableObject.OnShowMessage"/> in order.
/// </summary>
public static class B_HintManager
{
    /// <summary>
    /// Walks <see cref="B_LevelConfig.winConditions"/> and fires the hint
    /// key of each target state. Falls back to
    /// <see cref="B_LevelConfig.defaultHintMessageKey"/> if no win
    /// condition has a hint.
    /// </summary>
    public static void RequestHint()
    {
        B_LevelConfig config = B_LevelConfig.Current;
        if (config == null) return;

        int emitted = 0;
        List<LevelCondition> conds = config.winConditions;
        if (conds != null)
        {
            for (int i = 0; i < conds.Count; i++)
            {
                LevelCondition c = conds[i];
                if (c == null) continue;

                string id = c.GetEffectiveTargetId();
                if (string.IsNullOrEmpty(id)) continue;

                ObjectData data = ResolveData(id);
                ObjectState state = FindState(data, c.stateId);
                if (state == null) continue;
                if (string.IsNullOrEmpty(state.hintMessageKey)) continue;

                B_InteractableObject.OnShowMessage?.Invoke(state.hintMessageKey);
                emitted++;
            }
        }

        // Fallback only if no win-condition hint was emitted.
        if (emitted == 0 && !string.IsNullOrEmpty(config.defaultHintMessageKey))
            B_InteractableObject.OnShowMessage?.Invoke(config.defaultHintMessageKey);
    }

    /// <summary>
    /// Resolves a condition targetId to its ObjectData — checks
    /// B_InteractableObject registry first, then falls back to queues.
    /// </summary>
    private static ObjectData ResolveData(string id)
    {
        B_InteractableObject obj = B_InteractableObject.Find(id);
        if (obj != null) return obj.Data;

        B_InteractableQueue queue = B_InteractableQueue.Find(id);
        if (queue != null) return queue.Data;

        return null;
    }

    private static ObjectState FindState(ObjectData data, string stateId)
    {
        if (data == null || data.states == null) return null;
        for (int i = 0; i < data.states.Count; i++)
        {
            if (data.states[i].stateId == stateId) return data.states[i];
        }
        return null;
    }
}
