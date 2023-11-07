using Riptide;
using UnityEngine;
using System.Collections;
using DitzelGames.FastIK;

/* WORK REMINDER

    Implement CameraShake
    Implement Ik Aiming

*/

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

public class PlayerShooting : MonoBehaviour
{
    private enum ShootingState { Active, OnCooldown }
    public enum WeaponState { Idle, Shooting, Reloading, Switching }
    private bool isTilting = false;

    [Header("Components")]
    [Space(5)]
    [SerializeField] private Player player;
    [SerializeField] private ScriptablePlayer scriptablePlayer;
    [SerializeField] private PlayerHud playerHud;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private BoxCollider[] bodyColliders;
    [SerializeField] private LayerMask layersToIgnoreShootRaycast;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Transform playerCam;

    [Header("Weapons")]
    [Space(5)]
    [SerializeField] public GunComponents[] gunsComponents;
    [SerializeField] public MeleeComponents[] meleesComponents;

    [Header("Audio")]
    [Space(5)]
    [SerializeField] private AudioSource weaponAudioSource;
    [SerializeField] private AudioSource weaponHumAudioSource;
    [SerializeField] private AudioSource weaponReloadSpinAudioSource;

    [Header("Arms")]
    [Space(5)]
    [SerializeField] private SkinnedMeshRenderer leftArmMesh;
    [SerializeField] private SkinnedMeshRenderer rightArmMesh;
    [SerializeField] private FastIKFabric leftArmIk;
    [SerializeField] private FastIKFabric rightArmIk;
    private bool renderArms;

    [Header("Debugging Serialized")]
    [Space(5)]
    [SerializeField] private WeaponState currentWeaponState = WeaponState.Idle; // Serialized for Debugging
    [SerializeField] private ShootingState currentShootingState = ShootingState.Active; // Serialized for Debugging
    [SerializeField] private Guns[] currentPlayerGuns; // Serialized for Debugging
    [SerializeField] private int[] currentPlayerGunsIndexes; // Serialized for Debugging
    [SerializeField] public Guns activeGun; // Serialized for Debugging
    [SerializeField] private int _ammunition; // Serialized For Debugging

    private bool isWaitingForReload = false;

    // Shooting Cache
    Vector3 dirSpread;
    private float individualPelletDamage;
    private Vector3 spread;
    private RaycastHit shootingRayHit;
    private RaycastHit[] filterRayHits;
    private TrailRenderer tracer;
    private ParticleSystem hitParticle;
    private float damageMultiplier;
    private int activeGunSlot;

    private ushort lastShotTick = 0;
    private ushort lastSlotChangeTick = 0;
    private ushort lastReloadTick = 0;

    private int playerLayer;
    private int NetPlayerLayer;

    private IEnumerator tiltWeaponCoroutine;
    private IEnumerator reloadCoroutine;

    private GunComponents activeGunComponents;
    private MeleeComponents activeMeleeComponents;

    private Animator animator;
    private Transform barrelTip;

    private int ammunition
    {
        get { return _ammunition; }
        set
        {
            _ammunition = value;
            if (player.IsLocal) playerHud.UpdateAmmoDisplay(ammunition, activeGun.maxAmmo);
        }
    }

    private void Awake()
    {
        if (player.IsLocal) SettingsManager.updatedPlayerPrefs += GetPreferences;
        currentPlayerGuns = new Guns[3];
        currentPlayerGunsIndexes = new int[3];

        playerLayer = LayerMask.NameToLayer("Player");
        NetPlayerLayer = LayerMask.NameToLayer("NetPlayer");
    }

    private void GetPreferences()
    {
        renderArms = SettingsManager.playerPreferences.renderArms;
    }

    private void Start()
    {
        if (player.IsLocal)
        {
            if (!NetworkManager.Singleton.Server.IsRunning)
            {
                PickStartingWeapons();
                return;
            }
            else GetPreferences();
        }

        PickStartingWeapons();
        Player.clientSpawned += SendWeaponSync;
    }

