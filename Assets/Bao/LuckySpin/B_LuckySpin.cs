using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using NaughtyAttributes;
using UnityEngine.Events;

public class B_LuckySpin : MonoBehaviour
{
    [Header("Data")]
    public List<B_ItemSO> items;

    [Header("UI")]
    public Transform wheelRoot;
    public B_LuckySpinItemUI itemPrefab;
    public GameObject buttonHolder;

    [Header("Spin Config")]
    public float spinDuration = 3f;
    public int extraSpins = 5;

    [Header("Sound")]
    public B_SFXEventSO spinTickSound; 
    [Range(0.01f, 0.2f)]
    public float minTickInterval = 0.05f;
    [Range(0.1f, 0.6f)]
    public float maxTickInterval = 0.35f;

    private List<B_LuckySpinItemUI> spawnedItems = new();
    private bool isSpinning = false;

    [Header("Events After Reward")]
    public UnityEvent rewardEvent;

    #region Spawn
    [Button("Spawn Items")]
    public void SpawnItems()
    {
        Clear();
        int count = items.Count;
        float angleStep = 360f / count;

        for (int i = 0; i < count; i++)
        {
            var item = Instantiate(itemPrefab, wheelRoot);
            item.Setup(items[i]);

            float angle = -i * angleStep;
            item.transform.localRotation = Quaternion.Euler(0, 0, angle);
            item.transform.localPosition = Quaternion.Euler(0, 0, angle) * Vector3.up * 200f;

            spawnedItems.Add(item);
        }
    }

    private void Clear()
    {
        foreach (Transform child in wheelRoot)
            DestroyImmediate(child.gameObject);

        spawnedItems.Clear();
    }
    #endregion

    #region Spin
    public void Spin()
    {
        if (items.Count == 0 || isSpinning) return;

        buttonHolder.SetActive(false);
        isSpinning = true;

        int targetIndex = Random.Range(0, items.Count);
        SpinToIndex(targetIndex);
    }

    public void SpinToIndex(int index)
    {
        int count = items.Count;
        float anglePerItem = 360f / count;

        float totalRotation = 360f * (extraSpins + 2) + (index * anglePerItem);

        var spinTween = wheelRoot.DORotate(new Vector3(0, 0, totalRotation), spinDuration, RotateMode.FastBeyond360)
            .SetEase(Ease.OutCubic);

        StartCoroutine(PlayTickSoundDuringSpin(spinTween));

        spinTween.OnComplete(() =>
        {
            GiveReward(index);
            isSpinning = false;
            buttonHolder.SetActive(true);
            rewardEvent.Invoke();   
        });
    }

    private IEnumerator PlayTickSoundDuringSpin(Tween spinTween)
    {
        if (spinTickSound == null || B_AudioManager.Instance == null)
            yield break;

        float elapsed = 0f;
        float lastTickTime = 0f;

        while (spinTween.IsActive() && !spinTween.IsComplete())
        {
            elapsed = spinTween.Elapsed();
            float progress = Mathf.Clamp01(elapsed / spinDuration); 
            float currentInterval = Mathf.Lerp(minTickInterval, maxTickInterval, progress);

            if (Time.time - lastTickTime >= currentInterval)
            {
                B_AudioManager.Instance.PlaySFX(spinTickSound);
                lastTickTime = Time.time;
            }

            yield return null;
        }
    }

    private void GiveReward(int index)
    {
        var wonItem = items[index];
        B_RewardDialog.Instance.AddItem(wonItem);
        B_RewardDialog.Instance.ShowThis();
        B_PlayerDataHelper.Instance.AddItemById(wonItem.id, wonItem.amount);

    }
    #endregion
}