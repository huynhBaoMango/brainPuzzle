using DG.Tweening;
using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class B_WinDialog : B_BaseDialog
{
    [Header("UI")]
    public List<Image> stars;
    public GameObject starContainer;
    public GameObject infoHolder;
    public GameObject buttonHolder;

    [Header("Info")]
    public B_DynamicAmounItemSO ingameStar;
    public bool hasReward = true;

    [Header("Sound")]
    public List<B_SFXEventSO> starSounds;
    public B_SFXEventSO cheerSound;
    public override void ShowThis()
    {
        foreach (Image star in stars)
        {
            star.transform.DOScale(1.8f, 0);
            star.DOFade(0, 0);
        }
        infoHolder?.SetActive(false);
        buttonHolder?.SetActive(false);

        base.ShowThis();
        PlayAnimation();
        if (hasReward) GiveReward();
    }

    public void PlayAnimation()
    {
        StartCoroutine(starAnimationCoroutine());
    }

    public void GiveReward()
    {
        B_PlayerDataHelper.Instance.AddItemById(ingameStar.id, ingameStar.amount.Value);
    }

    IEnumerator starAnimationCoroutine()
    {
        yield return new WaitForSeconds(1f);

        for(int i = 0; i < ingameStar.amount.Value; i++)
        {
            Sequence seq = DOTween.Sequence();

            seq.Append(stars[i].DOFade(1f, 0.3f))
                .Join(stars[i].transform.DOScale(1.3f, 0.3f))
                .Join(stars[i].transform.DOLocalRotate(new(0, 0, 360f), 0.3f, RotateMode.FastBeyond360).SetEase(Ease.Linear))
                .Append(stars[i].transform.DOScale(1f, 0.1f).SetEase(Ease.OutBack))
                .Append(starContainer.transform.DOPunchScale(new(-0.1f, -0.1f, -0.1f), 0.3f));
            

            if(starSounds.Count > 1) B_AudioManager.Instance.PlaySFX(starSounds[i]);
            else B_AudioManager.Instance.PlaySFX(starSounds[0]);

            yield return seq.WaitForCompletion();
        }

        if(infoHolder != null)
        {
            infoHolder.SetActive(true);
            infoHolder.transform.DOPunchScale(new(0.1f, 0.1f, 0.1f), 0.2f);
        }
        if(buttonHolder != null)
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
        B_AudioManager.Instance.PlaySFX(cheerSound);
    }
}
