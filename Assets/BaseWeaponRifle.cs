using UnityEngine;
using DG.Tweening;
using FirstGearGames.SmoothCameraShaker;

public class BaseWeaponRifle : BaseWeapon
{
    [Header("Components")]
    [Space(5)]
    [SerializeField] protected ParticleSystem muzzleFlash;
    [SerializeField] protected Light muzzleFlashLight;

    [Header("Settings")]
    [Space(5)]
    [SerializeField] protected float muzzleFlashLightIntensity;
    [SerializeField] protected float muzzleFlashLightLasts;

    [Header("Tracer")]
    [Space(5)]
    [SerializeField] protected Transform barrelTip;
    [SerializeField] public TracerType tracerType;
    [SerializeField] public float tracerLasts;
    [SerializeField] public float tracerWidth;

    protected TrailRenderer tracer;
    protected ParticleSystem hitParticle;
    protected RaycastHit shotRayHit;

    public override bool CanPerformPrimaryAction(uint tick, bool compensatingForSwitch)
    {
        if (player.playerHealth.currentPlayerState == PlayerState.Dead) return false;

        if (currentAmmo <= 0) return false;

        if (tick - tickFireRate < playerShooting.lastShotTick) return false;

        if (currentWeaponState == WeaponState.Ulting) return false;

        if (!compensatingForSwitch && currentWeaponState == WeaponState.Switching) return false;

        if (currentWeaponState == WeaponState.Reloading) return false;

        playerShooting.lastShotTick = tick;
        return true;
    }

    public override bool PrimaryAction(uint tick, bool compensatingForSwitch = false)
    {
        if (!CanPerformPrimaryAction(tick, compensatingForSwitch)) return false;
        ShootNoSpread(tick);
        return true;
    }

    protected bool ShootNoSpread(uint tick)
    {
        currentAmmo--;
        SwitchWeaponState(WeaponState.Shooting);

        // Effects
        muzzleFlash.Play();
        if (muzzleFlashLight)
        {
            muzzleFlashLight.intensity = muzzleFlashLightIntensity;
            muzzleFlashLight.enabled = true;
            muzzleFlashLight.DOIntensity(0, muzzleFlashLightLasts).SetEase(Ease.InOutQuad).OnComplete(() => muzzleFlashLight.enabled = false);
        }

        if (shootAnimations.Length != 0) WeaponArmAnimation(shootAnimations[Random.Range(0, shootAnimations.Length)]);
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
        if (!player.IsLocal && NetworkManager.Singleton.Server.IsRunning)
        {
            for (uint i = playerShooting.lastShotTick - (uint)NetworkManager.overcompensationAmount; i < playerShooting.lastShotTick + NetworkManager.overcompensationAmount + 1; i++)
            {
                NetworkManager.Singleton.SetAllPlayersPositionsTo(i, player.Id);

                shotRayHit = FilteredRaycast(playerShooting.playerCam.forward);
                if (shotRayHit.collider && CheckPlayerHit(shotRayHit.collider)) break;
            }
            NetworkManager.Singleton.ResetPlayersPositions(player.Id);
        }
        else shotRayHit = FilteredRaycast(playerShooting.playerCam.forward);

        ShootingTracer(shotRayHit.collider, shotRayHit.point);

        // If it's a player damages it
        if (shotRayHit.collider && CheckPlayerHit(shotRayHit.collider)) GetHitPlayer(shotRayHit.collider.gameObject, damage);
        else HitParticle(shotRayHit.point);
        ApplyKnockback();

        if (NetworkManager.Singleton.Server.IsRunning) playerShooting.SendServerFire();
        else if (player.IsLocal) playerShooting.SendClientFire();

        return true;
    }

    protected void ShootingTracer(bool didHit, Vector3 pos)
    {
        // Get The Tracer From Pool
        tracer = PoolingManager.Singleton.GetBulletTracer(tracerType);

        // Configures Tracer
        tracer.time = tracerLasts;
        tracer.transform.DOComplete();
        tracer.transform.gameObject.layer = player.IsLocal ? playerShooting.playerLayer : playerShooting.netPlayerLayer;
        tracer.transform.position = barrelTip.position;
        tracer.Clear();

        Vector3 endPos = didHit ? pos : playerShooting.playerCam.forward * range + barrelTip.position;

        tracer.transform.DOMove(endPos, tracerLasts).SetEase(Ease.Linear);
        tracer.DOResize(tracerWidth, 0, 0.5f);

        // Returns Tracer To Pool After It's Used
        tracer.GetComponent<ReturnToPool>().ReturnToPoolIn(tracerLasts);
    }

    protected void HitParticle(Vector3 pos)
    {
        hitParticle = PoolingManager.Singleton.GetHitParticle(tracerType);

        hitParticle.transform.position = pos;
        hitParticle.Play();

        hitParticle.GetComponent<ReturnToPool>().ReturnToPoolIn(1);
    }
}
