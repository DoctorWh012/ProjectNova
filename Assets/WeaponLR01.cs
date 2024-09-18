using System;
using System.Collections;
using UnityEngine;
using DG.Tweening;
using FirstGearGames.SmoothCameraShaker;
using UnityEngine.UI;

public class WeaponLR01 : BaseWeaponRifle
{
    [Serializable]
    protected struct UltimateGrip
    {
        public Transform grip;
        public Transform pole;

        public Vector3 startPos;
        public Vector3 endPos;

        public float time;
    }

    [Header("Gizmos")]
    [SerializeField] private bool drawGizmos;

    [Header("LR-01")]
    [SerializeField] protected Transform rotatable;

    [Header("Ultimate")]
    [Header("Settings")]
    [Space(5)]
    [SerializeField] protected int killsNeeded;
    [SerializeField] protected float ricochetRadius;
    [SerializeField] protected int ricochetAmount;
    [SerializeField] protected float timeBetweenRicochets;
    [SerializeField] protected float weaponSpinSpoolTime;

    [Header("Grip")]
    [Space(5)]
    [SerializeField] protected string ultimateHoldAnimation;
    [SerializeField] protected UltimateGrip lGrip;

    [Header("Components")]
    [Space(5)]
    [SerializeField] protected ParticleSystem ultChargeParticle;
    [SerializeField] protected Image focusedOverlay;

    [Header("Effects")]
    [SerializeField] protected Material glowMat;
    [SerializeField] protected Material fadedMat;
    [SerializeField] protected MeshRenderer[] bullets;
    [SerializeField] protected MeshRenderer backIndicator;

    public override void OnWeaponPickUp()
    {
        base.OnWeaponPickUp();
    }

    public override void ActivateWeapon()
    {
        base.ActivateWeapon();
        UpdateUltIndicators();
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
        ultChargeParticle.Stop();
        SetupWeaponGrip();
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
    private void UpdateUltIndicators()
    {
        int kills = killsPerformed > 6 ? 6 : killsPerformed;

        for (int i = 0; i < bullets.Length; i++) bullets[i].material = killsPerformed > i ? glowMat : fadedMat;
        backIndicator.material = kills == 6 ? glowMat : fadedMat;

        if (!player.IsLocal) return;
        ultimateSlider.value = (float)killsPerformed / killsNeeded;

        ultimateIconImg.material = kills == killsNeeded ? playerShooting.ultGlowMat : null;
        ultimateIconImg.color = kills == killsNeeded ? Color.white : playerShooting.fadedUltColor;
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
        ultChargeParticle.Play();

        SetupUltimateGrip();

        lGrip.grip.localPosition = lGrip.startPos;
        lGrip.grip.DOLocalMove(lGrip.endPos, lGrip.time).SetEase(Ease.InOutSine);

        if (!player.IsLocal) return;
        FadeFocusedOverlay(1, 0.3f);
    }

    protected void UltEndEffects()
    {
        muzzleFlash.Play();
        animator.Play("Recoil", 0, 0);

        SetupWeaponGrip();

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

        ultChargeParticle.Stop();

        if (!player.IsLocal) return;
        FadeFocusedOverlay(0, 0.2f);
        focusedOverlay.DOComplete();
        focusedOverlay.DOFade(0, 0.2f);
    }

    protected void SetupUltimateGrip()
    {
        playerShooting.leftArm.handIK.enabled = lGrip.grip;
        // playerShooting.rightArm.handIK.enabled = rightHandTarget;

        if (lGrip.grip) playerShooting.leftArm.handIK.Target = lGrip.grip;
        if (lGrip.pole) playerShooting.leftArm.handIK.Pole = lGrip.pole;

        // if (rightHandTarget) playerShooting.rightArm.handIK.Target = rightHandTarget;

        if (!string.IsNullOrEmpty(ultimateHoldAnimation))
        {
            if (player.IsLocal) playerShooting.armsAnimator.Play(ultimateHoldAnimation, 0, 0);
            else playerShooting.armsAnimator.Play(ultimateHoldAnimation, 1, 0);
        }

        if (player.IsLocal && SettingsManager.playerPreferences.renderArms) playerShooting.EnableDisableHandsMeshes(lGrip.grip, rightHandTarget);

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

                shotRayHit = FilteredRaycast(playerShooting.playerCam.forward);
                if (shotRayHit.collider && CheckPlayerHit(shotRayHit.collider)) break;
            }
            NetworkManager.Singleton.ResetPlayersPositions(player.Id);
        }
        else shotRayHit = FilteredRaycast(playerShooting.playerCam.forward);
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
