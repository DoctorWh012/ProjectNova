using System;
using UnityEngine;
using FirstGearGames.SmoothCameraShaker;
using DG.Tweening;

public class Weapon : MonoBehaviour
{
    // public float tickFireRate { get; private set; }

    // [Header("Components")]
    // [SerializeField] protected Player player;
    // [SerializeField] protected PlayerHud playerHud;
    // [SerializeField] protected PlayerShooting playerShooting;
    // [SerializeField] protected Animator animator;
    // [SerializeField] protected AudioSource playerAudioSource;
    // [SerializeField] protected AudioSource weaponAudioSource;
    // [SerializeField] protected AudioSource weaponHumAudioSource;

    // [Header("Weapon")]
    // [SerializeField] public Gunslot slot;
    // [SerializeField] public Sprite weaponIcon;
    // [SerializeField] public string weaponName;

    // [Header("Crosshair")]
    // [SerializeField] public CrosshairType crosshairType;
    // [SerializeField] public float crosshairScale = 1;
    // [SerializeField] public float crosshairShotScale = 1.2f;
    // [SerializeField] public float crosshairShrinkTime = 0.5f;

    // [Header("Damage")]
    // [SerializeField] public int damage;
    // [SerializeField] public int range;
    // [SerializeField] public float fireRate;

    // [Header("Reload")]
    // [SerializeField] public float reloadTime;
    // [SerializeField] public int reloadSpins;
    // [SerializeField] public int maxAmmo;
    // [SerializeField] public int currentAmmo;

    // [Header("Recoil")]
    // [SerializeField] public ShakeData screenShakeData;
    // [SerializeField] public float knockbackForce;
    // [SerializeField] public float knockbackMaxHitDistance;

    // [Header("Audio")]
    // [SerializeField] public AudioClip weaponHum;
    // [SerializeField] public AudioClip weaponPickupSound;
    // [SerializeField] public AudioClip weaponSpinSound;
    // [SerializeField] public AudioClip weaponReloadSound;
    // [SerializeField, Range(0, 1)] public float weaponShootingSoundVolume = 1;
    // [SerializeField] public AudioClip[] weaponSounds;

    // [Header("Tracer")]
    // [SerializeField] protected Transform barrelTip;
    // [SerializeField] public TracerType tracerType;
    // [SerializeField] public float tracerLasts;
    // [SerializeField] public float tracerWidth;

    // [Header("Debugging Serialized")]
    // [SerializeField] public WeaponState currentWeaponState = WeaponState.Idle; // Serialized for Debugging

    // private Vector3 dirSpread;
    // private bool didHitPlayer = false;
    // private float individualPelletDamage;
    // private Vector3 spread;
    // private RaycastHit shootingRayHit;
    // private RaycastHit[] filterRayHits;
    // private ParticleSystem hitParticle;
    // private float damageMultiplier;
    // private TrailRenderer tracer;
    // [SerializeField] protected ParticleSystem muzzleFlash;

    // private void Start()
    // {
    //     animator.keepAnimatorStateOnDisable = true;
    //     tickFireRate = (1f / fireRate) / Time.fixedDeltaTime;
    //     currentAmmo = maxAmmo;
    // }

    // #region Virtual Functions
    // public virtual void ActivateWeapon()
    // {
    //     gameObject.SetActive(true);
    //     SwitchWeaponState(WeaponState.Switching);
    //     animator.Play("Raise", 0, 0);

    //     SetupWeaponGrip();

    //     if (weaponPickupSound)
    //     {
    //         weaponAudioSource.pitch = Utilities.GetRandomPitch(0.1f, 0.05f);
    //         weaponAudioSource.PlayOneShot(weaponPickupSound);
    //     }

    //     if (player.IsLocal)
    //     {
    //         playerHud.UpdateWeaponOnSlot((int)slot, weaponName, weaponIcon, true);
    //         playerHud.UpdateAmmoDisplay(currentAmmo, maxAmmo);
    //         if (SettingsManager.playerPreferences.crosshairType == 0) playerHud.UpdateCrosshair((int)crosshairType, crosshairScale, crosshairShotScale, crosshairShrinkTime);
    //     }
    // }

    // public virtual void DeactivateWeapon()
    // {
    //     if (currentWeaponState == WeaponState.Reloading) AbortReload();
    //     gameObject.SetActive(false);
    // }

    // public virtual void Shoot(uint tick, bool compensatingForSwitch = false)
    // {
    //     if (!GetCanShoot(tick, compensatingForSwitch)) return;
    //     currentAmmo--;
    //     SwitchWeaponState(WeaponState.Shooting);
    //     didHitPlayer = false;

