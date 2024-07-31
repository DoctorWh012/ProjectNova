using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FirstGearGames.SmoothCameraShaker;
using System.Collections.Generic;

public class WeaponLS50 : BaseWeaponRifle
{
    [Header("Ability")]
    [Header("Components")]
    [SerializeField] protected PlayerCam playerCam;
    [SerializeField] protected GameObject scopeOverlay;
    [SerializeField] protected Image shotPowerImg;
    [SerializeField] protected TextMeshProUGUI shotPowerTxt;

    [Header("Settings")]
    [SerializeField, Range(1, 120)] protected int aimedInFov;
    [SerializeField] protected int piercingAmount;

    [SerializeField] protected float maxQuickscopeMultiplier;
    [SerializeField] protected float minQuickscopeMultiplier;

    [SerializeField] protected float quickscopeUseTime;
    [SerializeField] protected float quickscopeRefillTime;

    [SerializeField] protected float quickscopeMultiplier;

    [SerializeField] protected float quickscopeMultiplierOnAim;
    [SerializeField] protected float quickscopeMultiplierOnLeaveAim;

    [SerializeField] protected float quickscopeUsePerTick;
    [SerializeField] protected float quickscopeRefillPerTick;

    new protected void Start()
    {
        BaseStart();
        float deltaMultiplier = (maxQuickscopeMultiplier - minQuickscopeMultiplier) / (1f / Time.fixedDeltaTime);
        quickscopeUsePerTick = deltaMultiplier / quickscopeUseTime;
        quickscopeRefillPerTick = deltaMultiplier / quickscopeRefillTime;
    }

    private void FixedUpdate()
    {
        AlterQuickscopeMultiplierValue();
    }

    public override void OnWeaponPickUp()
    {
        base.OnWeaponPickUp();
        quickscopeMultiplier = maxQuickscopeMultiplier;
        quickscopeMultiplierOnAim = quickscopeMultiplier;
        quickscopeMultiplierOnLeaveAim = quickscopeMultiplier;
    }

    public override void ActivateWeapon()
    {
        base.ActivateWeapon();
    }

    public override void PrimaryAction(uint tick, bool compensatingForSwitch = false)
    {
        if (currentWeaponState != WeaponState.Ulting) base.PrimaryAction(tick, compensatingForSwitch);
        else
        {
            if (!CanPerformQuickscopeShot(tick, compensatingForSwitch)) return;
            QuickscopeShootNoSpreadPiercing(tick);
        }
    }

    protected bool CanPerformQuickscopeShot(uint tick, bool compensatingForSwitch)
    {
        if (player.playerHealth.currentPlayerState == PlayerState.Dead) return false;

        if (currentAmmo <= 0) return false;

        if (tick - tickFireRate < playerShooting.lastShotTick) return false;

        if (!compensatingForSwitch && currentWeaponState == WeaponState.Switching) return false;

        if (currentWeaponState == WeaponState.Reloading) return false;

        playerShooting.lastShotTick = tick;
        return true;
    }

    public override bool CanPerformSecondaryAction(uint tick)
    {
        if (player.playerHealth.currentPlayerState == PlayerState.Dead) return false;

        if (currentWeaponState == WeaponState.Ulting) return false;

        if (currentWeaponState == WeaponState.Switching) return false;

        if (currentWeaponState == WeaponState.Reloading) return false;

        playerShooting.lastAltFireTick = tick;
        return true;
    }

    public override void AbortSecondaryAction()
    {
        SwitchWeaponState(WeaponState.Idle);
        AbilityEndEffects();
        StopAllCoroutines();
    }

    public override void SecondaryAction(uint tick)
    {
        if (!CanPerformSecondaryAction(tick)) return;
        StartCoroutine(AbilityScope());
    }

    public override void Reload()
    {
        if (currentWeaponState == WeaponState.Ulting) AbortSecondaryAction();
        base.Reload();
    }

    #region Ability
    protected void AbilityStartEffects()
    {
        if (!player.IsLocal) return;
        playerCam.AlterZoomMode(true, aimedInFov);
        scopeOverlay.SetActive(true);
    }

    protected void AbilityEndEffects()
    {
        if (!player.IsLocal) return;
        playerCam.AlterZoomMode(false);
        scopeOverlay.SetActive(false);
    }

