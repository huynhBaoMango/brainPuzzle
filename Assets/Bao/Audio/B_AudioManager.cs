using System.Collections.Generic;
using UnityEngine;

public class B_AudioManager : MonoBehaviour
{
    public static B_AudioManager Instance { get; private set; }

    [Header("=== Settings SO ===")]
    [Tooltip("B_BoolSO cho Music/BGM")]
    public B_BoolSO musicEnabled;

    [Tooltip("B_BoolSO cho SFX")]
    public B_BoolSO sfxEnabled;

    [Tooltip("B_BoolSO cho Vibration")]
    public B_BoolSO vibrationEnabled;


    [Header("=== Audio Sources ===")]
    [Tooltip("AudioSource riêng cho nhạc nền (thường loop)")]
    public AudioSource bgmSource;

    [Tooltip("Pool AudioSource cho SFX")]
    public List<AudioSource> sfxPool = new List<AudioSource>();

    [Header("Pool config")]
    public int poolSize = 15;


    [Header("Background Music")]
    public B_MusicEventSO defaultBGM;


    private void Reset()
    {
        if (bgmSource == null && !Application.isPlaying)
        {
            CreateBGMSourceInEditor();
        }
    }

    private void CreateBGMSourceInEditor()
    {
        GameObject bgmGo = new GameObject("BGM_AudioSource");
        bgmGo.transform.SetParent(transform);
        bgmGo.transform.localPosition = Vector3.zero;
#if UNITY_EDITOR
        bgmGo.tag = "EditorOnly";
#endif

        bgmSource = bgmGo.AddComponent<AudioSource>();

        // Cấu hình chuẩn cho BGM
        bgmSource.playOnAwake = false;
        bgmSource.loop = true;
        bgmSource.spatialBlend = 0f;    // 2D
        bgmSource.volume = 1f;
        bgmSource.pitch = 1f;
        bgmSource.priority = 0;

        Debug.Log("[SoundManager] Tự động tạo BGM_AudioSource trong Editor: " + bgmGo.name);

        // Đánh dấu scene dirty để Unity lưu thay đổi
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.EditorUtility.SetDirty(bgmGo);
#endif
    }


    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;

        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Tạo pool SFX
        for (int i = 0; i < poolSize; i++)
        {
            GameObject go = new GameObject("SFX_Pool_" + i);
            go.transform.SetParent(transform);
            AudioSource src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;
            sfxPool.Add(src);
        }
    }

    private void Start()
    {
        if (musicEnabled != null)
        {
            musicEnabled.OnValueChanged += OnMusicSettingChanged;
        }
        OnMusicSettingChanged();
    }

    private void OnDestroy()
    {
        if (musicEnabled != null)
        {
            musicEnabled.OnValueChanged -= OnMusicSettingChanged;
        }
    }

    // ==================== BGM ====================
    private void OnMusicSettingChanged()
    {
        if (musicEnabled == null) return;

        if (musicEnabled.Value)
        {
            if (bgmSource.clip != null)
            {
                ResumeBGM();
            }
            else if (defaultBGM != null)
            {
                PlayBGM(defaultBGM);
            }
        }
        else
        {
            PauseBGM();
        }
    }


    public void PlayBGM(B_MusicEventSO bgmEvent)
    {
        if (!musicEnabled.Value ||
            bgmEvent == null || bgmEvent.clip == null)
            return;

        bgmSource.Stop();
        bgmSource.clip = bgmEvent.clip;
        bgmSource.volume = bgmEvent.volume;
        bgmSource.pitch = bgmEvent.pitch;
        bgmSource.loop = bgmEvent.loop;
        bgmSource.Play();
    }

    public void StopBGM() => bgmSource?.Stop();
    public void PauseBGM() => bgmSource?.Pause();
    public void ResumeBGM() => bgmSource?.UnPause();

    // ==================== SFX ====================
    public void PlaySFX(B_SFXEventSO sfxEvent)
    {
        if (!sfxEnabled.Value ||
            sfxEvent == null || sfxEvent.clip == null)
            return;

        AudioSource source = GetFreeSFXSource();
        if (source == null) return;

        source.clip = sfxEvent.clip;
        source.volume = sfxEvent.volume;
        source.pitch = sfxEvent.pitch;
        source.loop = sfxEvent.loop;
        source.Play();
    }

    /// <summary>
    /// Plays an AudioClip one-shot at default volume/pitch. Convenience
    /// overload for callers that hold a direct AudioClip reference rather
    /// than a B_SFXEventSO wrapper.
    /// </summary>
    public void PlaySFX(AudioClip clip)
    {
        if (!sfxEnabled.Value || clip == null) return;

        AudioSource source = GetFreeSFXSource();
        if (source == null) return;

        source.clip = clip;
        source.volume = 1f;
        source.pitch = 1f;
        source.loop = false;
        source.Play();
    }

    private AudioSource GetFreeSFXSource()
    {
        foreach (var src in sfxPool)
        {
            if (!src.isPlaying)
                return src;
        }
        return null;
    }

    // ==================== Vibration ====================
    public void PlayVibration(B_VibraEventSO vibEvent)
    {
        if (!vibrationEnabled.Value || vibEvent == null)
            return;

        Vibrate(vibEvent.durationMs);
    }

    private void Vibrate(long ms)
    {
        Debug.Log("Vibrate for " + ms);
        if (!Application.isMobilePlatform) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        AndroidJavaObject vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
        if (vibrator.Call<bool>("hasVibrator"))
            vibrator.Call("vibrate", ms);
#elif UNITY_IOS && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }
}