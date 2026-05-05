using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class B_DataTextButton : MonoBehaviour
{
    [Header("Setting")]
    [SerializeField] private int amountOnClick;

    [Header("Data & UI Ref")]
    [SerializeField] private B_IntSO valueRef;
    [SerializeField] private TextMeshProUGUI text;
    private Button button;


    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void Start()
    {
        if (valueRef == null) return;
        button.onClick.AddListener(DecreaseValue);
        button.onClick.AddListener(UpdateAvailable);
        valueRef.OnValueChanged += UpdateText;
        UpdateAvailable();
        UpdateText();
    }

    private void OnDestroy()
    {
        valueRef.OnValueChanged -= UpdateText;
        button.onClick.RemoveListener(DecreaseValue);
        button.onClick.RemoveListener(UpdateAvailable);
    }

    private void DecreaseValue()
    {
        valueRef.Value += amountOnClick;
    }

    private void UpdateAvailable()
    {
        button.interactable = valueRef.Value > 0 ? true : false;
    }

    private void UpdateText()
    {
        text.text = valueRef.Value.ToString();
    }
}
