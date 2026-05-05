using UnityEngine;
[CreateAssetMenu(menuName = "Bao/Audio/Music")]
public class B_MusicEventSO : B_AudioEventSO
{
    public AudioClip clip;
    public float volume = 1f;
    public float pitch = 1f;
    public bool loop = true;
}
