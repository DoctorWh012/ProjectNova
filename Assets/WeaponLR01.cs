using System.Collections;
using UnityEngine;
using DG.Tweening;
using FirstGearGames.SmoothCameraShaker;
using UnityEngine.UI;
using System.Collections.Generic;

public class WeaponLR01 : BaseWeaponRifle
{
    [Header("Gizmos")]
    [SerializeField] private bool drawGizmos;

    [Space(10)]
    [Header("Settings")]
    [Space(5)]
    [SerializeField] protected int killsNeeded;
    [SerializeField] protected float ricochetRadius;
    [SerializeField] protected int ricochetAmount;
    [SerializeField] protected float timeBetweenRicochets;

    [Header("Spin")]
    [Space(5)]
    [SerializeField] protected Transform drum;
    [SerializeField] protected float drumRotateTime;
    [SerializeField] protected Transform weaponRoot;
    [SerializeField] protected float fullRotationTakes;

    [Header("Audio")]
    [SerializeField] protected AudioSource weaponSpinAudioSource;
    [SerializeField] protected float spinSoundVolume;
    [SerializeField] protected float spinSoundMaxPitch;
    [SerializeField] protected float spinSoundPitchUpTakes;

    [Header("Components")]
    [Space(5)]
    [SerializeField] protected Image focusedOverlay;
    [SerializeField] protected Material heatMat;
    [SerializeField] protected float heatEmissionIntensity;
    [SerializeField] protected SkinnedMeshRenderer bulletsHeat;
    [SerializeField] protected Material backHeatMat;
    [SerializeField] protected float backHeatEmissionIntensity;
    [SerializeField] protected SkinnedMeshRenderer weaponMesh;

    [Header("Effects")]
    [Space(5)]
    [SerializeField] protected WeaponArmAnimation ultimateAnimation;
    [SerializeField] protected float ultimateMuzzleFlashIntensity;
    [SerializeField] protected float ultimateMuzzleFlashLightLasts;
    [SerializeField] protected TrailRenderer spinTrail;
    [SerializeField] protected float spinTrailWidht;
    [SerializeField] protected float spinTrailTime;
    [SerializeField] protected float spinTrailFadeoutTime;

    protected override void BaseStart()
    {
        base.BaseStart();
        AssignMaterials();
    }

    public override void OnWeaponPickUp()
    {
        base.OnWeaponPickUp();
    }

    public override void ActivateWeapon()
    {
        base.ActivateWeapon();
        UpdateUltIndicators();
    }

    public override void PrimaryAction(uint tick, bool compensatingForSwitch = false)
    {
        if (!CanPerformPrimaryAction(tick, compensatingForSwitch)) return;
        ShootNoSpread(tick);
        UpdateBulletHeat();
        SpinDrum();
    }

    public override void HandleServerWeaponKill(int kills, ushort victimId, uint tick)
    {
        base.HandleServerWeaponKill(kills, victimId, tick);
        UpdateUltIndicators();
    }

    public override void OnKillPerformed(ushort victimId)
    {
        base.OnKillPerformed(victimId);
        UpdateUltIndicators();
    }

    public override bool CanPerformSecondaryAction(uint tick)
    {
        if (player.playerHealth.currentPlayerState == PlayerState.Dead) return false;

        if (currentWeaponState == WeaponState.Ulting) return false;

        if (currentWeaponState == WeaponState.Switching) return false;

        if (currentWeaponState == WeaponState.Reloading) return false;

        if (killsPerformed < killsNeeded) return false;

        playerShooting.lastAltFireTick = tick;
        return true;
    }

    public override void AbortSecondaryAction()
    {
        SwitchWeaponState(WeaponState.Idle);

        spinTrail.emitting = false;
        weaponSpinAudioSource.Stop();

        weaponRoot.DOKill();
        StopAllCoroutines();

        if (!player.IsLocal) return;
        FadeFocusedOverlay(0, 0.2f);
    }

    public override void Reload()
    {
        if (currentWeaponState == WeaponState.Ulting) AbortSecondaryAction();
        base.Reload();
    }

    #region Effects
    private void AssignMaterials()
    {
        List<Material> bulletsHeatMat = new List<Material>();
        for (int i = 0; i < bulletsHeat.materials.Length; i++) bulletsHeatMat.Add(Instantiate(heatMat));
        bulletsHeat.SetMaterials(bulletsHeatMat);
        FadeAllBulletsHeat();

        Material[] weaponMeshMats = weaponMesh.materials;
        weaponMeshMats[1] = Instantiate(backHeatMat);
        weaponMesh.materials = weaponMeshMats;
        FadeBackIndicator();
    }

