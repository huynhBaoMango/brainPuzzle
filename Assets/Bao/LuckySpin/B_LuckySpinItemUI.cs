using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class B_LuckySpinItemUI : MonoBehaviour
{
    public Image icon;
    public TMP_Text amountText;

    public void Setup(B_ItemSO data)
    {
        icon.sprite = data.icon;
        amountText.text = "x" + data.amount.ToString();
    }
}
