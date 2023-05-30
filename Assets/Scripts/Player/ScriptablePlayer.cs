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
    [Header("Health")]
    public float maxHealth;
    public BodyPartHitTagMultiplier[] bodyPartHitTagMultipliers;

    [Header("Game")]
    public float respawnTime;
}

