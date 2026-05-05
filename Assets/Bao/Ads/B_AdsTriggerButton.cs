using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class B_AdsTriggerButton : MonoBehaviour
{
    [Header("Ad Settings")]
    [SerializeField] private string placement = "default";
    [SerializeField] private AdsType typeOfAds = AdsType.Rewarded;

    [Header("Value Reward")]
    [SerializeField] private List<B_DynamicAmounItemSO> valueReward; 

    [Header("Action Reward")]
    [SerializeField] private UnityEvent onRewardSuccess = new UnityEvent();

    [Header("Failed / Closed")]
    [SerializeField] private UnityEvent onRewardFailed = new UnityEvent();

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(TriggerAd);
    }

    private void OnDestroy()
    {
        button.onClick.RemoveListener(TriggerAd);
    }

    private void TriggerAd()
    {
        string levelStr = B_PlayerDataHelper.Instance.GetPlayerLevel().ToString();

        switch (typeOfAds)
        {
            case AdsType.FullScreen:
                if (B_PlayerDataHelper.Instance.GetPlayerAdsFree()) break;
                ZenSDK.instance.ShowFullScreen(placement, levelStr);
                break;

            case AdsType.Rewarded:
                button.interactable = false;

                ZenSDK.instance.ShowVideoReward(OnRewardedCallback, placement, levelStr);
                break;
        }
    }

    private void OnRewardedCallback(bool success)
    {
        button.interactable = true;

        if (success)
        {
            // 1. Value Reward (nếu có)
            if (valueReward.Count > 0)
            {
                foreach (var item in valueReward) 
                {
                    B_RewardDialog.Instance.AddItem(new B_ItemSO { id = item.id, amount = item.amount.Value, icon = item.icon });
                    B_PlayerDataHelper.Instance.AddItemById(item.id, item.amount.Value);
                }
                B_RewardDialog.Instance.ShowThis();
            }
                

            onRewardSuccess?.Invoke();

            Debug.Log("Reward Success!");
        }
        else
        {
            onRewardFailed?.Invoke();
        }
    }

    [System.Serializable]
    public enum AdsType
    {
        FullScreen,
        Rewarded
    }
}