using UnityEngine;

public class WeaponLS50 : BaseWeaponRifle
{
    // [Header("Ability")]
    // [Header("Settings")]
    // [SerializeField, Range(1, 120)] protected int aimedInFov;
    // [SerializeField] float timeBetweenAimIn;
    // [SerializeField] protected float maxAimedInDamageMultiplier;
    // [SerializeField] protected float minAimedInDamageMultiplier;

    public override void PrimaryAction(uint tick, bool compensatingForSwitch = false)
    {
        if (!ShootNoSpread(tick, compensatingForSwitch)) return;
    }

    // public override bool CanPerformSecondaryAction(uint tick)
    // {
    // }

    // public override void SecondaryAction(uint tick)
    // {
    // }
}