    //     ShootNoSpread();

    //     if (player.IsLocal)
    //     {
    //         // ClearGhosts();
    //         // if (!NetworkManager.Singleton.Server.IsRunning) foreach (Player _player in Player.list.Values) CreateDebugGhosts(_player, false, NetworkManager.Singleton.serverTick);

    //         if (screenShakeData) CameraShakerHandler.ShakeAll(screenShakeData);
    //         playerHud.ScaleCrosshairShot();
    //     }

    //     if (!player.IsLocal && NetworkManager.Singleton.Server.IsRunning) NetworkManager.Singleton.ResetPlayersPositions(player.Id);

    //     if (NetworkManager.Singleton.Server.IsRunning) playerShooting.SendPlayerFire();
    //     else if (player.IsLocal) playerShooting.SendShootMessage();
    // }

    // public virtual void Reload()
    // {
    //     if (!CanReload()) return;
    //     SwitchWeaponState(WeaponState.Reloading);

    //     if (NetworkManager.Singleton.Server.IsRunning) playerShooting.SendReloading();
    //     else if (player.IsLocal) playerShooting.SendReload();

    //     transform.DOLocalRotate(new Vector3(-360 * reloadSpins, 0, 0), reloadTime, RotateMode.LocalAxisAdd).OnComplete(() => FinishReloading());
    // }

    // protected virtual bool GetCanShoot(uint tick, bool compensatingForSwitch)
    // {
    //     if (player.playerHealth.currentPlayerState == PlayerState.Dead) { print("RETURN HERe"); return false; }

    //     if (currentAmmo <= 0) { print("RETURN HERe"); return false; }

    //     if (tick - tickFireRate < playerShooting.lastShotTick) { print("RETURN HERe"); return false; }

    //     if (!compensatingForSwitch && currentWeaponState == WeaponState.Switching) { print("RETURN HERe"); return false; }

    //     if (currentWeaponState == WeaponState.Reloading) { print("RETURN HERe"); return false; }

    //     playerShooting.lastShotTick = tick;
    //     return true;
    // }
    // #endregion

    // private void SetupWeaponGrip()
    // {

    // }

    // public void SwitchWeaponState(WeaponState desiredState)
    // {
    //     currentWeaponState = desiredState;
    // }

    // #region Shooting
    // private void ShootNoSpread()
    // {
    //     // foreach (Player _player in Player.list.Values) CreateDebugGhosts(_player, false, NetworkManager.Singleton.serverTick);

    //     if (!player.IsLocal && NetworkManager.Singleton.Server.IsRunning)
    //     {
    //         for (uint i = playerShooting.lastShotTick - (uint)NetworkManager.overcompensationAmount; i < playerShooting.lastShotTick + NetworkManager.overcompensationAmount + 1; i++)
    //         {
    //             NetworkManager.Singleton.SetAllPlayersPositionsTo(i, player.Id);

    //             shootingRayHit = FilteredRaycast(playerShooting.playerCam.forward);
    //             if (!shootingRayHit.collider) continue;

    //             didHitPlayer = CheckPlayerHit(shootingRayHit.collider);

    //             // foreach (Player _player in Player.list.Values) CreateDebugGhosts(_player, true, i);
    //             if (didHitPlayer) break;
    //         }
    //         if (!shootingRayHit.collider)
    //         {
    //             ShootingEffects(false, false);
    //             return;
    //         }
    //     }
    //     else
    //     {
    //         shootingRayHit = FilteredRaycast(playerShooting.playerCam.forward);

    //         if (!shootingRayHit.collider)
    //         {
    //             ShootingEffects(false, false);
    //             return;
    //         }

    //         didHitPlayer = CheckPlayerHit(shootingRayHit.collider);
    //     }


    //     // If it's a player damages it
    //     if (didHitPlayer)
    //     {
    //         GetHitPlayer(shootingRayHit.collider.gameObject, damage);
    //         ShootingEffects(true, false, true);
    //     }

    //     else ShootingEffects(true, false);
    //     ApplyKnockback();
    // }

    // // private void ShotgunShoot()
    // // {
    // //     individualPelletDamage = damage / pellets;

    // //     for (int i = 0; i < pellets; i++) ShootWithFixedSpread(i);
    // // }

    // // private void ShootWithFixedSpread(int spreadIndex)
    // // {
    // //     // Gets the predefined spread
    // //     spread.x = spreadPatterns[spreadIndex].x;
    // //     spread.y = spreadPatterns[spreadIndex].y;
    // //     spread.z = 0;

