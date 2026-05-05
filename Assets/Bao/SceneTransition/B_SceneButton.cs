using System.Collections;
using UnityEngine;

public class B_SceneButton : MonoBehaviour
{
    [SerializeField] private float delayTime = 0;
    [SerializeField] private string sceneName;
    [SerializeField] private bool playIn = true;
    [SerializeField] private bool playOut = true;

    public void LoadScene()
    {
        StartCoroutine(delayToLoadScene());
    }

    IEnumerator delayToLoadScene()
    {
        yield return new WaitForSeconds(delayTime);
        B_SceneController.Instance.ChangeScene(sceneName, playIn, playOut);
    }
}