using TMPro;
using UnityEngine;

public class B_SetDataText : MonoBehaviour
{
    public B_StringSO textRef;
    public TextMeshProUGUI textComponent;

    public void SetData()
    {
        if (textRef != null && textComponent != null)
        {
            textRef.Value = textComponent.text;
        }
    }
}
