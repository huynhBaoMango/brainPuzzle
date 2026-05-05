using Unity.VisualScripting;

public abstract class B_VariableSO<T> : B_VariableSO_Base
{
    public T defaultValue;
    protected T runtimeValue;
    private bool isLoaded = false;

    protected async void EnsureLoaded()
    {
        if (isLoaded) return;

        await B_VariableDatabase.Instance.WaitUntilInitialized();

        if(SaveThis) runtimeValue = B_VariableDatabase.Instance.LoadOrCreate(key, defaultValue);
        else runtimeValue = defaultValue;


        isLoaded = true;
    }

    public override string GetStringValue()
    {
        EnsureLoaded();
        return runtimeValue?.ToString() ?? "";
    }

    public virtual T Value
    {
        get
        {
            EnsureLoaded();
            return runtimeValue;
        }
        set
        {
            EnsureLoaded();
            runtimeValue = value;

            if (SaveThis)
                B_VariableDatabase.Instance.Save(key, runtimeValue);

            RaiseValueChanged();
        }
    }

    protected virtual void OnEnable()
    {
        isLoaded = false; 
    }
}