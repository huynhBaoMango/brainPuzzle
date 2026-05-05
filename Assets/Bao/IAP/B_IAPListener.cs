using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

public class B_IAPListener : MonoBehaviour
{
    public void OnPurchaseComplete(Product product)
    {
        string id = product.definition.id;

        Debug.Log($"Mua thành công: {id}");

        switch (id)
        {
            case ConstValue.REMOVEADS_PACK_ID:
                B_PlayerDataHelper.Instance.SetPlayerAdsFree(true);
                break;

            default:
                Debug.LogWarning("Không tìm thấy sản phẩm: " + id);
                break;
        }

    }
    public void OnPurchaseFailed(Product product, PurchaseFailureDescription reason)
    {
        Debug.LogError($"Mua thất bại {product.definition.id}: {reason}");
    }

    public void OnRestoreSuccess()
    {
        Debug.Log("[IAP] Restore completed successfully!");
        CheckNonConsumableAfterRestore();
    }

    private void CheckNonConsumableAfterRestore()
    {
        var product = CodelessIAPStoreListener.Instance?.GetProduct(ConstValue.REMOVEADS_PACK_ID);
        if (product != null && product.hasReceipt)
        {
            B_PlayerDataHelper.Instance.SetPlayerAdsFree(true);
            Debug.Log("[IAP] Remove Ads restored from previous purchase.");
        }
    }
}