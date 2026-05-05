using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class B_RewardDialog : B_BaseDialog
{
    private static B_RewardDialog _instance;

    public static B_RewardDialog Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<B_RewardDialog>(FindObjectsInactive.Include);

                if (_instance == null)
                {
                    Debug.LogWarning("[B_RewardDialog] Không tìm thấy instance trong scene! " +
                                     "Đang tạo mới một GameObject tự động...");

                    GameObject go = new GameObject(nameof(B_RewardDialog));
                    _instance = go.AddComponent<B_RewardDialog>();
                }
            }
            return _instance;
        }
    }

    protected B_RewardDialog() { }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
    }

    [Header("UI")]
    [SerializeField] private B_RewardItemUI rewardItemPrefab;
    [SerializeField] private Transform rewardBox;
    [SerializeField] private GameObject skipBt;
    [SerializeField] private GameObject closeBt;

    [Header("Sound")]
    [SerializeField] private B_SFXEventSO rewardSound;

    private List<B_RewardItemUI> rewardObjects = new List<B_RewardItemUI>();
    private Coroutine _spawnRoutine;
    private bool _isSkipping = false;

    [Header("Reward Event")]
    public UnityEvent OnRewarded;

    public void SkipAnimation()
    {
        if (_spawnRoutine != null)
        {
            StopCoroutine(_spawnRoutine);
            _spawnRoutine = null;
        }

        _isSkipping = true;

        foreach (var item in rewardObjects)
        {
            if (item == null) continue;

            DOTween.Kill(item.transform);
            item.transform.localScale = Vector3.one;
            item.gameObject.SetActive(true);

        }
        B_AudioManager.Instance.PlaySFX(rewardSound);
        closeBt.SetActive(true);
        skipBt.SetActive(false);
    }

    // ── Methods ─────────────────────────────────────────────────
    public override void ShowThis()
    {
        _isSkipping = false;
        skipBt.SetActive(true);
        closeBt.SetActive(false);
        base.ShowThis();
        PlayAnimation();
    }

    public override void CloseThis()
    {
        if (_spawnRoutine != null)
        {
            StopCoroutine(_spawnRoutine);
            _spawnRoutine = null;
        }

        _isSkipping = false;

        foreach (var t in rewardObjects)
        {
            if (t != null && t.gameObject != null)
            {
                DOTween.Kill(t.transform);
                Destroy(t.gameObject);
            }
        }
        rewardObjects.Clear();
        OnRewarded.Invoke();
        base.CloseThis();
    }

    public void AddItem(B_ItemSO data)
    {
        B_RewardItemUI newItem = Instantiate(rewardItemPrefab, rewardBox);
        newItem.Setup(data.icon, data.amount);
        newItem.transform.localScale = Vector3.zero;
        newItem.gameObject.SetActive(false);
        rewardObjects.Add(newItem);
    }

    private void PlayAnimation()
    {
        _spawnRoutine = StartCoroutine(SpawnItemCoroutine());
    }

    private IEnumerator SpawnItemCoroutine()
    {
        yield return new WaitForSeconds(0.5f);

        if (_isSkipping) yield break;

        foreach (var item in rewardObjects)
        {
            if (item == null) continue;

            item.gameObject.SetActive(true);
            item.transform.localScale = Vector3.zero;

            B_AudioManager.Instance.PlaySFX(rewardSound);
            yield return item.transform
                .DOScale(1f, 0.5f)
                .SetEase(Ease.OutBack).WaitForCompletion();

            if (_isSkipping) break;
        }

        closeBt.SetActive(true);
        skipBt.SetActive(false);

        _spawnRoutine = null;
    }
}