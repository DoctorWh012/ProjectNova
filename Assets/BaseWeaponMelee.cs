using UnityEngine;
using FirstGearGames.SmoothCameraShaker;
using System.Collections.Generic;

public class BaseWeaponMelee : BaseWeapon
{
    [Header("Settings")]
    [SerializeField] private float attackRadius;

    [Header("Effects")]
    [SerializeField] private string[] primaryActionAnimations;

    void Start()
    {
        BaseStart();
    }

    public override void DeactivateWeapon()
    {
        gameObject.SetActive(false);
        CancelInvoke(nameof(FinishSwitching));
    }

    public override bool CanPerformPrimaryAction(uint tick, bool compensatingForSwitch)
    {
        if (player.playerHealth.currentPlayerState == PlayerState.Dead) return false;

        if (tick - tickFireRate < playerShooting.lastShotTick) return false;

        if (!compensatingForSwitch && currentWeaponState == WeaponState.Switching) return false;

        if (currentWeaponState == WeaponState.Reloading) return false;

        playerShooting.lastShotTick = tick;
        return true;
    }

    public override void PrimaryAction(uint tick, bool compensatingForSwitch = false)
    {
        MeleeAttack(tick, compensatingForSwitch);
    }

    public override void CheckIfReloadIsNeeded()
    {
    }

    protected bool MeleeAttack(uint tick, bool compensatingForSwitch)
    {
        if (!CanPerformPrimaryAction(tick, compensatingForSwitch)) return false;
        SwitchWeaponState(WeaponState.Shooting);

        // Effects
        animator.Play(primaryActionAnimations[Random.Range(0, primaryActionAnimations.Length)], 0, 0);
        Invoke(nameof(FinishPrimaryAction), fireTime);

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
        List<Collider> filteredCol = new List<Collider>();
        if (!player.IsLocal && NetworkManager.Singleton.Server.IsRunning) NetworkManager.Singleton.SetAllPlayersPositionsTo(playerShooting.lastShotTick, player.Id);

        filteredCol = FilteredOverlapSphere(playerShooting.playerCam.position + playerShooting.playerCam.forward * attackRadius, attackRadius);
        for (int j = 0; j < filteredCol.Count; j++) if (CheckPlayerHit(filteredCol[j])) GetHitPlayer(filteredCol[j].gameObject, damage);
        
        if (!player.IsLocal && NetworkManager.Singleton.Server.IsRunning) NetworkManager.Singleton.ResetPlayersPositions(player.Id);

        ApplyKnockback();

        if (NetworkManager.Singleton.Server.IsRunning) playerShooting.SendServerFire();
        else if (player.IsLocal) playerShooting.SendClientFire();

        return true;
    }

    private void OnDrawGizmos()
    {
        if (!playerShooting) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(playerShooting.playerCam.position + playerShooting.playerCam.forward * attackRadius, attackRadius);
    }
}
