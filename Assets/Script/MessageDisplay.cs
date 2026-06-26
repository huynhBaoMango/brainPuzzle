using System.Collections;
using UnityEngine;

/// <summary>
/// Subscribes to B_InteractableObject.OnShowMessage and displays the
/// translated text. Each message shows immediately (alpha = 1), holds for
/// <see cref="holdSeconds"/>, then fades to alpha 0 over
/// <see cref="fadeSeconds"/>. A new message arriving mid-display interrupts
/// the running hold/fade and starts a fresh cycle.
///
/// Looks up the key in the active B_LevelConfig.strings table — no Unity
/// Localization package dependency.
/// </summary>
public class MessageDisplay : MonoBehaviour
{
    [SerializeField] private TMPro.TextMeshProUGUI messageText;

    [Tooltip("How long the message stays fully visible before it starts to fade.")]
    [SerializeField] private float holdSeconds = 2f;

    [Tooltip("Duration of the fade-out tween after the hold ends.")]
    [SerializeField] private float fadeSeconds = 0.5f;

    private Coroutine routine;

    private void OnEnable()
    {
        B_InteractableObject.OnShowMessage += ShowMessage;
    }

    private void OnDisable()
    {
        B_InteractableObject.OnShowMessage -= ShowMessage;
    }

    private void ShowMessage(string localeKey)
    {
        if (messageText == null) return;

        messageText.text = B_LevelConfig.Translate(localeKey);

        // Snap to fully visible and restart the timer — even if a previous
        // message is mid-fade or mid-hold.
        Color c = messageText.color;
        c.a = 1f;
        messageText.color = c;

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(HoldThenFade());
    }

    private IEnumerator HoldThenFade()
    {
        if (holdSeconds > 0f) yield return new WaitForSeconds(holdSeconds);

        if (fadeSeconds <= 0f)
        {
            SetAlpha(0f);
            routine = null;
            yield break;
        }

        float t = 0f;
        Color start = messageText.color;
        while (t < fadeSeconds)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(1f, 0f, t / fadeSeconds);
            Color c = start;
            c.a = a;
            messageText.color = c;
            yield return null;
        }
        SetAlpha(0f);
        routine = null;
    }

    private void SetAlpha(float a)
    {
        if (messageText == null) return;
        Color c = messageText.color;
        c.a = a;
        messageText.color = c;
    }
}
