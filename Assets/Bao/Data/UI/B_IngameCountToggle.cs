using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class B_IngameCountToggle : MonoBehaviour
{
    public B_IntSO starCountRef;

    public List<Toggle> toggles;

    public void Start()
    {
        UpdateToggles();
        starCountRef.OnValueChanged += UpdateToggles;
    }

    private void OnDestroy()
    {
        starCountRef.OnValueChanged -= UpdateToggles;
    }

    private void UpdateToggles()
    {
        for(int i = 0; i < toggles.Count; i++)
        {
            toggles[i].SetIsOnWithoutNotify((i + 1) <= starCountRef.Value);
        }
    }
}
