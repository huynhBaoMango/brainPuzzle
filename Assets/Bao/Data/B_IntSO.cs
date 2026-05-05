using UnityEngine;
[CreateAssetMenu(menuName = "Bao/Data/IntSO")]
public class B_IntSO : B_VariableSO<int>
{
    public void IncreaseByOne()
    {
        Value += 1;
    }

    public void DecreaseByOne()
    {
        Value -= 1;
    }
}