    private void OnDestroy()
    {
        Player.clientSpawned -= SendWeaponSync;
        SettingsManager.updatedPlayerPrefs -= GetPreferences;
    }

    private void OnApplicationQuit()
    {
        Player.clientSpawned -= SendWeaponSync;
        SettingsManager.updatedPlayerPrefs -= GetPreferences;
    }

    private void Update()
    {
        if (playerHealth.currentPlayerState == PlayerState.Dead || !player.IsLocal || !PlayerHud.Focused) return;

        GetInput();
        CheckWeaponTilt();
    }

    private void GetInput()
    {
        if (Input.GetKey(SettingsManager.playerPreferences.fireBtn)) FireTick(NetworkManager.Singleton.serverTick);

        GunSwitchInput(SettingsManager.playerPreferences.primarySlotKey, 0);
        GunSwitchInput(SettingsManager.playerPreferences.secondarySlotKey, 1);
        GunSwitchInput(SettingsManager.playerPreferences.tertiarySlotKey, 2);

        if (Input.GetKeyDown(SettingsManager.playerPreferences.reloadKey)) StartGunReload();
    }

    private void GunSwitchInput(KeyCode keybind, int index)
    {
        if (Input.GetKeyDown(keybind)) StartSlotSwitch(index, NetworkManager.Singleton.serverTick);
    }

    public void SwitchWeaponState(WeaponState desiredState)
    {
        currentWeaponState = desiredState;
    }

    private bool GetShootingState(ushort tick, bool compensatingForSwitch)
    {
        if (playerHealth.currentPlayerState == PlayerState.Dead) return false;

        if (ammunition <= 0 && activeGun.weaponType != WeaponType.melee) return false;

        if (tick - activeGun.tickFireRate < lastShotTick) return false;

        if (!compensatingForSwitch && currentWeaponState == WeaponState.Switching) return false;

        if (currentWeaponState == WeaponState.Reloading) return false;

        lastShotTick = tick;
        return true;
    }

    #region Shooting
    private void HandleClientFired(int slot, ushort tick)
    {
        bool compensatingForSwitch = tick <= lastSlotChangeTick && activeGun != currentPlayerGuns[slot];

        if (compensatingForSwitch) activeGun = currentPlayerGuns[slot];

        FireTick(tick, compensatingForSwitch);

        if (compensatingForSwitch) activeGun = currentPlayerGuns[activeGunSlot];
    }

    public void FireTick(ushort tick, bool compensatingForSwitch = false)
    {
        if (GetShootingState(tick, compensatingForSwitch)) currentShootingState = ShootingState.Active;
        else currentShootingState = ShootingState.OnCooldown;

        if (activeGun.weaponType == WeaponType.rifle || activeGun.weaponType == WeaponType.shotgun) VerifyGunShoot();
        else if (activeGun.weaponType == WeaponType.melee) VerifyMeleeAttack();
    }

    private void VerifyGunShoot()
    {
        if (currentShootingState != ShootingState.Active) return;

        ammunition--;
        SwitchWeaponState(WeaponState.Shooting);

        if (!player.IsLocal && NetworkManager.Singleton.Server.IsRunning) NetworkManager.Singleton.SetAllPlayersPositionsTo(lastShotTick, player.Id);

        switch (activeGun.weaponType)
        {
            case WeaponType.rifle:
                Shoot();
                break;

            case WeaponType.shotgun:
                ShotgunShoot();
                break;
        }

        if (!player.IsLocal && NetworkManager.Singleton.Server.IsRunning) NetworkManager.Singleton.ResetPlayersPositions(player.Id);

        if (NetworkManager.Singleton.Server.IsRunning) SendPlayerFire();
        else if (player.IsLocal) SendShootMessage();
    }

