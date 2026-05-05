using DG.Tweening;
using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Bao/Dialog Animation/Slide")]
public class SlideAnimationSO : DialogAnimationSO
{
    public float durationIn = 0.5f;
    public float durationOut = 0.5f;
    public SlideDirection direction;

    public override IEnumerator PlayIn(B_BaseDialog dialog)
    {
        dialog.SetBackgroundAlpha(0);

        RectTransform panel = dialog.panel.GetComponent<RectTransform>();
        RectTransform canvas = dialog.GetComponentInParent<Canvas>().GetComponent<RectTransform>();

        Vector2 startPos = GetOffScreenPosition(direction, panel, canvas);
        panel.anchoredPosition = startPos;

        Sequence seq = DOTween.Sequence();
        seq.Join(dialog.background.DOFade(bgAlpha, durationIn));
        seq.Join(panel.DOAnchorPos(Vector2.zero, durationIn).SetEase(Ease.OutCubic));

        yield return seq.WaitForCompletion();
    }

    public override IEnumerator PlayOut(B_BaseDialog dialog)
    {
        RectTransform panel = dialog.panel.GetComponent<RectTransform>();
        RectTransform canvas = dialog.GetComponentInParent<Canvas>().GetComponent<RectTransform>();

        Vector2 endPos = GetOffScreenPosition(direction, panel, canvas);

        Sequence seq = DOTween.Sequence();
        seq.Join(dialog.background.DOFade(0f, durationOut));
        seq.Join(panel.DOAnchorPos(endPos, durationOut).SetEase(Ease.InCubic));

        yield return seq.WaitForCompletion();
    }


    private Vector2 GetOffScreenPosition(SlideDirection dir, RectTransform panel, RectTransform canvas)
    {
        Vector2 canvasSize = canvas.rect.size;
        Vector2 panelSize = panel.rect.size;

        switch (dir)
        {
            case SlideDirection.Left:
                return new Vector2(-(canvasSize.x / 2 + panelSize.x), 0);

            case SlideDirection.Right:
                return new Vector2((canvasSize.x / 2 + panelSize.x), 0);

            case SlideDirection.Top:
                return new Vector2(0, (canvasSize.y / 2 + panelSize.y));

            case SlideDirection.Bottom:
                return new Vector2(0, -(canvasSize.y / 2 + panelSize.y));
        }

        return Vector2.zero;
    }
}

public enum SlideDirection
{
    Left,
    Right,
    Top,
    Bottom
}