    protected IEnumerator AbilityScope()
    {
        SwitchWeaponState(WeaponState.Ulting);

        AbilityStartEffects();

        quickscopeMultiplierOnAim = quickscopeMultiplier;

        if (NetworkManager.Singleton.Server.IsRunning) playerShooting.SendServerAltFire();
        else if (player.IsLocal) playerShooting.SendClientAltFire();

        while (CheckAltFireConfirmation())
        {
            yield return null;
        }

        playerShooting.lastAltFireConfirmationTick = NetworkManager.Singleton.serverTick;
        if (NetworkManager.Singleton.Server.IsRunning) playerShooting.SendServerAltFireConfirmation();
        else if (player.IsLocal) playerShooting.SendClientAltFireConfirmation();
        quickscopeMultiplierOnLeaveAim = quickscopeMultiplier;

        if (player.IsLocal) AbilityEndEffects();

        SwitchWeaponState(WeaponState.Idle);
    }

    protected void AlterQuickscopeMultiplierValue()
    {
        if (currentWeaponState == WeaponState.Ulting)
        {
            uint tickDif = NetworkManager.Singleton.serverTick - playerShooting.lastAltFireTick;
            float result = quickscopeMultiplierOnAim - quickscopeUsePerTick * tickDif;
            quickscopeMultiplier = result > minQuickscopeMultiplier ? result : minQuickscopeMultiplier;
        }

        else
        {
            uint tickDif = NetworkManager.Singleton.serverTick - playerShooting.lastAltFireConfirmationTick;
            float result = quickscopeMultiplierOnLeaveAim + quickscopeRefillPerTick * tickDif;
            quickscopeMultiplier = result < maxQuickscopeMultiplier ? result : maxQuickscopeMultiplier;
        }

        if (!player.IsLocal) return;
        float shotPowerPercentage = (quickscopeMultiplier - minQuickscopeMultiplier) / (maxQuickscopeMultiplier - minQuickscopeMultiplier);
        ultimateSlider.value = shotPowerPercentage;
        shotPowerImg.fillAmount = shotPowerPercentage;
        shotPowerTxt.SetText(quickscopeMultiplier.ToString("#%"));
    }

    protected bool QuickscopeShootNoSpreadPiercing(uint tick)
    {
        currentAmmo--;

        // Effects
        muzzleFlash.Play();
        animator.Play("Recoil", 0, 0);
        Invoke(nameof(FinishPrimaryActionWhileScope), fireTime);

        if (weaponSounds.Length != 0)
        {
            weaponAudioSource.pitch = Utilities.GetRandomPitch(-0.1f, 0.02f);
            weaponAudioSource.PlayOneShot(weaponSounds[UnityEngine.Random.Range(0, weaponSounds.Length)], weaponSoundVolume);
        }

        if (player.IsLocal)
        {
            if (screenShakeData) CameraShakerHandler.ShakeAll(screenShakeData);
            playerHud.ScaleCrosshairShot();
        }

        // Shooting Logic
        List<RaycastHit> shotRayHits = new List<RaycastHit>();

        if (!player.IsLocal && NetworkManager.Singleton.Server.IsRunning)
        {
            for (uint i = playerShooting.lastShotTick - (uint)NetworkManager.overcompensationAmount; i < playerShooting.lastShotTick + NetworkManager.overcompensationAmount + 1; i++)
            {
                NetworkManager.Singleton.SetAllPlayersPositionsTo(i, player.Id);

                shotRayHits = FilteredRaycastPierced(playerShooting.playerCam.forward, piercingAmount);

                if (CheckIfPiercedShotHitPlayer(shotRayHits)) break;
            }
            NetworkManager.Singleton.ResetPlayersPositions(player.Id);
        }
        else shotRayHits = FilteredRaycastPierced(playerShooting.playerCam.forward, piercingAmount);

        foreach (RaycastHit col in shotRayHits) print(col.collider.gameObject.name);

        for (int i = 0; i < shotRayHits.Count; i++)
        {
            // If it's a player damages it
            if (CheckPlayerHit(shotRayHits[i].collider)) GetHitPlayer(shotRayHits[i].collider.gameObject, damage * quickscopeMultiplier);
            else HitParticle(shotRayHits[i].point);
        }
        ShootingTracer(shotRayHits[shotRayHits.Count - 1].collider, shotRayHits[shotRayHits.Count - 1].point);

        ApplyKnockback();

        if (NetworkManager.Singleton.Server.IsRunning) playerShooting.SendServerFire();
        else if (player.IsLocal) playerShooting.SendClientFire();

        return true;
    }

    protected bool CheckIfPiercedShotHitPlayer(List<RaycastHit> hits)
    {
        for (int i = 0; i < hits.Count; i++) if (CheckPlayerHit(hits[i].collider)) return true;
        return false;
    }

    protected void FinishPrimaryActionWhileScope()
    {
        if (currentWeaponState != WeaponState.Ulting) return;
        CheckIfReloadIsNeeded();
    }
    #endregion
}
