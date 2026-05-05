using DG.Tweening;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class B_Loading : MonoBehaviour
{
    [Header("Thời gian để fill từ 90% → 100% sau khi load async xong")]
    public float waitTimeAfterLoadingDone = 1.5f;

    [Header("Tên scene tiếp theo")]
    public string nextSceneName;


    [SerializeField] private Slider progressBar;
    [SerializeField] private GameObject logo;
    [SerializeField] private B_LoadingPanel loadingPanelPrefab;
    public Tween breathingLogoTween;

    private AsyncOperation asyncOperation;

    private void Start()
    {
        progressBar.value = 0f;
        progressBar.maxValue = 1f;

        breathingLogoTween = B_UIAnimation.BounceLoop(logo);

        StartCoroutine(LoadSceneAsync());
    }

    private IEnumerator LoadSceneAsync()
    {
        asyncOperation = SceneManager.LoadSceneAsync(nextSceneName);
        asyncOperation.allowSceneActivation = false;

        while (asyncOperation.progress < 0.9f)
        {
            progressBar.value = asyncOperation.progress;
            yield return null;
        }

        progressBar.value = 0.9f;

        float elapsed = 0f;
        while (elapsed < (waitTimeAfterLoadingDone * 0.9f))
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (waitTimeAfterLoadingDone * 0.9f);
            yield return null;
        }

        progressBar.value = 1f;
        yield return new WaitForSeconds(waitTimeAfterLoadingDone * 0.1f);
        HandleLogoAndProgressBar();

        yield return new WaitForSeconds(0.4f);

        if (!B_PlayerDataHelper.Instance.GetPlayerAdsFree() 
            && ZenSDK.instance.IsNetworkConnected() 
            && ZenSDK.instance.IsAppOpenReady())
        {
            ZenSDK.instance.ShowAppOpen((bool success) =>
            {
                B_SceneController.Instance.ChangeSceneBool(asyncOperation, false, true);
            });
        }
        else
        {
            B_SceneController.Instance.ChangeSceneBool(asyncOperation, false, true);
        }

        


    }

    void HandleLogoAndProgressBar()
    {
        breathingLogoTween?.Kill();

        logo.transform.DOLocalMoveY(0, 0.3f);
        logo.transform.localScale = Vector3.one;
        progressBar.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        breathingLogoTween?.Kill();
    }
}