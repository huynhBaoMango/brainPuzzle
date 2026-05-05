using UnityEngine;
using UnityEngine.Purchasing;

[CreateAssetMenu(menuName = "Bao/IAP/IAP Product")]
public class IAPProductSO : ScriptableObject
{
    public string productId;
    public ProductType productType;

    [Header("Display Info")]
    public string displayName;
    public string description;
    public Sprite icon;

    [Header("Reward")]
    public int amount = 0;
}