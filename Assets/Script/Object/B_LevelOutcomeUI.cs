using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Listens to <see cref="B_LevelConfig.OnLevelEnded"/> and shows a "Win" or
/// "Lose" message + a Replay button. Drop this on any GameObject that owns
/// the outcome UI (Canvas → Panel → this) and wire the references in the
/// inspector.
///
/// <para>
/// On Awake the panel is hidden. When the level ends:
/// <list type="bullet">
/// <item>Sets <see cref="outcomeText"/> to <see cref="winText"/> or
///     <see cref="loseText"/>.</item>
/// <item>Activates the panel root so text + button become visible.</item>
/// </list>
/// </para>
///
/// <para>
/// The Replay button reloads the active scene via
/// <see cref="SceneManager.LoadScene(int)"/>. That fully resets every
/// state's isDone flag, every interactable's transform, every queue's
/// member list — without requiring the player to press Play in the editor
/// again.
/// </para>
/// </summary>
public class B_LevelOutcomeUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Root GameObject of the outcome panel (text + button). Hidden on Awake, activated when the level ends.")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("Text component that displays the Win or Lose message.")]
    [SerializeField] private TMP_Text outcomeText;

    [Tooltip("Button the player clicks to instantly restart the current scene.")]
    [SerializeField] private Button replayButton;

    [Header("Messages")]
    [Tooltip("Text shown when the win condition fires.")]
    [SerializeField] private string winText = "Win";

    [Tooltip("Text shown when the lose condition fires.")]
    [SerializeField] private string loseText = "Lose";

    [Header("Visuals (optional)")]
    [Tooltip("Color applied to the outcome text when the level is won.")]
    [SerializeField] private Color winColor = new Color(0.2f, 0.9f, 0.4f);

    [Tooltip("Color applied to the outcome text when the level is lost.")]
    [SerializeField] private Color loseColor = new Color(0.95f, 0.35f, 0.35f);

    private void Awake()
    {
        if (replayButton != null)
        {
            replayButton.onClick.RemoveListener(Replay);
            replayButton.onClick.AddListener(Replay);
        }
        B_LevelConfig.OnLevelEnded -= HandleLevelEnded;
        B_LevelConfig.OnLevelEnded += HandleLevelEnded;
    }

    private void OnDestroy()
    {
        B_LevelConfig.OnLevelEnded -= HandleLevelEnded;
        if (replayButton != null) replayButton.onClick.RemoveListener(Replay);
    }

    private void HandleLevelEnded(bool isWin)
    {
        if (outcomeText != null)
        {
            outcomeText.text = isWin ? winText : loseText;
            outcomeText.color = isWin ? winColor : loseColor;
        }
        if (panelRoot != null) panelRoot.SetActive(true);
    }

    /// <summary>
    /// Reloads the active scene. Wired automatically to the replay button
    /// in <see cref="Awake"/>; can also be called from a Unity event.
    /// </summary>
    public void Replay()
    {
        Scene s = SceneManager.GetActiveScene();
        SceneManager.LoadScene(s.buildIndex);
    }
}
