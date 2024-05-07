using UnityEngine;

public class WeaponLW50 : BaseWeaponRifle
{
    public override void PrimaryAction(uint tick, bool compensatingForSwitch = false)
    {
        if (!ShootNoSpread(tick, compensatingForSwitch)) return;
    }
}
