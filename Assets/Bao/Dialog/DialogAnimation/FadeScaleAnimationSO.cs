using UnityEngine;
using System.Collections;
using DG.Tweening;

[CreateAssetMenu(menuName = "Bao/Dialog Animation/Fade Scale")]
public class FadeScaleAnimationSO : DialogAnimationSO
{
    public float durationIn = 0.5f;
    public float durationOut = 0.5f;

    public override IEnumerator PlayIn(B_BaseDialog dialog)
    {
        dialog.SetBackgroundAlpha(0);
        dialog.panel.transform.localScale = Vector3.zero;

        Sequence seq = DOTween.Sequence();
        seq.Join(dialog.background.DOFade(bgAlpha, durationIn));
        seq.Join(dialog.panel.transform.DOScale(1f, durationIn).SetEase(Ease.OutBack));

        yield return seq.WaitForCompletion();
    }

    public override IEnumerator PlayOut(B_BaseDialog dialog)
    {
        Sequence seq = DOTween.Sequence();
        seq.Join(dialog.background.DOFade(0f, durationOut));
        seq.Join(dialog.panel.transform.DOScale(0f, durationOut).SetEase(Ease.InBack));

        yield return seq.WaitForCompletion();
    }
}