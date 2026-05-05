using NaughtyAttributes;
using System.Collections;
using UnityEngine;

public class B_AdsBanner : MonoBehaviour
{
    [SerializeField] private B_BoolSO playerAdsFree;

    private void Start()
    {
        playerAdsFree.OnValueChanged += UpdateBanner;
        UpdateBanner();
    }

    private void OnDestroy()
    {
        playerAdsFree.OnValueChanged -= UpdateBanner;
        ZenSDK.instance.ShowBanner(false);
    }

    [Button]
    void UpdateBanner()
    {
        ZenSDK.instance.ShowBanner(!playerAdsFree.Value);
    }
}
