using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WeaponLS50 : BaseWeaponRifle
{
    [Header("Ability")]
    [Header("Components")]
    [SerializeField] protected PlayerCam playerCam;
    [SerializeField] protected GameObject scopeOverlay;
    [SerializeField] protected Slider shotPowerSlider;
    [SerializeField] protected TextMeshProUGUI shotPowerTxt;

    [Header("Settings")]
    [SerializeField, Range(1, 120)] protected int aimedInFov;
    [SerializeField] float timeBetweenAimIn;

    [SerializeField] protected float damageBoostTime;
    [SerializeField] protected float maxAimedInDamageMultiplier;
    [SerializeField] protected float minAimedInDamageMultiplier;

    protected float powerShotMultiplier;

    public override void PrimaryAction(uint tick, bool compensatingForSwitch = false)
    {
        if (!ShootNoSpread(tick, compensatingForSwitch)) return;
    }

    public override void SecondaryAction(uint tick)
    {
        StartCoroutine(AbilityScope());
    }

    #region Ability
    protected IEnumerator AbilityScope()
    {
        SwitchWeaponState(WeaponState.Ulting);

        playerCam.AlterZoomMode(true, aimedInFov);
        scopeOverlay.SetActive(true);

        while (CheckAltFireConfirmation())
        {
            yield return null;
        }

        playerCam.AlterZoomMode(false);
        scopeOverlay.SetActive(false);
        SwitchWeaponState(WeaponState.Idle);
    }
    #endregion
}
