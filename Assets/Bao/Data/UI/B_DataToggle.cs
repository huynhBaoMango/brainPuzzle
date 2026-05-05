using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public class B_DataToggle : MonoBehaviour
{
    [SerializeField] private B_BoolSO boolRef;

    private Toggle toggle;

    private void Awake()
    {
        toggle = GetComponent<Toggle>();
    }

    private void Start()
    {
        if (boolRef == null) return;
        toggle.SetIsOnWithoutNotify(boolRef.Value);
        toggle.onValueChanged.AddListener(OnToggleChanged);
        boolRef.OnValueChanged += OnSOChanged;
    }

    private void OnDestroy()
    {
        toggle.onValueChanged.RemoveListener(OnToggleChanged);

        if (boolRef != null)
            boolRef.OnValueChanged -= OnSOChanged;
    }

    private void OnToggleChanged(bool value)
    {
        if (boolRef == null) return;
        boolRef.Value = value;
    }

    private void OnSOChanged()
    {
        toggle.SetIsOnWithoutNotify(boolRef.Value);
    }
}
