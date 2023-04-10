using UnityEngine;

[CreateAssetMenu(fileName = "PlayerHealthSettings", menuName = "RUSP/PlayerHealthSettings", order = 0)]
public class PlayerHealthSettings : ScriptableObject
{
    [SerializeField] public int maxHealth;
    [SerializeField] public float respawnTime;
}