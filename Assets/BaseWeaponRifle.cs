using UnityEngine;
using DG.Tweening;
using FirstGearGames.SmoothCameraShaker;

public class BaseWeaponRifle : BaseWeapon
{
    [Header("Components")]
    [SerializeField] protected ParticleSystem muzzleFlash;

    [Header("Tracer")]
    [SerializeField] protected Transform barrelTip;
    [SerializeField] public TracerType tracerType;
    [SerializeField] public float tracerLasts;
    [SerializeField] public float tracerWidth;

    protected TrailRenderer tracer;
    protected ParticleSystem hitParticle;
    protected RaycastHit shotRayHit;

    protected void Start()
    {
        BaseStart();
    }

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

    public override void PrimaryAction(uint tick, bool compensatingForSwitch = false)
    {
        ShootNoSpread(tick, compensatingForSwitch);
    }

    protected bool ShootNoSpread(uint tick, bool compensatingForSwitch)
    {
        if (!CanPerformPrimaryAction(tick, compensatingForSwitch)) return false;
        currentAmmo--;
        SwitchWeaponState(WeaponState.Shooting);

        // Effects
        muzzleFlash.Play();
        animator.Play("Recoil", 0, 0);
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

        ShootingTracer(shotRayHit.collider);

        // If it's a player damages it
        if (shotRayHit.collider && CheckPlayerHit(shotRayHit.collider)) GetHitPlayer(shotRayHit.collider.gameObject, damage);
        else HitParticle();
        ApplyKnockback();

        if (NetworkManager.Singleton.Server.IsRunning) playerShooting.SendServerFire();
        else if (player.IsLocal) playerShooting.SendClientFire();

        return true;
    }

    protected void ShootingTracer(bool didHit)
    {
        // Get The Tracer From Pool
        tracer = PoolingManager.Singleton.GetBulletTracer(tracerType);

        // Configures Tracer
        tracer.time = tracerLasts;
        tracer.transform.DOComplete();
        tracer.transform.gameObject.layer = player.IsLocal ? playerShooting.playerLayer : playerShooting.netPlayerLayer;
        tracer.transform.position = barrelTip.position;
        tracer.Clear();

        Vector3 endPos = didHit ? shotRayHit.point : playerShooting.playerCam.forward * range + barrelTip.position;

        tracer.transform.DOMove(endPos, tracerLasts).SetEase(Ease.Linear);
        tracer.DOResize(tracerWidth, 0, 0.5f);

        // Returns Tracer To Pool After It's Used
        tracer.GetComponent<ReturnToPool>().ReturnToPoolIn(tracerLasts);
    }

    protected void HitParticle()
    {
        hitParticle = PoolingManager.Singleton.GetHitParticle(tracerType);

        hitParticle.transform.position = shotRayHit.point;
        hitParticle.Play();

        hitParticle.GetComponent<ReturnToPool>().ReturnToPoolIn(hitParticle.main.duration);
    }
}
