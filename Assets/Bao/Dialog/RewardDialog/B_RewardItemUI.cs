using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class B_RewardItemUI : MonoBehaviour
{
    public Image icon;
    public GameObject glowImg;
    public TMP_Text amountText;

    public void Setup(Sprite img, int amount)
    {
        icon.sprite = img;
        icon.preserveAspect = true;
        amountText.text = "x" + amount.ToString();

        glowImg.transform.DOLocalRotate(new Vector3(0, 0, 180), 10).SetEase(Ease.Linear).SetLoops(-1);
    }

    private void OnDestroy()
    {
        glowImg.transform.DOKill();
    }
}
