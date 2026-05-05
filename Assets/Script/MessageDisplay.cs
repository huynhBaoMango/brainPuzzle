using UnityEngine;

/// <summary>
/// Subscribes to B_InteractableObject.OnShowMessage and displays the
/// translated text. Looks up the key in the active B_LevelConfig.strings
/// table — no Unity Localization package dependency.
/// </summary>
public class MessageDisplay : MonoBehaviour
{
    [SerializeField] private TMPro.TextMeshProUGUI messageText;

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
    }
}
