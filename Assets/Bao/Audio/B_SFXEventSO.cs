using UnityEngine;

[CreateAssetMenu(menuName = "Bao/Audio/SFX")]
public class B_SFXEventSO : B_AudioEventSO
{
    public AudioClip clip;
    public float volume = 1f;
    public float pitch = 1f;
    public bool loop = false;
}