    // //     // Applies the spread to the raycast
    // //     dirSpread = playerShooting.playerCam.forward + Quaternion.LookRotation(playerShooting.playerCam.forward) * spread * spread;

    // //     if (!player.IsLocal && NetworkManager.Singleton.Server.IsRunning)
    // //     {
    // //         for (uint i = lastShotTick - (uint)NetworkManager.overcompensationAmount; i < lastShotTick + NetworkManager.overcompensationAmount; i++)
    // //         {
    // //             NetworkManager.Singleton.SetAllPlayersPositionsTo(i, player.Id);

    // //             shootingRayHit = FilteredRaycast(dirSpread);
    // //             if (!shootingRayHit.collider) continue;
    // //             didHitPlayer = CheckPlayerHit(shootingRayHit.collider);

    // //             // foreach (Player _player in Player.list.Values) CreateDebugGhosts(_player, true, i);
    // //             if (didHitPlayer) break;
    // //         }
    // //         if (!shootingRayHit.collider)
    // //         {
    // //             ShootingEffects(false, true);
    // //             return;
    // //         }
    // //     }
    // //     else
    // //     {
    // //         shootingRayHit = FilteredRaycast(dirSpread);
    // //         if (!shootingRayHit.collider)
    // //         {
    // //             ShootingEffects(false, true);
    // //             return;
    // //         }
    // //         didHitPlayer = CheckPlayerHit(shootingRayHit.collider);
    // //     }

    // //     // If it's a player damages it 
    // //     if (didHitPlayer)
    // //     {
    // //         GetHitPlayer(shootingRayHit.collider.gameObject, individualPelletDamage);
    // //         ShootingEffects(true, true, true);
    // //     }
    // //     else ShootingEffects(true, true);
    // //     ApplyKnockback();
    // // }

    // private void ShootWithVariableSpread()
    // {

    // }

    // private RaycastHit FilteredRaycast(Vector3 dir)
    // {
    //     filterRayHits = Physics.RaycastAll(playerShooting.playerCam.position, dir.normalized, range, ~playerShooting.layersToIgnoreShootRaycast);
    //     System.Array.Sort(filterRayHits, (x, y) => x.distance.CompareTo(y.distance));

    //     for (int i = 0; i < filterRayHits.Length; i++)
    //     {
    //         if (CompareHitCollider(filterRayHits[i].collider))
    //         {
    //             // If this is the last obj the ray collided with and it is still the player returns that it didn't hit anything
    //             if (filterRayHits.Length - 1 == i)
    //             {
    //                 return new RaycastHit();
    //             }
    //             continue;
    //         }
    //         return filterRayHits[i];
    //     }
    //     return new RaycastHit();
    // }

    // private void ShootingEffects(bool didHit, bool hasSpread, bool hitPlayer = false)
    // {
    //     animator.Play("Recoil", 0, 0);
    //     if (weaponSounds.Length != 0)
    //     {
    //         weaponAudioSource.pitch = Utilities.GetRandomPitch(-0.1f, 0.02f);
    //         weaponAudioSource.PlayOneShot(weaponSounds[UnityEngine.Random.Range(0, weaponSounds.Length)], weaponShootingSoundVolume);
    //     }
    //     muzzleFlash.Play();
    //     ShootingTracer(didHit, hasSpread);

    //     if (didHit && !hitPlayer) HitParticle();
    // }

    // LineRenderer debugLine;
    // private void ShootingTracer(bool didHit, bool hasSpread)
    // {
    //     // Get The Tracer From Pool
    //     tracer = PoolingManager.Singleton.GetBulletTracer(tracerType);

    //     // Configures Tracer
    //     tracer.time = tracerLasts;
    //     tracer.transform.DOComplete();
    //     tracer.transform.gameObject.layer = player.IsLocal ? playerShooting.playerLayer : playerShooting.netPlayerLayer;
    //     tracer.transform.position = barrelTip.position;
    //     tracer.Clear();

    //     Vector3 endPos = didHit ? shootingRayHit.point : hasSpread ? (dirSpread.normalized * range) + barrelTip.position : playerShooting.playerCam.forward * range + barrelTip.position;

    //     tracer.transform.DOMove(endPos, tracerLasts).SetEase(Ease.Linear);
    //     tracer.DOResize(tracerWidth, 0, 0.5f);

    //     // Returns Tracer To Pool After It's Used
    //     tracer.GetComponent<ReturnToPool>().ReturnToPoolIn(tracerLasts);

    //     if (player.IsLocal && NetworkManager.Singleton.Server.IsRunning) return;

