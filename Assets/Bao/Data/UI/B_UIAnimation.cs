using DG.Tweening;
using UnityEngine;

public static class B_UIAnimation
{
    public static Sequence BounceLoop(GameObject go)
    {
        Sequence bounceSequence = DOTween.Sequence();
        bounceSequence.Append(go.transform.DOScale(0.9f, 1f).SetEase(Ease.InQuad))
                      .Append(go.transform.DOScale(1f, 1f).SetEase(Ease.OutQuad))
                      .SetLoops(-1);

        return bounceSequence;
    }
}
