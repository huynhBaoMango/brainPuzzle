using UnityEngine;
using UnityEngine.Events;

public class B_DataEvent<T> : MonoBehaviour
{
    public B_VariableSO<T> variableSO;

    [Header("Event sẽ nhận giá trị mới")]
    public UnityEvent<T> onValueChanged = new UnityEvent<T>();

    private void Start()
    {
        if (variableSO != null)
        {
            variableSO.OnValueChanged += HandleValueChanged;
            HandleValueChanged();
        }
    }

    private void OnDestroy()
    {
        if (variableSO != null)
        {
            variableSO.OnValueChanged -= HandleValueChanged;
        }
    }

    protected virtual void HandleValueChanged()
    {
        if (variableSO != null)
        {
            onValueChanged?.Invoke(variableSO.Value);
        }
    }

    public void Refresh()
    {
        HandleValueChanged();
    }
}