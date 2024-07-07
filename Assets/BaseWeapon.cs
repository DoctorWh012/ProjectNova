using FirstGearGames.SmoothCameraShaker;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;
using System;

public enum Gunslot : int
{
    primary = 0,
    secondary = 1,
    melee = 2,
}

public enum TracerType
{
    yellow,
    red,
}

public enum CrosshairType : int
{
    cross,
    x,
    square,
    dot,
}

public enum WeaponState
{
    Idle,
    Shooting,
    Ulting,
    Reloading,
    Switching
}

public class GaussianDistribution
{
    // Marsaglia Polar
    float _spareResult;
    bool _nextResultReady = false;

    public float Next()
    {
        float result;
        if (_nextResultReady)
        {
            result = _spareResult;
            _nextResultReady = false;
        }
        else
        {
            float s = -1f, x, y;
            do
            {
                x = 2f * UnityEngine.Random.value - 1f;
                y = 2f * UnityEngine.Random.value - 1f;
                s = x * x + y * y;
            } while (s < 0f || s >= 1f);

            s = Mathf.Sqrt((-2f * Mathf.Log(s)) / s);
            _spareResult = y * s;
            _nextResultReady = true;
            result = x * s;
        }

        return result;
    }
    public float Next(float mean, float sigma = 1f) => mean + sigma * Next();

    public float Next(float mean, float sigma, float min, float max)
    {
        float x = min - 1f; while (x < min || x > max) x = Next(mean, sigma);
        return x;
    }
}


[Serializable]
public struct BodyPartHitTagMultiplier
{
    public string bodyPartTag;
    public float bodyPartMultiplier;
}

public class BaseWeapon : MonoBehaviour
{
    public float tickFireRate { get; protected set; }

    [Header("Components")]
    [Space(5)]
    [SerializeField] protected Player player;
    [SerializeField] protected PlayerHud playerHud;
    [SerializeField] protected PlayerShooting playerShooting;
    [SerializeField] protected Animator animator;
    [SerializeField] protected AudioSource playerAudioSource;
    [SerializeField] protected AudioSource weaponAudioSource;
    [SerializeField] protected AudioSource weaponHumAudioSource;

    [Header("Grip")]
    [Space(5)]
    [SerializeField] public float switchingTime;
    [SerializeField] public string weaponHoldAnimation;

    [SerializeField] public Transform leftHandTarget;
    [SerializeField] public Transform leftHandPole;

    [SerializeField] public Transform rightHandTarget;
    [SerializeField] public Transform rightHandPole;

    [Header("Weapon")]
    [Space(5)]
    [SerializeField] public Gunslot slot;
    [SerializeField] public Sprite weaponIcon;
    [SerializeField] public string weaponName;

    [Header("Damage")]
    [Space(5)]
    [SerializeField] public BodyPartHitTagMultiplier[] weaponDamageMultiplier;
    [SerializeField] public float damage;
    [SerializeField] public float range;
    [SerializeField] public float fireRate;

    [Header("Reload")]
    [Space(5)]
    [SerializeField] public float reloadTime;
    [SerializeField] public int reloadSpins;
    [SerializeField] public int maxAmmo;
    [HideInInspector] protected int _currentAmmo;

    [Header("Ultimate")]
    [Space(5)]
    [SerializeField] public GameObject ultimateIcon;
    [SerializeField] public Slider ultimateSlider;
    [SerializeField] public Image ultimateIconImg;

    public int currentAmmo
    {
        get { return _currentAmmo; }
        set
        {
            _currentAmmo = value;
            if (player.IsLocal) playerHud.UpdateAmmoDisplay(_currentAmmo, maxAmmo);
        }
    }

    [Header("Crosshair")]
    [Space(5)]
    [SerializeField] public CrosshairType crosshairType;
    [SerializeField] public float crosshairScale = 1;
    [SerializeField] public float crosshairShotScale = 1.2f;
    [SerializeField] public float crosshairShrinkTime = 0.5f;

