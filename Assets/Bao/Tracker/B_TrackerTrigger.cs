using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

public class B_TrackerTrigger : MonoBehaviour
{
    [Header("Tracker Action")]
    [SerializeField] private TrackerAction action = TrackerAction.LevelStart;

    [Header("Parameters")]
    [SerializeField] private string customLevelId = "";      // Để trống sẽ tự lấy từ Player Level
    [SerializeField] private string mode = "";
    [SerializeField] private string failedReason = "";
    [SerializeField] private string placement = "";

    [Header("Events")]
    public UnityEvent onTriggered;

    public enum TrackerAction
    {
        LevelStart,
        LevelQuit,
        LevelFailed,
        LevelCompleted,
        RewardOffer,
        RewardAccept,
        PurchaseOffer,
        PurchaseAccept,
        PurchaseSuccess,
        PurchaseFailed
    }

    public void Trigger()
    {
        if (B_Tracker.Instance == null)
        {
            Debug.LogError("[B_TrackerTrigger] B_Tracker.Instance is null!");
            return;
        }

        // Lấy levelId (ưu tiên custom → sau đó lấy từ PlayerData)
        string levelId = string.IsNullOrEmpty(customLevelId)
            ? B_PlayerDataHelper.Instance.GetPlayerLevel().ToString()
            : customLevelId;

        switch (action)
        {
            case TrackerAction.LevelStart:
                B_Tracker.Instance.TrackLevelStart(levelId, mode);
                break;

            case TrackerAction.LevelQuit:
                B_Tracker.Instance.TrackLevelQuit(failedReason, mode);
                break;

            case TrackerAction.LevelFailed:
                B_Tracker.Instance.TrackLevelFailed(failedReason, mode);
                break;

            case TrackerAction.LevelCompleted:
                B_Tracker.Instance.TrackLevelCompleted(mode);
                break;
            case TrackerAction.RewardOffer:
                B_Tracker.Instance.TrackRewardOffer(placement);
                break;
            case TrackerAction.RewardAccept:
                B_Tracker.Instance.TrackRewardOfferAccept(placement);
                break;
        }

        onTriggered?.Invoke();
    }

    public void TriggerProductFailed(Product product, PurchaseFailureDescription failedreason)
    {
        if (B_Tracker.Instance == null)
        {
            Debug.LogError("[B_TrackerTrigger] B_Tracker.Instance is null!");
            return;
        }

        switch (action)
        {
            case TrackerAction.PurchaseFailed:
                B_Tracker.Instance.TrackPurchaseFailed(product.definition.id, placement, failedreason.reason.ToString());
                break;
        }

        onTriggered?.Invoke();
    }

    public void TriggerProduct(Product product)
    {
        if (B_Tracker.Instance == null)
        {
            Debug.LogError("[B_TrackerTrigger] B_Tracker.Instance is null!");
            return;
        }

        switch (action)
        {
            case TrackerAction.PurchaseOffer:
                B_Tracker.Instance.TrackPurchaseOffer(product.definition.id, placement);
                break;
            case TrackerAction.PurchaseAccept:
                B_Tracker.Instance.TrackPurchaseAccept(product.definition.id, placement);
                break;
            case TrackerAction.PurchaseSuccess:
                B_Tracker.Instance.TrackPurchaseSuccess(product.definition.id, placement);
                break;
        }

        onTriggered?.Invoke();
    }

    [ContextMenu("Trigger Now")]
    private void TestTrigger()
    {
        Trigger();
    }
}