using TMPro;
using UnityEngine;
using UnityEngine.Purchasing;

[RequireComponent(typeof(TextMeshProUGUI))]
public class B_IAPPriceText : MonoBehaviour
{
    private TextMeshProUGUI text;

    private void Awake()           // ← Đổi từ Start() sang Awake()
    {
        text = GetComponent<TextMeshProUGUI>();

        // Safety check
        if (text == null)
            Debug.LogError($"[B_IAPPriceText] TextMeshProUGUI component not found on {gameObject.name}");
    }

    /// <summary>
    /// Gọi từ OnProductFetched của IAP Button
    /// </summary>
    public void UpdatePrice(Product product)
    {
        if (text == null)
        {
            text = GetComponent<TextMeshProUGUI>(); // fallback
        }

        if (product == null || product.metadata == null)
        {
            text.text = "N/A";
            Debug.LogWarning($"[B_IAPPriceText] Product or metadata is null on {gameObject.name}");
            return;
        }

        // Cách an toàn nhất
        string priceString = product.metadata.localizedPriceString;

        if (string.IsNullOrEmpty(priceString))
            priceString = product.metadata.localizedPrice.ToString("C"); // fallback

        text.text = priceString;

        // Optional: Log để debug
        // Debug.Log($"[IAP Price] {product.definition.id} → {priceString}");
    }
}