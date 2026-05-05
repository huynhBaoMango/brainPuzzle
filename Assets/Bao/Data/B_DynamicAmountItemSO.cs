using UnityEngine;

[CreateAssetMenu(menuName = "Bao/Data/DynamicItemSO")]
public class B_DynamicAmounItemSO : ScriptableObject
{
    public string id;
    public Sprite icon;
    public B_IntSO amount;
}
