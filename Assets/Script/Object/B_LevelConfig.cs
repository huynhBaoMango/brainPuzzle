using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-level metadata that the LevelExporter writes into the top of the
/// JSON file. Place ONE of these on an empty GameObject in the scene
/// (e.g. "_LevelConfig"). Most fields are designer-facing data with no
/// runtime behaviour — the exception is <see cref="strings"/>, which
/// backs the runtime <see cref="GetString"/> lookup for UI messages.
/// </summary>
[DisallowMultipleComponent]
public class B_LevelConfig : MonoBehaviour
{
    // ============================================================
    //  RUNTIME STRING LOOKUP
    // ============================================================

    /// <summary>The active B_LevelConfig in the scene. Set in Awake.</summary>
    public static B_LevelConfig Current { get; private set; }

    /// <summary>
    /// Language code used by <see cref="GetString"/>. "en" or "vn".
    /// Change this when the player switches language. Defaults to "en".
    /// </summary>
    public static string CurrentLanguage = "en";

    /// <summary>
    /// Fired exactly once when a win/lose condition becomes satisfied.
    /// Argument: <c>true</c> for win, <c>false</c> for lose. Subscribe from
    /// any UI (e.g. B_LevelOutcomeUI) to react. Wired into the global state
    /// pipeline via <see cref="EvaluateOutcome"/>, called whenever the
    /// action lock returns to zero.
    /// </summary>
    public static System.Action<bool> OnLevelEnded;

    /// <summary>
    /// Set true once a win/lose condition has fired so the event isn't
    /// re-emitted on every subsequent state activation. Reset on Awake of
    /// a new B_LevelConfig (i.e. on scene reload).
    /// </summary>
    private static bool levelEnded;

    /// <summary>True once the level has ended (win or lose). Read-only.</summary>
    public static bool LevelEnded => levelEnded;

