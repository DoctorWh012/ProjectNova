using UnityEngine;

public class WeaponLA99 : BaseWeaponRifle
{
    public override void PrimaryAction(uint tick, bool compensatingForSwitch = false)
    {
        if (!ShootNoSpread(tick, compensatingForSwitch)) return;
    }
}
