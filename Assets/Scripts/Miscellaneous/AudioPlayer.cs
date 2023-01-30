using UnityEngine;

public class AudioPlayer : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    
    public void PlayAudioOneShot(AudioClip sfx)
    {
        SoundManager.Instance.PlaySound(audioSource, sfx);
    }
}
