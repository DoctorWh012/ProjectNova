using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioSourceConfig : MonoBehaviour
{
    [Header("Settings")]
    [Space(5)]
    [SerializeField, Range(0, 1)] private float audioVolume = 1;
    [SerializeField] private float minShift = -0.1f;
    [SerializeField] private float maxShift = 0.02f;

    private AudioSource audioSource;

    private void Start()
    {
        audioSource = gameObject.GetComponent<AudioSource>();
        audioSource.volume= audioVolume;
        audioSource.pitch = Utilities.GetRandomPitch(minShift, maxShift);
    }
}
