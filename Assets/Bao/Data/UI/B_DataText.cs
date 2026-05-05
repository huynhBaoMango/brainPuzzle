using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

[RequireComponent(typeof(TextMeshProUGUI))]
public class B_DataText : MonoBehaviour
{
    [SerializeField] private B_VariableSO_Base textRef;

    public string prefix;
    public string suffix;
    public bool useLocalize = false;
    public bool useLocalizePreSuffix = false;

    private TMP_Text curText;

    private void Awake()
    {
        curText = GetComponent<TextMeshProUGUI>();
    }

    private void Start()
    {
        if (textRef == null) return;
        textRef.OnValueChanged += OnSOChanged;
        OnSOChanged();
    }

    private void OnDestroy()
    {
        if (textRef != null)
            textRef.OnValueChanged -= OnSOChanged;
    }

    private void OnSOChanged()
    {
        string prefixUpdated = "";
        string suffixUpdated = "";
        string textUpdated = "";
        if (!string.IsNullOrEmpty(prefix)) prefixUpdated = useLocalizePreSuffix ? LocalizationSettings.StringDatabase.GetLocalizedString(ConstValue.LOCALIZE_TABLE, prefix) + " " : prefix + " ";
        if(!string.IsNullOrEmpty(suffix)) suffixUpdated = useLocalizePreSuffix ? " " + LocalizationSettings.StringDatabase.GetLocalizedString(ConstValue.LOCALIZE_TABLE, suffix):" " + suffix;
        if (!string.IsNullOrEmpty(textRef.GetStringValue())) textUpdated = useLocalize ? LocalizationSettings.StringDatabase.GetLocalizedString(ConstValue.LOCALIZE_TABLE, textRef.GetStringValue()) : textRef.GetStringValue();
        curText.text = prefixUpdated + textUpdated + suffixUpdated;
    }
}
