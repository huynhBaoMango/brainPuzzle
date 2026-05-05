using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using DG.Tweening;
using System.Data.Common;

public class B_LoadingPanel : MonoBehaviour
{

    [SerializeField] private Image bg;
    [SerializeField] private GameObject logo;

    public IEnumerator PlayIn()
    {
        Color c = bg.color;
        c.a = 0;
        bg.color = c;

        logo.transform.localScale = Vector3.zero;

        Sequence seq = DOTween.Sequence();
        seq.Join(bg.DOFade(1f, 0.5f));
        seq.Join(logo.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack));

        yield return seq.WaitForCompletion();
    }

    public IEnumerator PlayOut()
    {
        Debug.Log("PLAY OUT");
        Sequence seq = DOTween.Sequence();
        seq.Join(bg.DOFade(0f, 0.5f));
        seq.Join(logo.transform.DOScale(0f, 0.5f).SetEase(Ease.InBack));

        yield return seq.WaitForCompletion();
    }
}
