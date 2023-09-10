using UnityEngine;
using System;

[Serializable]
public struct BodyPartHitTagMultiplier
{
    public string bodyPartTag;
    public float bodyPartMultiplier;
}

[CreateAssetMenu(fileName = "ScriptablePlayer", menuName = "RUSP/ScriptablePlayer", order = 0)]
public class ScriptablePlayer : ScriptableObject
{
    [Header("Body")]
    [SerializeField] public float playerHeight = 2f;
    [SerializeField] public float crouchedHeight = 1.5f;

    [Header("Health")]
    [SerializeField] public float maxHealth;
    [SerializeField] public BodyPartHitTagMultiplier[] bodyPartHitTagMultipliers;

    [Header("Weapons")]
    [SerializeField] public Guns[] startingGuns = new Guns[3];
    [SerializeField] public int[] startingWeaponsIndex = new int[3];

    [Header("Game")]
    [SerializeField] public float respawnTime;
    [SerializeField] public float interactBufferTime = 0.1f;
}