    [Header("Knockback")]
    [Space(5)]
    [SerializeField] public float knockbackForce;
    [SerializeField] public float knockbackMaxHitDistance;

    [Header("Screenshake")]
    [Space(5)]
    [SerializeField] public ShakeData screenShakeData;

    [Header("Audio")]
    [Space(5)]
    [SerializeField] public AudioClip weaponHum;
    [SerializeField] public AudioClip weaponPickupSound;
    [SerializeField, Range(0, 1)] public float weaponSoundVolume = 1;
    [SerializeField] public AudioClip[] weaponSounds;

    [Header("Debugging Serialized")]
    [Space(5)]
    [SerializeField] public WeaponState currentWeaponState = WeaponState.Idle; // Serialized for Debugging

    public int killsPerformed;
    protected float damageMultiplier;
    protected float fireTime;
    public Vector3 startingRotation;

    public void SwitchWeaponState(WeaponState desiredState)
    {
        currentWeaponState = desiredState;
    }

    protected void AssignDefaultDamageMultiplier()
    {
        if (weaponDamageMultiplier.Length == 0) weaponDamageMultiplier = playerShooting.scriptablePlayer.bodyPartHitTagMultipliers;
    }

    protected void BaseStart()
    {
        AssignDefaultDamageMultiplier();
        animator.keepAnimatorStateOnDisable = true;
        startingRotation = transform.localEulerAngles;
        tickFireRate = (1f / fireRate) / Time.fixedDeltaTime;
        fireTime = 1f / fireRate;
        currentAmmo = maxAmmo;
    }

    public virtual void OnWeaponPickUp()
    {
        killsPerformed = 0;
        if (player.IsLocal) playerHud.UpdateWeaponOnSlot((int)slot, weaponName, weaponIcon, false);
    }

    public virtual void ActivateWeapon()
    {
        gameObject.SetActive(true);
        SetupWeaponGrip();
        SwitchWeaponState(WeaponState.Switching);
        CancelInvoke(nameof(FinishSwitching));
        Invoke(nameof(FinishSwitching), switchingTime);
        animator.Play("Raise", 0, 0);

        if (weaponPickupSound)
        {
            weaponAudioSource.pitch = Utilities.GetRandomPitch(0.1f, 0.05f);
            weaponAudioSource.PlayOneShot(weaponPickupSound);
        }

        if (!player.IsLocal) return;
        playerHud.UpdateWeaponOnSlot((int)slot, weaponName, weaponIcon, true);
        playerHud.UpdateAmmoDisplay(currentAmmo, maxAmmo);
        if (ultimateIcon) ultimateIcon.SetActive(true);

        if (SettingsManager.playerPreferences.crosshairType == 0) playerHud.UpdateCrosshair((int)crosshairType, crosshairScale, crosshairShotScale, crosshairShrinkTime);
        else playerHud.UpdateCrosshair((int)CrosshairType.dot, 1, 1, 0);
    }

    public virtual void DeactivateWeapon()
    {
        gameObject.SetActive(false);
        CancelInvoke(nameof(FinishSwitching));
        if (currentWeaponState == WeaponState.Reloading) AbortReload();
        if (currentWeaponState == WeaponState.Ulting) AbortSecondaryAction();

        if (!player.IsLocal) return;
        if (ultimateIcon) ultimateIcon.SetActive(false);
    }

    public virtual void PrimaryAction(uint tick, bool compensatingForSwitch = false)
    {

    }

    public virtual void SecondaryAction(uint tick)
    {

    }

    public virtual void AbortSecondaryAction()
    {

    }

    public virtual void HandleServerWeaponKill(int kills, uint tick)
    {
        killsPerformed = kills;
        playerShooting.lastWeaponKillsTick = tick;
    }

