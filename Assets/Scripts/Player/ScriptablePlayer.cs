using UnityEngine;
using System;

[CreateAssetMenu(fileName = "ScriptablePlayer", menuName = "RUSP/ScriptablePlayer", order = 0)]
public class ScriptablePlayer : ScriptableObject
{
    [Header("Body")]
    [SerializeField] public float playerHeight = 2f;
    [SerializeField] public float crouchedHeight = 1.5f;

    [Header("Health")]
    [Range(0, 1f)]
    [SerializeField] public float playerHurtAudioVolume;
    [SerializeField] public AudioClip playerHurtAudio;
    [Range(0, 1f)]
    [SerializeField] public float playerHealAudioVolume;
    [SerializeField] public AudioClip playerHealAudio;
    [SerializeField] public float invincibilityTime;

    [Range(0, 1f)]
    [SerializeField] public float playerDieAudioVolume;
    [SerializeField] public AudioClip playerDieAudio;

    [SerializeField] public float maxHealth;
    [SerializeField] public BodyPartHitTagMultiplier[] bodyPartHitTagMultipliers;

    [Header("Weapons")]
    [SerializeField] public int[] startingWeaponsIndex = new int[3];

    [Header("Game")]
    [Range(0, 1f)]
    [SerializeField] public float playerHitMarkerAudioVolume;
    [SerializeField] public AudioClip playerHitMarkerAudio;
    [SerializeField] public AudioClip playerHitMarkerSpecialAudio;

    [SerializeField] public float interactBufferTime = 0.1f;
}

