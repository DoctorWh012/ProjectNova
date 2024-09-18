using UnityEngine;
using DG.Tweening;
using FirstGearGames.SmoothCameraShaker;

public class BaseWeaponShotgun : BaseWeapon
{
    [Header("Shotgun")]
    [SerializeField] public float spread;
    [SerializeField] protected int pellets;

    [Header("Components")]
    [SerializeField] protected ParticleSystem muzzleFlash;

    [Header("Tracer")]
    [SerializeField] protected Transform barrelTip;
    [SerializeField] public TracerType tracerType;
    [SerializeField] public float tracerLasts;
    [SerializeField] public float tracerWidth;

    protected float individualPelletDamage;
    protected Vector3 dirSpread;
    protected Vector2[] spreadPatterns;
    protected TrailRenderer tracer;
    protected ParticleSystem hitParticle;
    protected RaycastHit shotRayHit;

    protected void Start()
    {
        BaseStart();
        spreadPatterns = new Vector2[pellets];
        individualPelletDamage = damage / pellets;
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
        ShootWithVariableSpread(tick, compensatingForSwitch);
    }

    protected bool ShootWithVariableSpread(uint tick, bool compensatingForSwitch)
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
        GenerateSpreadPattern((int)(playerShooting.lastShotTick % int.MaxValue));

        if (!player.IsLocal && NetworkManager.Singleton.Server.IsRunning) NetworkManager.Singleton.SetAllPlayersPositionsTo(playerShooting.lastShotTick, player.Id);

        for (int i = 0; i < spreadPatterns.Length; i++)
        {
            dirSpread = playerShooting.playerCam.forward + Quaternion.LookRotation(playerShooting.playerCam.forward) * spreadPatterns[i] * spread;
            shotRayHit = FilteredRaycast(dirSpread);
            ShootingTracer(dirSpread, shotRayHit.collider);

            // If it's a player damages it
            if (shotRayHit.collider && CheckPlayerHit(shotRayHit.collider)) GetHitPlayer(shotRayHit.collider.gameObject, individualPelletDamage);
            else HitParticle();
        }

        if (!player.IsLocal && NetworkManager.Singleton.Server.IsRunning) NetworkManager.Singleton.ResetPlayersPositions(player.Id);

        ApplyKnockback();

        if (NetworkManager.Singleton.Server.IsRunning) playerShooting.SendServerFire();
        else if (player.IsLocal) playerShooting.SendClientFire();

        return true;
    }

    protected void ShootingTracer(Vector3 dir, bool didHit)
    {
        // Get The Tracer From Pool
        tracer = PoolingManager.Singleton.GetBulletTracer(tracerType);

        // Configures Tracer
        tracer.time = tracerLasts;
        tracer.transform.DOComplete();
        tracer.transform.gameObject.layer = player.IsLocal ? playerShooting.playerLayer : playerShooting.netPlayerLayer;
        tracer.transform.position = barrelTip.position;
        tracer.Clear();

        Vector3 endPos = didHit ? shotRayHit.point : dir * range + barrelTip.position;

        tracer.transform.DOMove(endPos, tracerLasts).SetEase(Ease.Linear);
        tracer.DOResize(tracerWidth, 0, 0.5f);

        // Returns Tracer To Pool After It's Used
        tracer.GetComponent<ReturnToPool>().ReturnToPoolIn(tracerLasts);

        if (player.IsLocal && NetworkManager.Singleton.Server.IsRunning) return;
    }

    protected void HitParticle()
    {
        hitParticle = PoolingManager.Singleton.GetHitParticle(tracerType);

        hitParticle.transform.position = shotRayHit.point;
        hitParticle.Play();

        hitParticle.GetComponent<ReturnToPool>().ReturnToPoolIn(hitParticle.main.duration);
    }

    protected void GenerateSpreadPattern(int seed)
    {
        GaussianDistribution gaussianDistribution = new GaussianDistribution();
        Random.InitState(seed);
        for (int i = 0; i < pellets; i++) spreadPatterns[i] = new Vector2(gaussianDistribution.Next(0f, 1, -1, 1), gaussianDistribution.Next(0f, 1, -1, 1));
    }
}
