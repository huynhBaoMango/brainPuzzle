using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class PromoUI : MonoBehaviour
{
    public TextMeshProUGUI countDownText;
    public Button closeBt;

    private static PromoUI instance;
    string placement = null;
    private int countDownSec;

    public List<CrossPromoItemSO> crossPromoItemSOs;

    public static PromoUI Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<PromoUI>(FindObjectsInactive.Include);
                if (instance == null)
                {
                    GameObject obj = new GameObject("PromoUI");
                    instance = obj.AddComponent<PromoUI>();
                }
            }
            return instance;
        }
    }

    public void OpenPromoUI()
    {
        //placement = placementString;
        countDownSec = ZenSDK.instance.GetConfigInt("fullscreenCrossPromoDuration", 3);
        gameObject.SetActive(true);
        
        StartCoroutine(delayToEnableCloseBt());

        CrossPromoItemSO randomPromoGame = crossPromoItemSOs[Random.Range(0, crossPromoItemSOs.Count)];
        string link = BuildCrossPromoLink(randomPromoGame.id);

        GetComponent<Image>().sprite = randomPromoGame.banner;

        GetComponent<Button>().onClick.AddListener(() =>
        {
            Application.OpenURL(link);
            ZenSDK.instance.TrackPromoClick("fullscreen", randomPromoGame.id);
        });

        ZenSDK.instance.TrackPromoOffer("fullscreen");
    }

    IEnumerator delayToEnableCloseBt()
    {
        for (int i = 0; i < countDownSec; i++)
        {
            countDownText.text = (countDownSec - i).ToString();
            yield return new WaitForSeconds(1f);
        }
        closeBt.interactable = true;
    }

    public void CloseBt()
    {
        gameObject.SetActive(false);
        closeBt.interactable = false;
    }

    public string BuildCrossPromoLink(string targetPackageName)
    {

        string rawReferrer = $"utm_source=cross_promotion&utm_medium={ConstValue.GAME_PACKAGE_ID}";
        string encodedReferrer = UnityEngine.Networking.UnityWebRequest.EscapeURL(rawReferrer);

        return $"https://play.google.com/store/apps/details?id={targetPackageName}&referrer={encodedReferrer}";
    }
}
