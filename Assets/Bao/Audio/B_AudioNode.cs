using UnityEngine;
using UnityEngine.UI;

public class B_AudioNode : MonoBehaviour
{
    [SerializeField] private B_AudioEventSO eventSO;
    [SerializeField] private bool playOnAwake = false;

    private Button _button;
    private Toggle _toggle;

    private void OnEnable()
    {
        if (playOnAwake && eventSO != null)
        {
            Play();
        }

        _button = GetComponent<Button>();
        _toggle = GetComponent<Toggle>();

        if (_button != null)
        {
            _button.onClick.RemoveListener(Play); 
            _button.onClick.AddListener(Play);
        }

        if (_toggle != null)
        {
            _toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
            _toggle.onValueChanged.AddListener(OnToggleValueChanged);
        }
    }

    private void OnDisable()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(Play);
        }

        if (_toggle != null)
        {
            _toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
        }
    }

    private void OnToggleValueChanged(bool isOn)
    {
        Play();
    }

    public void Play()
    {
        if (eventSO == null)
        {
            Debug.Log("Something is missing");
            return;
        }

        if (eventSO is B_MusicEventSO musicEvent)
        {
            B_AudioManager.Instance.PlayBGM(musicEvent);
        }
        else if (eventSO is B_SFXEventSO sfxEvent)
        {
            B_AudioManager.Instance.PlaySFX(sfxEvent);
        }
        else if (eventSO is B_VibraEventSO vibraEvent)
        {
            B_AudioManager.Instance.PlayVibration(vibraEvent);
        }
    }
}