using UnityEngine;

public class WeaponLL30 : BaseWeaponRifle
{
    public override void PrimaryAction(uint tick, bool compensatingForSwitch = false)
    {
        if (!ShootNoSpread(tick, compensatingForSwitch)) return;
    }
}