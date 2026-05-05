using System;
using UnityEngine;

public class B_Tracker : MonoBehaviour
{
    private static B_Tracker instance;

    [Header("Current Tracking")]
    [SerializeField] private string currentLevelId = "";
    [SerializeField] private DateTime levelStartTime;
    [SerializeField] private bool isTracking = false;

    public static B_Tracker Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<B_Tracker>();
                if (instance == null)
                {
                    var obj = new GameObject("B_Tracker");
                    instance = obj.AddComponent<B_Tracker>();
                }
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ==================== TRACK LEVEL START ====================
    public void TrackLevelStart(string levelId = null, string mode = "")
    {
        if (string.IsNullOrEmpty(levelId))
            levelId = B_PlayerDataHelper.Instance.GetPlayerLevel().ToString();

        currentLevelId = levelId;
        levelStartTime = DateTime.UtcNow;
        isTracking = true;

        ZenSDK.instance.TrackLevelStart(levelId, mode);

        Debug.Log($"[B_Tracker] START → Level: {levelId} | Time: {levelStartTime:HH:mm:ss}");
    }

    // ==================== TRACK LEVEL QUIT ====================
    public void TrackLevelQuit(string failedReason = "quit_game", string mode = "")
    {
        if (!isTracking || string.IsNullOrEmpty(currentLevelId))
        {
            Debug.LogWarning("[B_Tracker] TrackLevelQuit called but no active tracking!");
            return;
        }

        float duration = CalculateDuration();

        ZenSDK.instance.TrackLevelFailed(currentLevelId, mode, failedReason, duration);

        Debug.Log($"[B_Tracker] QUIT → Level: {currentLevelId} | Duration: {duration:F2}s | Reason: {failedReason}");

        ResetTracking();
    }

    // ==================== TRACK LEVEL FAILED ====================
    public void TrackLevelFailed(string failedReason = "out_of_star", string mode = "")
    {
        if (!isTracking) return;

        float duration = CalculateDuration();

        ZenSDK.instance.TrackLevelFailed(currentLevelId, mode, failedReason, duration);

        Debug.Log($"[B_Tracker] FAILED → Level: {currentLevelId} | Duration: {duration:F2}s | Reason: {failedReason}");

        ResetTracking();
    }

    // ==================== TRACK LEVEL COMPLETED ====================
    public void TrackLevelCompleted(string mode = "")
    {
        if (!isTracking) return;

        float duration = CalculateDuration();

        ZenSDK.instance.TrackLevelCompleted(currentLevelId, mode, duration);

        Debug.Log($"[B_Tracker] COMPLETED → Level: {currentLevelId} | Duration: {duration:F2}s");

        ResetTracking();
    }

    // ==================== TRACK REWARD ====================
    public void TrackRewardOffer(string placement, string levelId = null)
    {
        if (string.IsNullOrEmpty(levelId))
            levelId = B_PlayerDataHelper.Instance.GetPlayerLevel().ToString();

        ZenSDK.instance.TrackRewardOffer(placement, levelId, ZenSDK.instance.IsNetworkConnected().ToString());

        Debug.Log($"[B_Tracker] REWARD OFFER AT {placement}");
    }

    public void TrackRewardOfferAccept(string placement, string levelId = null)
    {
        if (string.IsNullOrEmpty(levelId))
            levelId = B_PlayerDataHelper.Instance.GetPlayerLevel().ToString();

        ZenSDK.instance.TrackRewardOfferAccept(placement, levelId, ZenSDK.instance.IsNetworkConnected().ToString());

        Debug.Log($"[B_Tracker] REWARD ACCEPT AT {placement}");
    }

    // ==================== TRACK PURCHASE ====================
    public void TrackPurchaseOffer(string sku, string placement, string levelId = null)
    {
        if (string.IsNullOrEmpty(levelId))
            levelId = B_PlayerDataHelper.Instance.GetPlayerLevel().ToString();

        ZenSDK.instance.TrackPurchaseOffer(sku, placement, levelId);

        Debug.Log($"[B_Tracker] PURCHASE OFFER AT {placement}");
    }
    public void TrackPurchaseAccept(string sku, string placement, string levelId = null)
    {
        if (string.IsNullOrEmpty(levelId))
            levelId = B_PlayerDataHelper.Instance.GetPlayerLevel().ToString();

        ZenSDK.instance.TrackPurchaseAccept(sku, placement, levelId);

        Debug.Log($"[B_Tracker] PURCHASE ACCEPT AT {placement}");
    }

    public void TrackPurchaseSuccess(string sku, string placement, string levelId = null)
    {
        if (string.IsNullOrEmpty(levelId))
            levelId = B_PlayerDataHelper.Instance.GetPlayerLevel().ToString();

        ZenSDK.instance.TrackPurchaseSuccess(sku, placement, levelId);

        Debug.Log($"[B_Tracker] PURCHASE SUCCESS AT {placement}");
    }

    public void TrackPurchaseFailed(string sku, string placement, string failedReason, string levelId = null)
    {
        if (string.IsNullOrEmpty(levelId))
            levelId = B_PlayerDataHelper.Instance.GetPlayerLevel().ToString();

        ZenSDK.instance.TrackPurchaseFail(sku, placement, levelId, failedReason);

        Debug.Log($"[B_Tracker] PURCHASE FAILED AT {placement}");
    }

    // ==================== HELPER ====================
    private float CalculateDuration()
    {
        return (float)(DateTime.UtcNow - levelStartTime).TotalSeconds;
    }

    private void ResetTracking()
    {
        currentLevelId = "";
        isTracking = false;
    }

    // Tự động track quit khi game bị đóng đột ngột
    private void OnApplicationQuit()
    {
        if (isTracking)
            TrackLevelQuit("application_quit");
    }

    // ==================== EDITOR BUTTONS ====================
}