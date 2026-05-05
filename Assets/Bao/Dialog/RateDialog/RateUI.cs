using System;
using UnityEngine;

public class RateUI : B_BaseDialog
{
    string placement = null;
    public B_StringSO lastRatePopUpSO;
    public B_BoolSO rated;

    private static RateUI instance;

    public static RateUI Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<RateUI>(FindObjectsInactive.Include);
                if (instance == null)
                {
                    GameObject obj = new GameObject("RateUI");
                    instance = obj.AddComponent<RateUI>();
                }
            }
            return instance;
        }
    }

    public void RequestOpenRateUI(string placement)
    {
        Debug.Log("RATE UI REQUESTING...");

        int minLevel = ZenSDK.instance.GetConfigInt("minLevelToPopUpRate", 10);
        int currentLevel = B_PlayerDataHelper.Instance.GetPlayerLevel();

        if (rated.Value)
        {
            Debug.Log("RATE UI: Đã đánh giá rồi, không hiển thị nữa");
            return;
        }

        // Bước 1: Kiểm tra level trước
        if (currentLevel < minLevel)
        {
            Debug.Log($"RATE UI: Level chưa đủ ({currentLevel}/{minLevel})");
            return;
        }

        // Bước 2: Kiểm tra lastRatePopUp
        string lastRatePopUp = lastRatePopUpSO.Value;

        // Nếu là lần đầu (chuỗi rỗng) → Popup luôn
        if (string.IsNullOrEmpty(lastRatePopUp))
        {
            Debug.Log("RATE UI: Lần đầu tiên → Popup Rate UI");
            OpenRateUI(placement);
            lastRatePopUpSO.Value = DateTime.UtcNow.ToString("o");   // Lưu thời gian hiện tại
            return;
        }

        // Bước 3: Đã từng popup rồi → kiểm tra delay
        if (DateTime.TryParse(lastRatePopUp, out DateTime lastSkipTime))
        {
            int minDaysDelay = ZenSDK.instance.GetConfigInt("minDayDelayToPopUpRateAgain", 1);
            TimeSpan timeSinceSkip = DateTime.UtcNow - lastSkipTime;

            if (timeSinceSkip.TotalDays >= minDaysDelay)
            {
                Debug.Log($"RATE UI: Đủ delay ({timeSinceSkip.TotalDays:F1} ngày) → Popup Rate UI");
                OpenRateUI(placement);
                lastRatePopUpSO.Value = DateTime.UtcNow.ToString("o");   // Cập nhật thời gian mới
            }
            else
            {
                Debug.Log($"RATE UI: Còn delay → Chưa popup ({timeSinceSkip.TotalDays:F1}/{minDaysDelay} ngày)");
            }
        }
        else
        {
            // Trường hợp parse lỗi (dữ liệu hỏng)
            Debug.LogWarning("RATE UI: lastRatePopUpSO parse thất bại → Popup và reset thời gian");
            OpenRateUI(placement);
            lastRatePopUpSO.Value = DateTime.UtcNow.ToString("o");
        }
    }
    public void OpenRateUI(string placementString)
    {
        placement = placementString;
        lastRatePopUpSO.Value = DateTime.UtcNow.ToString("yyyy-MM-dd");

        ShowThis();
    }

    public void CloseRateUI()
    {
        CloseThis();
        ZenSDK.instance.TrackRateSelect(placement, "skip");
        lastRatePopUpSO.Value = DateTime.UtcNow.ToString("yyyy-MM-dd");
    }

    public void GoodRateBt()
    {
        rated.Value = true;
        ZenSDK.instance.TrackRateSelect(placement, "good");
        ZenSDK.instance.RateInApp();
        CloseThis();
    }
    public void BadRateBt() 
    {
        lastRatePopUpSO.Value = DateTime.UtcNow.ToString("yyyy-MM-dd");
        ZenSDK.instance.TrackRateSelect(placement, "bad");
        CloseThis();
    }

}