    private void UpdateBulletHeat()
    {
        bulletsHeat.materials[bulletsHeat.materials.Length - currentAmmo - 1].DOFloat(heatEmissionIntensity, "_EmissionIntensity", 0.2f).SetEase(Ease.OutQuad);
        print(bulletsHeat.materials.Length - currentAmmo - 1);
    }

    public void FadeAllBulletsHeat()
    {
        for (int i = 0; i < bulletsHeat.materials.Length; i++) bulletsHeat.materials[i].DOFloat(0, "_EmissionIntensity", 0.7f).SetEase(Ease.OutQuad);
    }

    private void SpinDrum()
    {
        drum.DOComplete();
        drum.DOLocalRotate(new Vector3(0, 360 * Random.Range(3, 5), 0), drumRotateTime, RotateMode.FastBeyond360).SetUpdate(UpdateType.Late).SetEase(Ease.OutQuad);
    }

    private void UpdateUltIndicators()
    {
        int kills = killsPerformed > killsNeeded ? killsNeeded : killsPerformed;
        weaponMesh.materials[1].DOComplete();
        weaponMesh.materials[1].DOFloat(((float)kills / (float)killsNeeded) * backHeatEmissionIntensity * (kills == killsNeeded ? 4 : 1), "_EmissionIntensity", 0.2f).SetEase(Ease.OutQuad);

        if (!player.IsLocal) return;
        ultimateSlider.value = (float)killsPerformed / killsNeeded;

        ultimateIconImg.material = kills == killsNeeded ? playerShooting.ultGlowMat : null;
        ultimateIconImg.color = kills == killsNeeded ? Color.white : playerShooting.fadedUltColor;
    }

    private void FadeBackIndicator()
    {
        weaponMesh.materials[1].DOFloat(0, "_EmissionIntensity", 0.2f).SetEase(Ease.OutQuad);
    }

    private void FadeFocusedOverlay(float value, float time)
    {
        focusedOverlay.DOComplete();
        focusedOverlay.DOFade(value, time);
    }
    #endregion

    #region Ultimate
    public override void SecondaryAction(uint tick)
    {
        if (!CanPerformSecondaryAction(tick)) return;
        StartCoroutine(UltimatePayback());
    }

    protected void UltStartEffects()
    {
        spinTrail.DOComplete();
        spinTrail.time = spinTrailTime;
        spinTrail.emitting = true;
        spinTrail.startWidth = spinTrailWidht;

        weaponSpinAudioSource.DOComplete();
        weaponSpinAudioSource.pitch = 1;
        weaponSpinAudioSource.volume = spinSoundVolume;
        weaponSpinAudioSource.Play();
        weaponSpinAudioSource.DOPitch(spinSoundMaxPitch, spinSoundPitchUpTakes).SetEase(Ease.InCubic);

        WeaponArmAnimation(ultimateAnimation);

        weaponRoot.DOLocalRotate(new Vector3(-360, 0, 0), fullRotationTakes, RotateMode.LocalAxisAdd).SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart).SetUpdate(UpdateType.Late);