    //     // if (!debugLine) debugLine = gameObject.AddComponent<LineRenderer>();
    //     // debugLine.startWidth = 0.05f;
    //     // debugLine.endWidth = 0.05f;
    //     // debugLine.SetPosition(0, playerShooting.playerCam.position);
    //     // debugLine.SetPosition(1, endPos);

    // }

    // private void HitParticle()
    // {
    //     hitParticle = PoolingManager.Singleton.GetHitParticle(tracerType);

    //     hitParticle.transform.position = shootingRayHit.point;
    //     hitParticle.Play();

    //     hitParticle.GetComponent<ReturnToPool>().ReturnToPoolIn(hitParticle.main.duration);
    // }

    // private bool CheckPlayerHit(Collider col)
    // {
    //     damageMultiplier = 0;

    //     for (int i = 0; i < playerShooting.scriptablePlayer.bodyPartHitTagMultipliers.Length; i++)
    //     {
    //         if (!col.CompareTag(playerShooting.scriptablePlayer.bodyPartHitTagMultipliers[i].bodyPartTag)) continue;

    //         damageMultiplier = playerShooting.scriptablePlayer.bodyPartHitTagMultipliers[i].bodyPartMultiplier;

    //         if (!player.IsLocal) return true;

    //         playerHud.FadeHitmarker(damageMultiplier > 1, 0.3f);

    //         playerAudioSource.pitch = Utilities.GetRandomPitch(0.1f, 0.05f);
    //         AudioClip hitmarkerSfx = damageMultiplier > 1 ? playerShooting.scriptablePlayer.playerHitMarkerSpecialAudio : playerShooting.scriptablePlayer.playerHitMarkerAudio;

    //         if (hitmarkerSfx) playerAudioSource.PlayOneShot(hitmarkerSfx, playerShooting.scriptablePlayer.playerHitMarkerAudioVolume);

    //         return true;
    //     }

    //     return false;
    // }

    // private bool CompareHitCollider(Collider col)
    // {
    //     for (int i = 0; i < playerShooting.bodyColliders.Length; i++) if (col == playerShooting.bodyColliders[i]) return true;
    //     return false;
    // }

    // private void ApplyKnockback()
    // {
    //     if (!player.IsLocal) return;
    //     if (FilteredRaycast(playerShooting.playerCam.forward).collider) playerShooting.rb.AddForce(-playerShooting.playerCam.forward * knockbackForce * playerShooting.rb.mass, ForceMode.Impulse);
    // }

    // private void GetHitPlayer(GameObject playerHit, float damage)
    // {
    //     if (!NetworkManager.Singleton.Server.IsRunning) return;

    //     Player hitPlayer = playerHit.GetComponentInParent<Player>();

    //     if (hitPlayer.playerHealth.ReceiveDamage(damage * damageMultiplier, player.Id))
    //     {
    //         MatchManager.Singleton.AddKillToPlayerScore(player.Id);
    //         player.playerHealth.RecoverHealth(playerShooting.scriptablePlayer.maxHealth);
    //     }
    // }

    // public void FinishPlayerShooting()
    // {
    //     if (currentWeaponState == WeaponState.Switching || currentWeaponState == WeaponState.Reloading) return;
    //     print("FinishPlayerShooting");
    //     CheckIfReloadIsNeeded();
    //     SwitchWeaponState(WeaponState.Idle);
    // }
    // #endregion

    // #region Reloading
    // public void CheckIfReloadIsNeeded()
    // {
    //     if (currentAmmo <= 0) Reload();
    // }

    // public void ReplenishAmmo()
    // {
    //     currentAmmo = maxAmmo;
    // }

    // public bool CanReload()
    // {
    //     if (currentAmmo >= maxAmmo) return false;
    //     if (currentWeaponState == WeaponState.Reloading) return false;

    //     playerShooting.lastReloadTick = NetworkManager.Singleton.serverTick;
    //     return true;
    // }

    // protected void FinishReloading()
    // {
    //     ReplenishAmmo();
    //     SwitchWeaponState(WeaponState.Idle);
    // }

    // public void AbortReload()
    // {
    //     transform.DOKill();
    //     if (player.IsLocal) playerShooting.playerHud.UpdateReloadSlider(0);
    //     SwitchWeaponState(WeaponState.Idle);
    // }
    // #endregion

    // #region Switching
    // public void FinishSwitching()
    // {
    //     print("FinishSwitch");
    //     SwitchWeaponState(WeaponState.Idle);
    //     CheckIfReloadIsNeeded();
    // }
    // #endregion
}
