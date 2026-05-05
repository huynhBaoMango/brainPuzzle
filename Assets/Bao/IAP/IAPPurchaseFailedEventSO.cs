using UnityEngine.Events;
using UnityEngine;

[CreateAssetMenu(menuName = "Bao/IAP/IAP Purchase Failed")]
public class IAPPurchaseFailedEventSO : ScriptableObject
{
    public UnityAction<string, string> OnPurchaseFailed;

    public void Raise(string productId, string reason)
    {
        OnPurchaseFailed?.Invoke(productId, reason);
    }
}