        if (!player.IsLocal) return;
        FadeFocusedOverlay(1, 0.3f);
    }

    protected void UltEndEffects()
    {
        spinTrail.DOTime(0, spinTrailFadeoutTime);
        spinTrail.DOResize(0.01f, 0, spinTrailFadeoutTime).OnComplete(() => spinTrail.emitting = false);

        weaponSpinAudioSource.DOFade(0, 0.2f).SetEase(Ease.InCubic).OnComplete(() => weaponSpinAudioSource.Stop());

        weaponRoot.DOKill();

        muzzleFlash.Play();

        if (muzzleFlashLight)
        {
            muzzleFlashLight.intensity = ultimateMuzzleFlashIntensity;
            muzzleFlashLight.enabled = true;
            muzzleFlashLight.DOIntensity(0, ultimateMuzzleFlashLightLasts).SetEase(Ease.InOutQuad).OnComplete(() => muzzleFlashLight.enabled = false);
        }

        if (shootAnimations.Length != 0) WeaponArmAnimation(shootAnimations[Random.Range(0, shootAnimations.Length)]);

        if (weaponSounds.Length != 0)
        {
            weaponAudioSource.pitch = Utilities.GetRandomPitch(-0.1f, 0.02f);
            weaponAudioSource.PlayOneShot(weaponSounds[Random.Range(0, weaponSounds.Length)], weaponSoundVolume);
        }

        if (player.IsLocal)
        {
            if (screenShakeData) CameraShakerHandler.ShakeAll(screenShakeData);
            playerHud.ScaleCrosshairShot();
        }

        if (!player.IsLocal) return;
        FadeFocusedOverlay(0, 0.2f);
    }

    protected void RicochetTracer(Vector3 from, Vector3 to)
    {
        // Get The Tracer From Pool
        tracer = PoolingManager.Singleton.GetBulletTracer(tracerType);

        // Configures Tracer
        tracer.time = tracerLasts;
        tracer.transform.DOComplete();
        tracer.transform.gameObject.layer = playerShooting.netPlayerLayer;
        tracer.transform.position = from;
        tracer.Clear();

        tracer.transform.DOMove(to, tracerLasts).SetEase(Ease.Linear);
        tracer.DOResize(tracerWidth, 0, 0.5f);

        // Returns Tracer To Pool After It's Used
        tracer.GetComponent<ReturnToPool>().ReturnToPoolIn(tracerLasts);
    }

    protected IEnumerator UltimatePayback()
    {
        // Ult Prep
        SwitchWeaponState(WeaponState.Ulting);

        UltStartEffects();

        if (NetworkManager.Singleton.Server.IsRunning) playerShooting.SendServerAltFire();
        else if (player.IsLocal) playerShooting.SendClientAltFire();

        while (CheckAltFireConfirmation())
        {
            yield return null;
        }

        // Ult Confirm
        playerShooting.lastAltFireConfirmationTick = NetworkManager.Singleton.serverTick;

        if (NetworkManager.Singleton.Server.IsRunning) playerShooting.SendServerAltFireConfirmation();
        else if (player.IsLocal) playerShooting.SendClientAltFireConfirmation();

        killsPerformed = 0;

        UltEndEffects();

        // Ult Shot
        if (!player.IsLocal && NetworkManager.Singleton.Server.IsRunning)
        {
            for (uint i = playerShooting.lastAltFireConfirmationTick - (uint)NetworkManager.overcompensationAmount; i < playerShooting.lastShotTick + NetworkManager.overcompensationAmount + 1; i++)
            {
                NetworkManager.Singleton.SetAllPlayersPositionsTo(i, player.Id);

                shotRayHit = FilteredRaycast(playerShooting.playerCam.forward, 0.2f);
                if (shotRayHit.collider && CheckPlayerHit(shotRayHit.collider)) break;
            }
            NetworkManager.Singleton.ResetPlayersPositions(player.Id);
        }
        else shotRayHit = FilteredRaycast(playerShooting.playerCam.forward, 0.2f);
        ShootingTracer(shotRayHit.collider, shotRayHit.point);

        // If it's a player damages it
        if (shotRayHit.collider && CheckPlayerHit(shotRayHit.collider))
        {
            GetHitPlayer(shotRayHit.collider.gameObject, 999);

            Collider[] playersOnRadius = PlayersOverlapSphere(shotRayHit.point, ricochetRadius * ricochetAmount);
            System.Array.Sort(playersOnRadius, (x, y) => Vector3.Distance(x.transform.position, shotRayHit.point).CompareTo(Vector3.Distance(y.transform.position, shotRayHit.point)));

            foreach (Collider col in playersOnRadius) print($"<color=red> Player {col.transform.root.name} is in range at {Vector3.Distance(col.transform.position, shotRayHit.point)}</color>");

            Vector3 lastRicochetPos = shotRayHit.point;
            for (int i = 0; i < playersOnRadius.Length; i++)
            {
                yield return new WaitForSeconds(timeBetweenRicochets);
                if (Vector3.Distance(playersOnRadius[i].transform.position, lastRicochetPos) > ricochetRadius) break;
                if (playersOnRadius[i].transform.root == playerShooting.gameObject.transform) continue;
                if (FilteredObstacleCheck(lastRicochetPos, playersOnRadius[i].transform.position)) break;

                GetHitPlayer(playersOnRadius[i].gameObject, 999);
                RicochetTracer(lastRicochetPos, playersOnRadius[i].transform.position);
                lastRicochetPos = playersOnRadius[i].transform.position;
            }
        }
        else HitParticle(shotRayHit.point);

        SwitchWeaponState(WeaponState.Idle);
    }

    #endregion 

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.root.position, ricochetRadius * ricochetAmount);
    }
}
