using DG.Tweening;
using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class B_LoseDialog : B_BaseDialog
{
    [Header("UI")]
    public List<Image> stars;
    public GameObject starContainer;
    public GameObject infoHolder;
    public GameObject buttonHolder;

    [Header("Sound")]
    public List<B_SFXEventSO> starSounds;
    public B_SFXEventSO failedSound;

    public override void ShowThis()
    {
        foreach (Image star in stars)
        {
            star.DOFade(0, 0);
        }
        if (!infoHolder.GetComponent<CanvasGroup>())
        {
            infoHolder.AddComponent<CanvasGroup>();
        }
        infoHolder.GetComponent<CanvasGroup>().DOFade(0, 0);

        infoHolder?.SetActive(false);
        buttonHolder?.SetActive(false);

        base.ShowThis();
        PlayAnimation();
    }

    [Button]
    public void PlayAnimation()
    {
        StartCoroutine(starAnimationCoroutine());
    }

    IEnumerator starAnimationCoroutine()
    {
        yield return new WaitForSeconds(0.5f);

        for (int i = 0; i < 3; i++)
        {
            Sequence seq = DOTween.Sequence();
            RectTransform rect = stars[i].GetComponent<RectTransform>();
            Vector2 target = rect.anchoredPosition;
            rect.anchoredPosition = target + Vector2.up * 800f;
            stars[i].DOFade(1, 0);

            seq.Append(rect.DOAnchorPos(target, 0.2f).SetEase(Ease.OutBack))
                .Append(starContainer.transform.DOPunchPosition(new(0, -40f, 0), 0.3f));

            if(starSounds.Count > 1) B_AudioManager.Instance.PlaySFX(starSounds[i]);
            else B_AudioManager.Instance.PlaySFX(starSounds[0]);

            yield return seq.WaitForCompletion();
        }

        if (infoHolder != null)
        {
            infoHolder.SetActive(true);
            if(infoHolder.TryGetComponent<CanvasGroup>(out CanvasGroup group))
            {
                group.DOFade(1, 0.5f);
            }
        }
        if (buttonHolder != null)
        {
            buttonHolder.SetActive(true);
            RectTransform rect = buttonHolder.GetComponent<RectTransform>();
            RectTransform parent = rect.parent as RectTransform;

            Vector2 target = rect.anchoredPosition;

            float offset = parent.rect.height / 2;

            rect.anchoredPosition = target + Vector2.down * offset;

            rect.DOAnchorPos(target, 0.5f)
                .SetEase(Ease.OutBack);
        }

        B_AudioManager.Instance.PlaySFX(failedSound);
    }
}