    private void VerifyMeleeAttack()
    {
        if (currentShootingState != ShootingState.Active) return;
        SwitchWeaponState(WeaponState.Shooting);

        if (!player.IsLocal && NetworkManager.Singleton.Server.IsRunning) NetworkManager.Singleton.SetAllPlayersPositionsTo(lastShotTick, player.Id);

        AttackMelee();

        if (!player.IsLocal && NetworkManager.Singleton.Server.IsRunning) NetworkManager.Singleton.ResetPlayersPositions(player.Id);

        if (NetworkManager.Singleton.Server.IsRunning) SendPlayerFire();
        else if (player.IsLocal) SendShootMessage();

    }

    private void Shoot()
    {
        shootingRayHit = FilteredRaycast(playerCam.forward);

        if (!shootingRayHit.collider)
        {
            ShootingEffects(false, false);
            return;
        }

        // If it's a player damages it
        if (CheckPlayerHit(shootingRayHit.collider))
        {
            GetHitPlayer(shootingRayHit.collider.gameObject, activeGun.damage);
            ShootingEffects(true, true, true);
        }

        else ShootingEffects(true, false);
        ApplyKnockback();

    }

    private void ShotgunShoot()
    {
        individualPelletDamage = activeGun.damage / activeGun.pellets;

        for (int i = 0; i < activeGun.pellets; i++)
        {
            ShootWithFixedSpread(i);
        }
    }

    private void ShootWithFixedSpread(int spreadIndex)
    {
        // Gets the predefined spread
        spread.x = activeGun.spreadPatterns[spreadIndex].x;
        spread.y = activeGun.spreadPatterns[spreadIndex].y;
        spread.z = 0;

        // Applies the spread to the raycast
        dirSpread = playerCam.forward + Quaternion.LookRotation(playerCam.forward) * spread * activeGun.spread;

        shootingRayHit = FilteredRaycast(dirSpread);

        // Checks if it did not hit anything
        if (!shootingRayHit.collider)
        {
            ShootingEffects(false, true);
            return;
        }

        // If it's a player damages it 
        if (CheckPlayerHit(shootingRayHit.collider))
        {
            GetHitPlayer(shootingRayHit.collider.gameObject, individualPelletDamage);
            ShootingEffects(true, true, true);
        }
        else ShootingEffects(true, true);

        ApplyKnockback();
    }

    private void AttackMelee()
    {
        shootingRayHit = FilteredRaycast(playerCam.forward);

        if (!shootingRayHit.collider)
        {
            MeleeEffects(false);
            return;
        }

        // If the first thing it hit is not a player break
        if (CheckPlayerHit(shootingRayHit.collider)) GetHitPlayer(shootingRayHit.collider.gameObject, activeGun.damage);

        MeleeEffects(true);
    }