    public virtual void OnKillPerformed()
    {
        killsPerformed++;
        playerShooting.lastWeaponKillsTick = NetworkManager.Singleton.serverTick;
        playerShooting.SendWeaponKill(killsPerformed);
    }

    public virtual bool CanPerformPrimaryAction(uint tick, bool compensatingForSwitch)
    {
        return true;
    }

    public virtual bool CanPerformSecondaryAction(uint tick)
    {
        return false;
    }

    #region Reloading
    public virtual void CheckIfReloadIsNeeded()
    {
        if (currentAmmo <= 0) Reload();
    }

    public virtual void Reload()
    {
        if (!CanReload()) return;
        SwitchWeaponState(WeaponState.Reloading);

        if (NetworkManager.Singleton.Server.IsRunning) playerShooting.SendReloading();
        else if (player.IsLocal) playerShooting.SendReload();

        Tween reloadTween = transform.DOLocalRotate(startingRotation - new Vector3(360 * reloadSpins, 0, 0), reloadTime, RotateMode.LocalAxisAdd);

        if (player.IsLocal) reloadTween.OnUpdate(() => playerHud.UpdateReloadSlider(reloadTween.ElapsedPercentage()));

        reloadTween.OnComplete(() => FinishReloading());
    }

    public bool CanReload()
    {
        if (currentAmmo >= maxAmmo) return false;
        if (currentWeaponState == WeaponState.Reloading) return false;

        playerShooting.lastReloadTick = NetworkManager.Singleton.serverTick;
        return true;
    }

    public void AbortReload()
    {
        transform.DOKill();
        transform.localEulerAngles = startingRotation;
        if (player.IsLocal) playerShooting.playerHud.UpdateReloadSlider(0);
        SwitchWeaponState(WeaponState.Idle);
    }

    protected void FinishReloading()
    {
        ReplenishAmmo();
        SwitchWeaponState(WeaponState.Idle);
    }

    public void ReplenishAmmo()
    {
        currentAmmo = maxAmmo;
    }

    #endregion

    #region Grip
    protected void SetupWeaponGrip()
    {
        playerShooting.leftArm.handIK.enabled = leftHandTarget;
        playerShooting.rightArm.handIK.enabled = rightHandTarget;

        if (leftHandTarget) playerShooting.leftArm.handIK.Target = leftHandTarget;
        if (leftHandPole) playerShooting.leftArm.handIK.Pole = leftHandPole;

        if (rightHandTarget) playerShooting.rightArm.handIK.Target = rightHandTarget;
        if (rightHandPole) playerShooting.rightArm.handIK.Pole = rightHandPole;

        if (!string.IsNullOrEmpty(weaponHoldAnimation))
        {
            if (player.IsLocal) playerShooting.armsAnimator.Play(weaponHoldAnimation, 0, 0);
            else playerShooting.armsAnimator.Play(weaponHoldAnimation, 1, 0);
        }

        if (player.IsLocal && SettingsManager.playerPreferences.renderArms) playerShooting.EnableDisableHandsMeshes(leftHandTarget, rightHandTarget);
    }
    #endregion

    protected RaycastHit FilteredRaycast(Vector3 dir)
    {
        RaycastHit[] filterRayHits = Physics.SphereCastAll(playerShooting.playerCam.position, 0.1f, dir.normalized, range, ~playerShooting.layersToIgnoreShootRaycast);

        Array.Sort(filterRayHits, (x, y) => x.distance.CompareTo(y.distance));

        for (int i = 0; i < filterRayHits.Length; i++)
        {
            if (CompareHitCollider(filterRayHits[i].collider))
            {
                // If this is the last obj the ray collided with and it is still the player returns that it didn't hit anything
                if (filterRayHits.Length - 1 == i) return new RaycastHit();
                continue;
            }
            return filterRayHits[i];
        }
        return new RaycastHit();
    }

