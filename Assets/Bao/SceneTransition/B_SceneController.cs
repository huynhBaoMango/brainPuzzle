using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class B_SceneController : MonoBehaviour
{
    public static B_SceneController Instance;

    [SerializeField] private B_LoadingPanel loadingPanelPrefab;

    [SerializeField] private B_SFXEventSO logoInSound;
    [SerializeField] private B_SFXEventSO logoOutSound;

    private B_LoadingPanel currentPanel;

    private Transform uiRoot;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            GameObject root = new GameObject("GlobalUIRoot");
            DontDestroyOnLoad(root);
            uiRoot = root.transform;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ChangeScene(string sceneName, bool inAnimation = true, bool outAnimation = true)
    {
        StartCoroutine(LoadSceneRoutine(sceneName, inAnimation, outAnimation));
    }

    private IEnumerator LoadSceneRoutine(string sceneName, bool inAnimation, bool outAnimation)
    {
        currentPanel = Instantiate(loadingPanelPrefab, uiRoot);

        if (inAnimation)
        {
            if (logoInSound != null) B_AudioManager.Instance.PlaySFX(logoInSound);
            yield return currentPanel.PlayIn();
        }

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone)
        {
            yield return null;
        }

        if (outAnimation)
        {
            if (logoOutSound != null) B_AudioManager.Instance.PlaySFX(logoOutSound);
            yield return currentPanel.PlayOut();
        }

        Destroy(currentPanel.gameObject);
    }

    private IEnumerator LoadSceneByBoolRoutine(AsyncOperation asyncOp, bool inAnimation, bool outAnimation)
    {
        currentPanel = Instantiate(loadingPanelPrefab, uiRoot);

        if (inAnimation)
        {
            if (logoInSound != null) B_AudioManager.Instance.PlaySFX(logoInSound);
            yield return currentPanel.PlayIn();
        }

        asyncOp.allowSceneActivation = true;

        if (outAnimation)
        {
            if (logoOutSound != null) B_AudioManager.Instance.PlaySFX(logoOutSound);
            yield return currentPanel.PlayOut();
        }

        Destroy(currentPanel.gameObject);
    }

    public void ChangeSceneBool(AsyncOperation asyncOp, bool inAnimation = true, bool outAnimation = true)
    {
        StartCoroutine(LoadSceneByBoolRoutine(asyncOp, inAnimation, outAnimation));
    }
}
