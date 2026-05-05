using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Localization.Settings;

public class B_LocalizationManager : MonoBehaviour
{
    public B_IntSO languageIndexSO;

    public static B_LocalizationManager instance;

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private IEnumerator Start()
    {
        yield return LocalizationSettings.InitializationOperation;

        languageIndexSO.OnValueChanged += SetLocale;
        SetLocale();
    }

    private void SetLocale()
    {
        if (languageIndexSO.Value >= LocalizationSettings.AvailableLocales.Locales.Count)
        {
            languageIndexSO.Value = 0;
        }

        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[languageIndexSO.Value];

        // Also drive our own strings.json-based lookup used by MessageDisplay.
        // Index 0 = English, 1 = Vietnamese (matches locale order in Project Settings).
        B_LevelConfig.CurrentLanguage = languageIndexSO.Value == 1 ? "vn" : "en";
    }

}
