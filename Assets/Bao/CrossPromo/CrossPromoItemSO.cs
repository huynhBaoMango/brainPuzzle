using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(menuName = "Bao/Promo/Promo Item")]
public class CrossPromoItemSO : ScriptableObject
{
    public string id;
    public Sprite icon;
    public Sprite banner;
    public string name;
}