    private RaycastHit FilteredRaycast(Vector3 dir)
    {
        filterRayHits = Physics.RaycastAll(playerCam.position, dir.normalized, activeGun.range, ~layersToIgnoreShootRaycast);
        System.Array.Sort(filterRayHits, (x, y) => x.distance.CompareTo(y.distance));

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

    private void ShootingEffects(bool didHit, bool hasSpread, bool hitPlayer = false)
    {
        animator.Play("Recoil");
        if (activeGun.weaponShootingSounds.Length != 0)
        {
            weaponAudioSource.pitch = Utilities.GetRandomPitch(-0.1f, 0.02f);
            weaponAudioSource.PlayOneShot(activeGun.weaponShootingSounds[Random.Range(0, activeGun.weaponShootingSounds.Length)], activeGun.weaponShootingSoundVolume);
        }
        activeGunComponents.muzzleFlash.Play();
        ShootingTracer(didHit, hasSpread);

        if (didHit && !hitPlayer) HitParticle();
    }

    private void MeleeEffects(bool didHit, bool hitPlayer = false)
    {
        animator.Play("Attack");
        if (activeGun.weaponShootingSounds.Length != 0)
        {
            weaponAudioSource.pitch = Utilities.GetRandomPitch(-0.1f, 0.02f);
            weaponAudioSource.PlayOneShot(activeGun.weaponShootingSounds[Random.Range(0, activeGun.weaponShootingSounds.Length)], activeGun.weaponShootingSoundVolume);
        }
        if (didHit && !hitPlayer) HitParticle();
    }

    private void ShootingTracer(bool didHit, bool hasSpread)
    {
        // Get The Tracer From Pool
        tracer = PoolingManager.Instance.GetBulletTracer(activeGun.tracerType);

        // Configures Tracer
        tracer.time = activeGun.tracerLasts;
        tracer.transform.gameObject.layer = player.IsLocal ? playerLayer : NetPlayerLayer;
        tracer.Clear();
        tracer.AddPosition(barrelTip.position);

        // Moves Tracer
        if (didHit) tracer.transform.position = shootingRayHit.point;

        else
        {
            if (hasSpread) tracer.transform.position = (dirSpread.normalized * activeGun.range) + barrelTip.position;
            else tracer.transform.position = (playerCam.forward * activeGun.range) + barrelTip.position;
        }

        // Returns Tracer To Pool After It's Used
        tracer.GetComponent<ReturnToPool>().ReturnToPoolIn(activeGun.tracerLasts);
    }

    private void HitParticle()
    {
        hitParticle = PoolingManager.Instance.GetHitParticle(activeGun.tracerType);

        hitParticle.transform.position = shootingRayHit.point;
        hitParticle.Play();

        hitParticle.GetComponent<ReturnToPool>().ReturnToPoolIn(hitParticle.main.duration);
    }

    private bool CheckPlayerHit(Collider col)
    {
        damageMultiplier = 0;

        for (int i = 0; i < scriptablePlayer.bodyPartHitTagMultipliers.Length; i++)
        {
            if (col.CompareTag(scriptablePlayer.bodyPartHitTagMultipliers[i].bodyPartTag))
            {
                damageMultiplier = scriptablePlayer.bodyPartHitTagMultipliers[i].bodyPartMultiplier;
                return true;
            }
        }
        return false;
    }

    private bool CompareHitCollider(Collider col)
    {
        for (int i = 0; i < bodyColliders.Length; i++)
        {
            if (col == bodyColliders[i]) return true;
        }
        return false;
    }

    private void ApplyKnockback()
    {
        if (!player.IsLocal) return;
        if (FilteredRaycast(playerCam.forward).collider) rb.AddForce(-playerCam.forward * activeGun.knockbackForce * rb.mass, ForceMode.Impulse);
    }

    private void GetHitPlayer(GameObject playerHit, float damage)
    {
        if (!NetworkManager.Singleton.Server.IsRunning) return;

        Player player = playerHit.GetComponentInParent<Player>();

        if (player.playerHealth.ReceiveDamage(damage * damageMultiplier)) ;
    }

    public void FinishPlayerShooting()
    {
        CheckIfReloadIsNeeded();
        SwitchWeaponState(WeaponState.Idle);
    }
    #endregion

    #region Reloading
    public void CheckIfReloadIsNeeded()
    {
        if (ammunition <= 0 && activeGun.weaponType != WeaponType.melee) StartGunReload();
    }

    public void ReplenishAmmo()
    {
        ammunition = activeGun.maxAmmo;
    }

    public void ReplenishAllAmmo()
    {
        for (int i = 0; i < gunsComponents.Length; i++)
        {
            gunsComponents[i].gunSettings.currentAmmo = gunsComponents[i].gunSettings.maxAmmo;
        }

        ammunition = activeGun.maxAmmo;
    }

    private void HandleClientReload(int slot, ushort tick)
    {
        if (tick < lastReloadTick || activeGunSlot != slot) return;
        lastReloadTick = tick;

        StartGunReload();
    }

    public void StartGunReload()
    {
        if (ammunition == activeGun.maxAmmo || currentWeaponState == WeaponState.Reloading || isWaitingForReload) return;

        reloadCoroutine = RotateReloadGun(activeGun.reloadSpins, activeGun.reloadTime);
        StartCoroutine(reloadCoroutine);
    }

    private void StopReload()
    {
        StopCoroutine(reloadCoroutine);
        isWaitingForReload = false;
        animator.enabled = true;
        weaponReloadSpinAudioSource.loop = false;
        weaponReloadSpinAudioSource.Stop();
        SwitchWeaponState(WeaponState.Idle);
    }
    #endregion

    #region GunSwitching
    public void PickStartingWeapons()
    {
        if (player.IsLocal) playerHud.ResetWeaponsOnSlots();

        for (int i = 0; i < currentPlayerGuns.Length; i++)
        {
            currentPlayerGuns[i] = scriptablePlayer.startingGuns[i];
            currentPlayerGunsIndexes[i] = scriptablePlayer.startingWeaponsIndex[i];

            if (currentPlayerGuns[i] && player.IsLocal) playerHud.UpdateWeaponOnSlot(i, currentPlayerGuns[i].gunName, currentPlayerGuns[i].gunIcon, false);
        }

        StartSlotSwitch(0, NetworkManager.Singleton.serverTick);
    }

    private void PickSyncedWeapons(int primaryIndex, int secondaryIndex, int meleeIndex)
    {
        currentPlayerGuns[0] = gunsComponents[primaryIndex].gunSettings;
        currentPlayerGunsIndexes[0] = primaryIndex;

        currentPlayerGuns[1] = gunsComponents[secondaryIndex].gunSettings;
        currentPlayerGunsIndexes[1] = secondaryIndex;

        currentPlayerGuns[2] = meleesComponents[meleeIndex].meleeSettings;
        currentPlayerGunsIndexes[2] = meleeIndex;
    }

    private void HandleServerWeaponSwitch(ushort tick, int ammo, int slot)
    {
        if (tick < lastSlotChangeTick) return;
        print($"Server is telling me to switch to slot {slot}");
        StartSlotSwitch(slot, tick, true);
        ammunition = ammo;

    }

    private void HandleClientWeaponSwitch(ushort tick, int slot)
    {
        if (tick < lastSlotChangeTick) return;
        print($"Client is telling me to switch to slot {slot}");
        StartSlotSwitch(slot, tick);
    }

    public void StartSlotSwitch(int slotIndex, ushort tick, bool askedByServer = false)
    {
        if (!currentPlayerGuns[slotIndex] || currentPlayerGuns[slotIndex] == activeGun) return;
        if (currentWeaponState == WeaponState.Reloading || isWaitingForReload) StopReload();
        print($"Passed checks Switching to slot {slotIndex} currently on slot {activeGunSlot}");

        SlotSwitch(slotIndex);

        lastSlotChangeTick = tick;
        print($"Was asked to switch to {slotIndex} currently on slot {activeGunSlot}");

        if (NetworkManager.Singleton.Server.IsRunning) SendGunSwitch(slotIndex);
        else if (player.IsLocal && !askedByServer) SendSlotSwitch(slotIndex);

    }

    private void SlotSwitch(int slotIndex)
    {
        SwitchWeaponState(WeaponState.Switching);
        activeGunSlot = slotIndex;

        // This is saving the ammunition before changing guns
        if (activeGun) activeGun.currentAmmo = ammunition;

        // Changes guns
        activeGun = currentPlayerGuns[slotIndex];
        ammunition = activeGun.currentAmmo;

        if (player.IsLocal) playerHud.UpdateWeaponOnSlot(slotIndex, activeGun.gunName, activeGun.gunIcon, true);

        if (activeGun.weaponType != WeaponType.melee) SwitchGun(currentPlayerGunsIndexes[slotIndex]);
        else SwitchMelee(currentPlayerGunsIndexes[slotIndex]);

        animator.Play("Raise");
    }


    public void SwitchGun(int index)
    {
        activeGunComponents = gunsComponents[index];

        barrelTip = gunsComponents[index].barrelTip;
        animator = gunsComponents[index].animator;

        // if (player.IsLocal) GameCanvas.Instance.ChangeGunSlotIcon(((int)activeGun.slot), activeGun.gunIcon, activeGun.gunName);

        EnableActiveWeapon(activeGunComponents.gunSettings.weaponType);
    }

    public void SwitchMelee(int index)
    {
        activeMeleeComponents = meleesComponents[index];
        animator = meleesComponents[index].animator;
        // if (player.IsLocal) GameCanvas.Instance.ChangeGunSlotIcon(((int)meleesComponents[index].meleeSettings.slot), meleesComponents[index].meleeSettings.gunIcon, activeGun.name);
        EnableActiveWeapon(WeaponType.melee);
    }

    public void PickUpGun(int slot, int pickedGunIndex, ushort tick)
    {
        Guns pickedGun = gunsComponents[pickedGunIndex].gunSettings;
        currentPlayerGuns[slot] = pickedGun;
        currentPlayerGunsIndexes[slot] = pickedGunIndex;
        StartSlotSwitch(slot, tick);
        ReplenishAmmo();

        if (NetworkManager.Singleton.Server.IsRunning) SendPickedUpGun(slot, pickedGunIndex);
    }

    public void PickUpMelee(int pickedGunIndex, ushort tick)
    {
        Guns pickedMelee = meleesComponents[pickedGunIndex].meleeSettings;
        currentPlayerGuns[2] = pickedMelee;
        currentPlayerGunsIndexes[2] = pickedGunIndex;
        StartSlotSwitch(2, tick);
    }

    public void FinishSwitching()
    {
        SwitchWeaponState(WeaponState.Idle);
        CheckIfReloadIsNeeded();
    }
    #endregion

    public void EnableActiveWeapon(WeaponType weaponType)
    {
        DisableAllGuns();
        DisableAllMelees();

        rightArmIk.enabled = true;
        leftArmIk.enabled = true;

        // Sets up Audio stuff
        if (activeGun.weaponHum)
        {
            weaponHumAudioSource.clip = activeGun.weaponHum;
            weaponHumAudioSource.Play();
        }
        else weaponHumAudioSource.Stop();

        if (activeGun.weaponPickupSound)
        {
            weaponAudioSource.pitch = Utilities.GetRandomPitch(0.1f, 0.05f);
            weaponAudioSource.PlayOneShot(activeGun.weaponPickupSound);
        }

        if (weaponType != WeaponType.melee)
        {
            // Enables Gun
            activeGunComponents.gameObject.SetActive(true);

            // Places The Arms Targets On The Active Weapon
            if (player.IsLocal)
            {
                rightArmMesh.enabled = !renderArms ? false : activeGunComponents.rightArmTarget;
                leftArmMesh.enabled = !renderArms ? false : activeGunComponents.leftArmTarget;
            }

            if (activeGunComponents.rightArmTarget) rightArmIk.Target = activeGunComponents.rightArmTarget;
            else rightArmIk.enabled = false;
            if (activeGunComponents.leftArmTarget) leftArmIk.Target = activeGunComponents.leftArmTarget;
            else leftArmIk.enabled = false;

            // Enables The Scope If The Weapon Has One And The Player Is Local
            // if (!activeGunComponents.gunSettings.canAim || !player.IsLocal) return;
            // scopeCam.enabled = true;
            // scopeCam.fieldOfView = activeGun.scopeFov;
            return;
        }

        // Enables Melee Weapon
        activeMeleeComponents.gameObject.SetActive(true);

        // Places The Arms Targets On The Active Weapon
        if (player.IsLocal)
        {
            rightArmMesh.enabled = !renderArms ? false : activeMeleeComponents.rightArmTarget;
            leftArmMesh.enabled = !renderArms ? false : activeMeleeComponents.leftArmTarget;
        }
        if (activeMeleeComponents.rightArmTarget) rightArmIk.Target = activeMeleeComponents.rightArmTarget;
        else rightArmIk.enabled = false;
        if (activeMeleeComponents.leftArmTarget) leftArmIk.Target = activeMeleeComponents.leftArmTarget;
        else leftArmIk.enabled = false;
    }

    public void DisableAllGuns()
    {
        for (int i = 0; i < gunsComponents.Length; i++)
        {
            gunsComponents[i].gameObject.SetActive(false);

            // if (!gunsComponents[i].gunSettings.canAim || !player.IsLocal) continue;

            // scopeCam.enabled = false;
        }
    }

    public void DisableAllMelees()
    {
        for (int i = 0; i < meleesComponents.Length; i++)
        {
            meleesComponents[i].gameObject.SetActive(false);
        }
    }

    public void CheckWeaponTilt()
    {
        if (playerMovement.currentMovementState == PlayerMovement.MovementStates.Crouched && rb.velocity.magnitude > 8f)
        {
            TiltGun(35, 0.25f);
        }

        else
        {
            TiltGun(0, 0.15f);
        }
    }

    public void EnableDisableHandsMeshes(bool state)
    {
        leftArmMesh.enabled = state;
        rightArmMesh.enabled = state;
    }

    #region ServerSenders
    private void SendPlayerFire()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.playerFired);
        message.AddUShort(player.Id);
        message.AddByte((byte)activeGunSlot);
        message.AddUShort(lastShotTick);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    private void SendGunSwitch(int gunSlot)
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.gunChanged);
        message.AddUShort(player.Id);
        message.AddUShort(lastSlotChangeTick);
        message.AddUShort((ushort)ammunition);
        message.AddByte((byte)gunSlot);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    private void SendReloading()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.gunReloading);
        message.AddUShort(player.Id);
        message.AddByte((byte)activeGunSlot);
        message.AddUShort(lastReloadTick);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    public void SendPickedUpGun(int slot, int pickedGunIndex)
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.pickedGun);
        message.AddUShort(player.Id);
        message.AddByte((byte)slot);
        message.AddByte((byte)pickedGunIndex);
        message.AddUShort(lastSlotChangeTick);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    private void SendWeaponSync(ushort id)
    {
        if (!player) return;
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.weaponSync);
        message.AddUShort(player.Id);
        message.AddByte((byte)currentPlayerGunsIndexes[0]);
        message.AddByte((byte)currentPlayerGunsIndexes[1]);
        message.AddByte((byte)currentPlayerGunsIndexes[2]);

        message.AddByte((byte)activeGunSlot);
        message.AddUShort(lastSlotChangeTick);
        NetworkManager.Singleton.Server.Send(message, id);
    }
    #endregion

    #region ClientSenders
    private void SendShootMessage()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ClientToServerId.fireInput);
        message.AddByte((byte)activeGunSlot);
        message.AddUShort(lastShotTick);
        NetworkManager.Singleton.Client.Send(message);
    }

    private void SendReload()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ClientToServerId.gunReload);
        message.AddByte((byte)activeGunSlot);
        message.AddUShort(lastReloadTick);
        NetworkManager.Singleton.Client.Send(message);
    }

    public void SendSlotSwitch(int index)
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ClientToServerId.slotChange);
        message.AddUShort(lastSlotChangeTick);
        message.AddByte((byte)index);
        NetworkManager.Singleton.Client.Send(message);
    }
    #endregion

    #region ClientToServerHandlers
    [MessageHandler((ushort)ClientToServerId.fireInput)]
    private static void FireInput(ushort fromClientId, Message message)
    {
        if (Player.list.TryGetValue(fromClientId, out Player player))
        {
            player.playerShooting.HandleClientFired(message.GetByte(), message.GetUShort());
        }
    }

    [MessageHandler((ushort)ClientToServerId.slotChange)]
    private static void ChangeSlot(ushort fromClientId, Message message)
    {
        if (Player.list.TryGetValue(fromClientId, out Player player))
        {
            player.playerShooting.HandleClientWeaponSwitch(message.GetUShort(), (int)message.GetByte());
        }
    }

    [MessageHandler((ushort)ClientToServerId.gunReload)]
    private static void ReloadGun(ushort fromClientId, Message message)
    {
        if (Player.list.TryGetValue(fromClientId, out Player player))
        {
            player.playerShooting.HandleClientReload((int)message.GetByte(), message.GetUShort());
        }
    }
    #endregion

    #region ServerToClientHandlers
    [MessageHandler((ushort)ServerToClientId.playerFired)]
    private static void PlayerFired(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            if (player.IsLocal) return;
            player.playerShooting.HandleClientFired((int)message.GetByte(), message.GetUShort());
        }
    }

    [MessageHandler((ushort)ServerToClientId.pickedGun)]
    private static void PickGun(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            player.playerShooting.PickUpGun((int)message.GetByte(), (int)message.GetByte(), message.GetUShort());
        }
    }

    [MessageHandler((ushort)ServerToClientId.gunReloading)]
    private static void PlayerGunReloading(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            player.playerShooting.HandleClientReload((int)message.GetByte(), message.GetUShort());
        }
    }

    [MessageHandler((ushort)ServerToClientId.weaponSync)]
    private static void SyncWeapons(Message message)
    {
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            player.playerShooting.PickSyncedWeapons((int)message.GetByte(), (int)message.GetByte(), (int)message.GetByte());
            player.playerShooting.StartSlotSwitch((int)message.GetByte(), message.GetUShort());
        }
    }

    [MessageHandler((ushort)ServerToClientId.gunChanged)]
    private static void ChangeGun(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            if (!player.playerShooting.activeGun) return;
            player.playerShooting.HandleServerWeaponSwitch(message.GetUShort(), (int)message.GetUShort(), (int)message.GetByte());
        }
    }
    #endregion

    public void TiltGun(float angle, float duration)
    {
        if (isTilting) return;
        tiltWeaponCoroutine = TiltWeapon(angle, duration);
        StartCoroutine(tiltWeaponCoroutine);
    }

    // FUCK QUATERNIONS
    public IEnumerator RotateReloadGun(int times, float duration)
    {
        isWaitingForReload = true;
        while (currentWeaponState != WeaponState.Idle) yield return null;
        SwitchWeaponState(WeaponState.Reloading);

        if (player.IsLocal) lastReloadTick = NetworkManager.Singleton.serverTick;

        if (NetworkManager.Singleton.Server.IsRunning) SendReloading();
        else if (player.IsLocal) SendReload();

        if (activeGun.weaponSpinSound)
        {
            weaponReloadSpinAudioSource.clip = activeGun.weaponSpinSound;
            weaponReloadSpinAudioSource.loop = true;
            weaponReloadSpinAudioSource.Play();
        }

        animator.enabled = false;

        Vector3 startingAngle = activeGunComponents.gunModelPos.localEulerAngles;
        float toAngle = startingAngle.x + -360 * times;
        float t = 0;

        while (t < duration)
        {
            t += Time.deltaTime;
            float xRot = Mathf.Lerp(startingAngle.x, toAngle, t / duration);
            activeGunComponents.gunModelPos.localEulerAngles = new Vector3(xRot, startingAngle.y, startingAngle.z);
            yield return null;
        }

        activeGunComponents.gunModelPos.localEulerAngles = startingAngle;
        animator.enabled = true;
        ReplenishAmmo();

        if (activeGun.weaponSpinSound)
        {
            weaponReloadSpinAudioSource.Stop();
            weaponReloadSpinAudioSource.loop = false;

            if (activeGun.weaponReloadSound)
            {
                weaponAudioSource.pitch = Utilities.GetRandomPitch(0.1f, 0.05f);
                weaponAudioSource.PlayOneShot(activeGun.weaponReloadSound);
            }
        }
        SwitchWeaponState(WeaponState.Idle);
        isWaitingForReload = false;
    }

    private IEnumerator TiltWeapon(float tiltAngle, float duration)
    {
        isTilting = true;
        Transform weaponTransform = activeGunComponents.transform;
        Quaternion startingAngle = weaponTransform.localRotation;
        Quaternion toAngle = Quaternion.Euler(new Vector3(0, 0, tiltAngle));
        float rotationDuration = 0;

        while (weaponTransform.localRotation != toAngle)
        {
            weaponTransform.localRotation = Quaternion.Lerp(startingAngle, toAngle, rotationDuration / duration);
            rotationDuration += Time.deltaTime;
            yield return null;
        }
        isTilting = false;
    }
}
