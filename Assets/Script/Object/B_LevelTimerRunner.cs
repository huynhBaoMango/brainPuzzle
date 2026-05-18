using UnityEngine;

/// <summary>
/// Editor / Play-mode test harness for the timer that LibGDX runs at
/// shipping. Reads <see cref="B_LevelConfig.timeLimit"/> /
/// <see cref="B_LevelConfig.timeUpTarget"/> / <see cref="B_LevelConfig.timeUpStateId"/>
/// and force-activates the configured state when the countdown hits 0,
/// exactly the way the LibGDX runtime will.
///
/// <para>
/// Drop on any GameObject in the scene to test in Unity Play mode. The
/// component is purely a test helper — the exporter doesn't include it
/// in level JSON, so it has no effect in the LibGDX build.
/// </para>
///
/// <para>
/// Auto-pauses once <see cref="B_LevelConfig.OnLevelEnded"/> fires (so it
/// won't keep ticking after the player already won / lost via some other
/// condition).
/// </para>
/// </summary>
public class B_LevelTimerRunner : MonoBehaviour
{
    [Tooltip("If true, the countdown starts on Awake. Disable for manual control via StartTimer().")]
    [SerializeField] private bool autoStart = true;

    [Tooltip("Optional TMP_Text that shows the remaining time as MM:SS. Leave empty if you don't want a display.")]
    [SerializeField] private TMPro.TMP_Text displayText;

    /// <summary>Seconds remaining. Read-only.</summary>
    public float TimeRemaining { get; private set; }

    /// <summary>True while the timer is ticking.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>True once the time-up state has fired this run. Reset by ResetTimer().</summary>
    public bool HasFired { get; private set; }

    private B_LevelConfig cfg;

    private void Awake()
    {
        cfg = B_LevelConfig.Current;
        if (cfg == null)
        {
            // B_LevelConfig.Awake might not have run yet — try again on Start.
            cfg = null;
        }
        TimeRemaining = 0f;
        RefreshDisplay();

        B_LevelConfig.OnLevelEnded -= HandleLevelEnded;
        B_LevelConfig.OnLevelEnded += HandleLevelEnded;
    }

    private void Start()
    {
        if (cfg == null) cfg = B_LevelConfig.Current;
        TimeRemaining = cfg != null ? cfg.timeLimit : 0f;
        RefreshDisplay();
        if (autoStart && TimeRemaining > 0f) StartTimer();
    }

    private void OnDestroy()
    {
        B_LevelConfig.OnLevelEnded -= HandleLevelEnded;
    }

    private void HandleLevelEnded(bool isWin) => StopTimer();

    public void StartTimer() => IsRunning = true;
    public void StopTimer() => IsRunning = false;

    public void ResetTimer()
    {
        IsRunning = false;
        HasFired = false;
        TimeRemaining = cfg != null ? cfg.timeLimit : 0f;
        RefreshDisplay();
    }

    public void AddTime(float seconds)
    {
        TimeRemaining = Mathf.Max(0f, TimeRemaining + seconds);
        RefreshDisplay();
    }

    private void Update()
    {
        if (!IsRunning || HasFired) return;
        if (B_LevelConfig.LevelEnded) return;

        TimeRemaining -= Time.deltaTime;
        if (TimeRemaining <= 0f)
        {
            TimeRemaining = 0f;
            FireTimeUp();
        }
        RefreshDisplay();
    }

    private void FireTimeUp()
    {
        if (HasFired) return;
        HasFired = true;
        IsRunning = false;

        if (cfg == null)
        {
            Debug.LogWarning("[B_LevelTimerRunner] No B_LevelConfig in scene — nothing to fire.", this);
            return;
        }
        if (cfg.timeUpTarget == null || string.IsNullOrEmpty(cfg.timeUpStateId))
        {
            Debug.LogWarning(
                "[B_LevelTimerRunner] Time ran out but B_LevelConfig.timeUpTarget / " +
                "timeUpStateId aren't set. Configure them in Level Config → Timer.", this);
            return;
        }

        // Block new player input immediately so the queue can't grow while
        // we wait for the current chain to finish. Then wait until every
        // in-flight action has settled and fire the lose state. This avoids
        // the two bad cases: firing mid-chain (looks like an interrupt) or
        // letting a fresh player action start AFTER the timer expired.
        StartCoroutine(FireTimeUpAfterCurrentAction());
    }

    private System.Collections.IEnumerator FireTimeUpAfterCurrentAction()
    {
        // Block new player input the moment time hits 0 so no fresh action
        // chain can start during our wait. Existing in-flight actions keep
        // running and we let them finish.
        B_InteractableObject.InputSuspended = true;

        // Wait until every in-flight action chain has cleared.
        while (B_InteractableObject.AnyActionChainRunning)
            yield return null;

        // Clear to fire. ForceActivateState bumps the lock again for the
        // lose chain itself. Once that chain finishes, EvaluateOutcome
        // runs as usual and LevelEnded takes over input blocking.
        cfg.timeUpTarget.ForceActivateState(cfg.timeUpStateId);
    }

    private void RefreshDisplay()
    {
        if (displayText == null) return;
        int t = Mathf.CeilToInt(Mathf.Max(0f, TimeRemaining));
        int m = t / 60;
        int s = t % 60;
        displayText.text = $"{m:00}:{s:00}";
    }
}
