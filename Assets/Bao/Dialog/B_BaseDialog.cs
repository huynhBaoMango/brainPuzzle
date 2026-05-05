using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using DG.Tweening;
using DG;
using NaughtyAttributes;

public class B_BaseDialog : MonoBehaviour
{
    public Image background;
    public GameObject panel;

    [SerializeField] private DialogAnimationSO animation;

    [Button]
    public virtual void ShowThis()
    {
        gameObject.SetActive(true);

        if (animation != null)
        {
            StartCoroutine(animation.PlayIn(this));
        }
        else
        {
            StartCoroutine(DefaultFadeIn());
        }
    }

    [Button]
    public virtual void CloseThis()
    {
        if (animation != null)
        {
            StartCoroutine(CloseRoutine());
        }
        else
        {
            StartCoroutine(DefaultFadeOut());
        }
    }

    private IEnumerator CloseRoutine()
    {
        yield return animation.PlayOut(this);
        gameObject.SetActive(false);
    }

    private IEnumerator DefaultFadeIn()
    {
        SetImageAlpha(background, 0f);
        if (panel.GetComponent<Image>()) SetImageAlpha(panel.GetComponent<Image>(), 0f);

        Sequence seq = DOTween.Sequence();

        seq.Join(background.DOFade(1f, 0.4f).SetEase(Ease.OutQuad));

        if (panel != null)
        {
            seq.Join(background.DOFade(ConstValue.BG_ALPHA_DEFAULT, 0.4f).SetEase(Ease.OutQuad));
            if (panel.GetComponent<Image>()) seq.Join(panel.GetComponent<Image>().DOFade(ConstValue.BG_ALPHA_DEFAULT, 0.4f).SetEase(Ease.OutQuad));
        }

        yield return seq.WaitForCompletion();
    }

    private IEnumerator DefaultFadeOut()
    {
        Sequence seq = DOTween.Sequence();

        seq.Join(background.DOFade(0f, 0.3f).SetEase(Ease.InQuad));

        if (panel != null)
        {
            seq.Join(background.DOFade(0f, 0.4f).SetEase(Ease.OutQuad));
            if (panel.GetComponent<Image>()) seq.Join(panel.GetComponent<Image>().DOFade(0f, 0.4f).SetEase(Ease.OutQuad));
        }

        yield return seq.WaitForCompletion();

        gameObject.SetActive(false);
    }

    public void SetBackgroundAlpha(float alpha)
    {
        SetImageAlpha(background, alpha);
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        if (image == null) return;
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }
}