    private void Awake()
    {
        Current = this;
        levelEnded = false;
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    // ============================================================
    //  WIN / LOSE EVALUATION
    // ============================================================

    /// <summary>
    /// Walks <see cref="winConditions"/> and <see cref="loseConditions"/>
    /// and fires <see cref="OnLevelEnded"/>:
    /// <list type="bullet">
    /// <item><b>Win</b> = ALL win conditions met (AND). Standard puzzle
    ///     semantic — every required goal must be done before the level
    ///     is considered won. Empty list = never wins.</item>
    /// <item><b>Lose</b> = ANY lose condition met (OR). Lose triggers as
    ///     soon as a single failure event fires.</item>
    /// </list>
    /// Win is checked first. Idempotent: a second call after an outcome
    /// is locked is a no-op.
    /// </summary>
    public static void EvaluateOutcome()
    {
        if (levelEnded || Current == null) return;

        if (AllConditionsMet(Current.winConditions))
        {
            levelEnded = true;
            OnLevelEnded?.Invoke(true);
            return;
        }
        if (AnyConditionMet(Current.loseConditions))
        {
            levelEnded = true;
            OnLevelEnded?.Invoke(false);
        }
    }

    /// <summary>
    /// True only when EVERY condition in the list is satisfied AND the list
    /// is non-empty. Used for win conditions — multi-goal puzzles need
    /// every goal done before the level ends.
    /// </summary>
    private static bool AllConditionsMet(System.Collections.Generic.List<LevelCondition> conds)
    {
        if (conds == null || conds.Count == 0) return false;

        bool sawAny = false;
        for (int i = 0; i < conds.Count; i++)
        {
            LevelCondition c = conds[i];
            if (c == null) continue;
            string id = c.GetEffectiveTargetId();
            if (string.IsNullOrEmpty(id)) continue;

            if (!IsConditionMet(c, id)) return false;
            sawAny = true;
        }
        // If every entry was empty/invalid we end up here — treat as "no
        // valid conditions" rather than "all met".
        return sawAny;
    }

    /// <summary>
    /// True if at least one condition in the list is satisfied. Used for
    /// lose conditions — a single failure should end the level.
    /// </summary>
    private static bool AnyConditionMet(System.Collections.Generic.List<LevelCondition> conds)
    {
        if (conds == null) return false;
        for (int i = 0; i < conds.Count; i++)
        {
            LevelCondition c = conds[i];
            if (c == null) continue;
            string id = c.GetEffectiveTargetId();
            if (string.IsNullOrEmpty(id)) continue;

            if (IsConditionMet(c, id)) return true;
        }
        return false;
    }

    /// <summary>
    /// Resolves a single condition's targetId (interactable first, then
    /// queue) and applies the StateActivated / StateNotActivated semantic.
    /// </summary>
    private static bool IsConditionMet(LevelCondition c, string id)
    {
        bool done;
        B_InteractableObject obj = B_InteractableObject.Find(id);
        if (obj != null)
        {
            done = obj.IsStateDone(c.stateId);
        }
        else
        {
            B_InteractableQueue q = B_InteractableQueue.Find(id);
            if (q == null) return false;
            done = q.IsStateDone(c.stateId);
        }

        return c.type == LevelConditionType.StateActivated ? done : !done;
    }

    /// <summary>
    /// Looks up a string key in this level's <see cref="strings"/> table.
    /// Falls back: current language → English → the key itself.
    /// </summary>
    public string GetString(string key)
    {
        if (string.IsNullOrEmpty(key) || strings == null) return key;
        for (int i = 0; i < strings.Count; i++)
        {
            LevelString ls = strings[i];
            if (ls == null || ls.key != key) continue;

            if (CurrentLanguage == "vn" && !string.IsNullOrEmpty(ls.vn)) return ls.vn;
            if (!string.IsNullOrEmpty(ls.en)) return ls.en;
            return key;
        }
        return key;
    }

    /// <summary>
    /// Shortcut for <c>Current?.GetString(key)</c>. Returns the key itself
    /// if no level config is active.
    /// </summary>
    public static string Translate(string key)
    {
        if (Current == null) return key;
        return Current.GetString(key);
    }

    [Header("Identity")]
    [Tooltip("Stable id for this level. Also used as the export folder name (e.g. \"lv1\" → lv1/level.json). Keep it short and filesystem-safe.")]
    public string levelId = "lv1";

    [Tooltip("String KEY for the level's display name. The actual localized text lives in the Level Strings table below. Use GetTitle() / B_LevelConfig.Current.GetString(title) at runtime to resolve it.")]
    public string title;

    /// <summary>
    /// Returns the localized level title by looking <see cref="title"/> up
    /// in the strings table. Falls back to the key itself if unset.
    /// </summary>
    public string GetTitle() => GetString(title);

    [Tooltip("String KEY for the level's description. The actual localized text lives in the Level Strings table below. Use GetDescription() / B_LevelConfig.Current.GetString(description) at runtime to resolve it.")]
    public string description;

    /// <summary>
    /// Returns the localized level description by looking <see cref="description"/> up
    /// in the strings table. Falls back to the key itself if unset.
    /// </summary>
    public string GetDescription() => GetString(description);

    [Header("Virtual Viewport")]
    [Tooltip("Virtual width exported into the JSON. The LibGDX viewport scales to fit this.")]
    public int virtualWidth = 1080;

    [Tooltip("Virtual height exported into the JSON.")]
    public int virtualHeight = 1920;

    [Tooltip("Camera that frames the level. The exporter uses its position and orthographic size to convert world coordinates into virtual viewport pixels, so the JSON always lines up with the LibGDX viewport. Falls back to Camera.main if empty.")]
    public Camera levelCamera;

    [Tooltip("Fallback only — used if Level Camera is not set. Unity world units to virtual pixels.")]
    public float pixelsPerUnit = 100f;

    [Header("Outcome Conditions")]
    [Tooltip("Level wins when ANY of these conditions become true.")]
    public List<LevelCondition> winConditions;

    [Tooltip("Level loses when ANY of these conditions become true.")]
    public List<LevelCondition> loseConditions;

    [Header("Strings")]
    [Tooltip("Fallback hint string key shown when no state has a matching hint (or the level is effectively finished). Leave empty for no fallback.")]
    public string defaultHintMessageKey;

    [Tooltip("Localized strings for this level. Each entry maps a key to translations. Exported as a separate strings.json alongside the level file.")]
    public List<LevelString> strings;
}

public enum LevelConditionType
{
    StateActivated,
    StateNotActivated,
}

[System.Serializable]
public class LevelCondition
{
    [Tooltip("Kind of check to perform.")]
    public LevelConditionType type = LevelConditionType.StateActivated;

    [Tooltip("Object id of the interactable OR queue id of the queue whose state is being checked. Accepts both so a win condition can watch a queue (e.g. \"beggar_line\" + \"served\").")]
    public string targetId;

    [Tooltip("State id on that interactable / queue.")]
    public string stateId;

    // Legacy field — old scenes serialized a direct B_InteractableObject
    // reference here. The drawer auto-migrates it into targetId on first
    // inspection so existing win/lose picks are preserved. Kept hidden so
    // designers don't see two redundant fields.
    [HideInInspector]
    public B_InteractableObject target;

    /// <summary>
    /// Resolves the effective id to check: prefers <see cref="targetId"/>,
    /// falls back to the legacy <see cref="target"/> reference's ObjectId.
    /// </summary>
    public string GetEffectiveTargetId()
    {
        if (!string.IsNullOrEmpty(targetId)) return targetId;
        if (target != null) return target.ObjectId;
        return null;
    }
}

/// <summary>
/// One localized string entry for a level. Matches the LibGDX
/// strings.json format: <c>{ "key": "...", "en": "...", "vn": "..." }</c>.
/// </summary>
[System.Serializable]
public class LevelString
{
    [Tooltip("String key referenced by successMessageKey / failMessageKey in states.")]
    public string key;

    [Tooltip("English translation.")]
    public string en;

    [Tooltip("Vietnamese translation.")]
    public string vn;
}
