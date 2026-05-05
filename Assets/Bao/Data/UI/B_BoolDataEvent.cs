using UnityEngine;

public class B_BoolDataEvent : B_DataEvent<bool>
{
    [Header("Bool Specific")]
    [SerializeField] private bool reverseValue = false;

    protected override void HandleValueChanged()
    {
        if (variableSO == null) return;

        bool finalValue = reverseValue ? !variableSO.Value : variableSO.Value;

        onValueChanged?.Invoke(finalValue);

        Debug.Log($"[B_BoolDataEvent] {variableSO.name} changed to: {finalValue} (original: {variableSO.Value})");
    }
}