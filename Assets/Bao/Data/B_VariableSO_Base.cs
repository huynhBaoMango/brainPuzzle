using UnityEngine;
using System;

public abstract class B_VariableSO_Base : ScriptableObject, IStringRepresentable
{
    [Header("[Save To Json For Long Use]")]
    public bool SaveThis = true;

    [Header("[Config]")]
    public string key;
    public event Action OnValueChanged;


    protected void RaiseValueChanged()
    {
        OnValueChanged?.Invoke();
    }
    public abstract string GetStringValue();
}