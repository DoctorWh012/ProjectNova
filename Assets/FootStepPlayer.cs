using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FootStepPlayer : MonoBehaviour
{
    [Header("Component")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] footStepSounds;

    [Header("Settings")]
    [Space(5)]
    [SerializeField, Range(0, 1)] private float footStepAudioVolume = 1;
    [SerializeField] private float minShift = -0.1f;
    [SerializeField] private float maxShift = 0.02f;


    private void Start()
    {
        audioSource.volume = footStepAudioVolume;
        audioSource.pitch = Utilities.GetRandomPitch(minShift, maxShift);
    }

    public void PlayFootStep()
    {
        audioSource.pitch = Utilities.GetRandomPitch(minShift, maxShift);
        audioSource.PlayOneShot(footStepSounds[Random.Range(0, footStepSounds.Length)], footStepAudioVolume);
    }
}
