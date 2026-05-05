using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization.Settings;

[RequireComponent(typeof(Button))]
public class B_CooldownButton : MonoBehaviour
{
    [Header("Data")]
    public B_StringSO lastClickTimeSO;

    [Tooltip("Cooldown (seconds)")]
    public float cooldownDuration = 60f;

    [Header("UI")]
    [SerializeField] private TMP_Text countdownText;

    [Header("Locale Id Ready Text")]
    public string readyTextLocaleId;

    private Button button;
    private Coroutine countdownRoutine;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
    }

    private void OnEnable()
    {
        UpdateState();

        // start loop update mỗi 1s
        if (countdownRoutine != null)
            StopCoroutine(countdownRoutine);

        countdownRoutine = StartCoroutine(UpdateRoutine());
    }

    private void OnDisable()
    {
        if (countdownRoutine != null)
            StopCoroutine(countdownRoutine);
    }

    private void OnClick()
    {
        SaveCurrentTime();
        UpdateState();
    }

    #region Loop

    private IEnumerator UpdateRoutine()
    {
        while (true)
        {
            UpdateState();
            yield return new WaitForSeconds(1f);
        }
    }

    #endregion

    #region Core Logic

    private void UpdateState()
    {
        if (!TryGetLastTime(out DateTime lastTime))
        {
            SetReadyState();
            return;
        }

        double elapsed = (DateTime.UtcNow - lastTime).TotalSeconds;

        if (elapsed >= cooldownDuration)
        {
            SetReadyState();
        }
        else
        {
            double remaining = cooldownDuration - elapsed;
            SetCooldownState(remaining);
        }
    }

    private void SetReadyState()
    {
        button.interactable = true;

        if (countdownText != null)
            countdownText.text = LocalizationSettings.StringDatabase.GetLocalizedString(ConstValue.LOCALIZE_TABLE, readyTextLocaleId);
    }

    private void SetCooldownState(double remaining)
    {
        button.interactable = false;

        if (countdownText != null)
            countdownText.text = FormatTime(remaining);
    }

    #endregion

    #region Time

    private void SaveCurrentTime()
    {
        lastClickTimeSO.Value = DateTime.UtcNow.ToString("o");
    }

    private bool TryGetLastTime(out DateTime time)
    {
        string raw = lastClickTimeSO.Value;

        if (string.IsNullOrEmpty(raw))
        {
            time = default;
            return false;
        }

        return DateTime.TryParse(
            raw,
            null,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out time
        );
    }

    #endregion

    #region Utils

    private string FormatTime(double seconds)
    {
        TimeSpan t = TimeSpan.FromSeconds(seconds);

        if (t.TotalHours >= 1)
            return $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";

        return $"{t.Minutes:D2}:{t.Seconds:D2}";
    }

    #endregion
}