    protected List<Collider> FilteredOverlapSphere(Vector3 pos, float radius)
    {
        Collider[] unfilteredCol = Physics.OverlapSphere(pos, radius, ~playerShooting.layersToIgnoreShootRaycast);
        List<Collider> filteredCol = new List<Collider>();

        for (int i = 0; i < unfilteredCol.Length; i++)
        {
            if (CompareHitCollider(unfilteredCol[i])) continue;
            if (CheckDuplicateCol(filteredCol, unfilteredCol[i])) continue;
            filteredCol.Add(unfilteredCol[i]);
        }

        return filteredCol;
    }

    protected Collider[] PlayersOverlapSphere(Vector3 pos, float radius)
    {
        return Physics.OverlapSphere(pos, radius, playerShooting.playersLayer);
    }

    protected bool FilteredObstacleCheck(Vector3 from, Vector3 to)
    {
        return Physics.Raycast(from, to - from, Vector3.Distance(from, to), playerShooting.obstacleLayers);
    }

    protected bool CheckDuplicateCol(List<Collider> filteredCol, Collider col)
    {
        foreach (Collider fcol in filteredCol) if (fcol.transform.root == col.transform.root) return true;
        return false;
    }

    protected bool CheckAltFireConfirmation()
    {
        if (player.IsLocal) return Input.GetKey(SettingsManager.playerPreferences.altFireBtn);
        else return playerShooting.lastAltFireConfirmationTick < playerShooting.lastAltFireTick;
    }

    protected bool CheckPlayerHit(Collider col)
    {
        damageMultiplier = 0;

        for (int i = 0; i < weaponDamageMultiplier.Length; i++)
        {
            if (!col.CompareTag(weaponDamageMultiplier[i].bodyPartTag)) continue;

            damageMultiplier = weaponDamageMultiplier[i].bodyPartMultiplier;

            if (!player.IsLocal) return true;

            playerHud.FadeHitmarker(damageMultiplier > 1, 0.3f);

            playerAudioSource.pitch = Utilities.GetRandomPitch(0.1f, 0.05f);
            AudioClip hitmarkerSfx = damageMultiplier > 1 ? playerShooting.scriptablePlayer.playerHitMarkerSpecialAudio : playerShooting.scriptablePlayer.playerHitMarkerAudio;

            if (hitmarkerSfx) playerAudioSource.PlayOneShot(hitmarkerSfx, playerShooting.scriptablePlayer.playerHitMarkerAudioVolume);

            return true;
        }

        return false;
    }

    protected bool CompareHitCollider(Collider col)
    {
        for (int i = 0; i < playerShooting.bodyColliders.Length; i++) if (col == playerShooting.bodyColliders[i]) return true;
        return false;
    }

    protected void GetHitPlayer(GameObject playerHit, float damage)
    {
        if (!NetworkManager.Singleton.Server.IsRunning) return;

        Player hitPlayer = playerHit.GetComponentInParent<Player>();

        if (hitPlayer.playerHealth.ReceiveDamage(damage * damageMultiplier, player.Id))
        {
            OnKillPerformed();
            MatchManager.Singleton.AddKillToPlayerScore(player.Id);
            player.playerHealth.RecoverHealth(playerShooting.scriptablePlayer.maxHealth);
        }
    }

    public void FinishPrimaryAction()
    {
        if (currentWeaponState == WeaponState.Switching || currentWeaponState == WeaponState.Reloading) return;
        CheckIfReloadIsNeeded();
        SwitchWeaponState(WeaponState.Idle);
    }

    public virtual void FinishSwitching()
    {
        SwitchWeaponState(WeaponState.Idle);
        CheckIfReloadIsNeeded();
    }

    protected void ApplyKnockback()
    {
        if (!player.IsLocal) return;
        if (FilteredRaycast(playerShooting.playerCam.forward).collider) playerShooting.rb.AddForce(-playerShooting.playerCam.forward * knockbackForce * playerShooting.rb.mass, ForceMode.Impulse);
    }
}
