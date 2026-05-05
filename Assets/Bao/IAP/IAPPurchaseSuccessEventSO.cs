using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(menuName = "Bao/IAP/IAP Purchase Success")]
public class IAPPurchaseSuccessEventSO : ScriptableObject
{
    public UnityAction<IAPProductSO> OnPurchaseSuccess;
    public void Raise(IAPProductSO product) => OnPurchaseSuccess?.Invoke(product);
}