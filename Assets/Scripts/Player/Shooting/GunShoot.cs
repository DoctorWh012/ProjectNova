using Riptide;
using UnityEngine;
using System.Collections;
using EZCameraShake;
using DitzelGames.FastIK;

/* WORK REMINDER

    Turn ChangeToSlot into a coroutine | Fixed
    Network shooting
    Does the weapons GameObject have to stay enabled? | Fixed
    Fix weapon tilting (Check for weapon tilt when switch)
    Should firerate be animation based or time based | Fixed
    Implement CameraShake

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

public class GunShoot : MonoBehaviour
{
    public enum WeaponState { Active, Shooting, Reloading, Switching }
    public bool isWeaponTilted { get; private set; } = false;

    [SerializeField] private WeaponState currentWeaponState = WeaponState.Active; // Serialized for Debugging

    [Header("Components")]
    [SerializeField] private Player player;
    [SerializeField] private ScriptablePlayer scriptablePlayer;
    [SerializeField] private BoxCollider[] bodyColliders;
    [SerializeField] private LayerMask layersToIgnoreShootRaycast;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private PlayerScore playerScore;
    [SerializeField] private Transform playerCam;
    [SerializeField] private HeadBobController headBobController;
    [SerializeField] private Camera scopeCam;

    [Header("Weapons")]
    [SerializeField] private Guns[] currentPlayerGuns; // Serialized for Debugging
    [SerializeField] private int[] currentPlayerGunsIndexes; // Serialized for Debugging
    [SerializeField] public GunComponents[] gunsComponents;
    [SerializeField] public MeleeComponents[] meleesComponents;

    [Header("Audio")]
    [SerializeField] private AudioSource weaponAudioSource;
    [SerializeField] private AudioSource weaponHumAudioSource;

    [Header("Arms")]
    [SerializeField] private SkinnedMeshRenderer leftArmMesh;
    [SerializeField] private SkinnedMeshRenderer rightArmMesh;
    [SerializeField] private FastIKFabric leftArmIk;
    [SerializeField] private FastIKFabric rightArmIk;

    // Shooting Cache
    Vector3 dirSpread;
    private float individualPelletDamage;
    private Vector3 spread;
    private RaycastHit shootingRayHit;
    private RaycastHit[] filterRayHits;
    private TrailRenderer tracer;
    private ParticleSystem hitParticle;

    private int playerLayer;
    private int NetPlayerLayer;
    private IEnumerator tiltWeaponCoroutine;
    private IEnumerator reloadCoroutine;
    private GunComponents activeGunComponents;
    private MeleeComponents activeMeleeComponents;
    private Animator animator;
    private Transform barrelTip;
    private float damageMultiplier;
    private int _ammunition;
    private int ammunition
    {
        get { return _ammunition; }
        set
        {
            _ammunition = value;
            if (player.IsLocal) GameCanvas.Instance.UpdateAmmunition(ammunition, activeGun.maxAmmo);
        }
    }
    private float nextTimeToFire = 0f;
    private bool isWaitingForAction;
    public Guns activeGun;
    private int activeGunSlot;
    private ushort lastShotTick;

    private void Awake()
    {
        currentPlayerGuns = new Guns[3];
        currentPlayerGunsIndexes = new int[3];

        playerLayer = LayerMask.NameToLayer("Player");
        NetPlayerLayer = LayerMask.NameToLayer("NetPlayer");
    }

    private void Start()
    {
        if (!NetworkManager.Singleton.Server.IsRunning)
        {
            if (player.IsLocal) PickStartingWeapons();
            return;
        }
        PickStartingWeapons();
        Player.playerJoinedServer += SendWeaponSync;
    }

    public void SwitchWeaponState(WeaponState desiredState)
    {
        currentWeaponState = desiredState;
    }

    #region Shooting
    public void HandleClientInput(bool shooting, ushort tick)
    {
        if (!shooting) return;
        lastShotTick = tick;
        FireTick();
    }

    public void FireTick()
    {
        if (activeGun.weaponType == WeaponType.rifle || activeGun.weaponType == WeaponType.shotgun) VerifyGunShoot();
        else if (activeGun.weaponType == WeaponType.melee) VerifyMeleeAttack();
    }

    private void VerifyGunShoot()
    {
        if (currentWeaponState == WeaponState.Reloading || currentWeaponState == WeaponState.Switching || isWaitingForAction) return;

        if (ammunition > 0 && Time.time >= nextTimeToFire)
        {
            nextTimeToFire = Time.time + 1f / activeGun.fireRate;
            ammunition--;
            SwitchWeaponState(WeaponState.Shooting);

            if (!player.IsLocal && NetworkManager.Singleton.Server.IsRunning) GameManager.Singleton.SetAllPlayersPositionsTo(lastShotTick, player.Id);
            switch (activeGun.weaponType)
            {
                case WeaponType.rifle:
                    Shoot();
                    break;

                case WeaponType.shotgun:
                    ShotgunShoot();
                    break;
            }
            if (!player.IsLocal && NetworkManager.Singleton.Server.IsRunning) GameManager.Singleton.ResetPlayersPositions(player.Id);
        }
    }

    private void VerifyMeleeAttack()
    {
        if (currentWeaponState == WeaponState.Reloading || currentWeaponState == WeaponState.Switching || isWaitingForAction) return;

        if (Time.time >= nextTimeToFire)
        {
            nextTimeToFire = Time.time + 1f / activeGun.fireRate;
            SwitchWeaponState(WeaponState.Shooting);

            if (!player.IsLocal && NetworkManager.Singleton.Server.IsRunning) GameManager.Singleton.SetAllPlayersPositionsTo(lastShotTick, player.Id);

            AttackMelee();

            if (!player.IsLocal && NetworkManager.Singleton.Server.IsRunning) GameManager.Singleton.ResetPlayersPositions(player.Id);
        }
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
        if (CheckPlayerHit(shootingRayHit.collider)) GetHitPlayer(shootingRayHit.collider.gameObject, activeGun.damage);

        ShootingEffects(true, false);
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
        if (CheckPlayerHit(shootingRayHit.collider)) GetHitPlayer(shootingRayHit.collider.gameObject, individualPelletDamage);

        ShootingEffects(true, true);
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

    private void ShootingEffects(bool didHit, bool hasSpread)
    {
        animator.Play("Recoil");
        if (activeGun.weaponShootingSounds.Length != 0)
        {
            weaponAudioSource.PlayOneShot(activeGun.weaponShootingSounds[Random.Range(0, activeGun.weaponShootingSounds.Length)]);
        }
        activeGunComponents.muzzleFlash.Play();
        ShootingTracer(didHit, hasSpread);

        if (didHit) HitParticle();
    }

    private void MeleeEffects(bool didHit)
    {
        animator.Play("Attack");
        if (didHit) HitParticle();
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
                print($"Hit {col.gameObject.name} of tag {col.tag} multiplier set to {damageMultiplier}");
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
        if (FilteredRaycast(playerCam.forward).collider) rb.AddForce(-playerCam.forward * activeGun.knockbackForce, ForceMode.Impulse);
    }

    private void GetHitPlayer(GameObject playerHit, float damage)
    {
        if (!NetworkManager.Singleton.Server.IsRunning) return;

        Player player = playerHit.GetComponentInParent<Player>();

        if (player.playerHealth.ReceiveDamage(damage * damageMultiplier)) playerScore.kills++;
    }

    public void FinishPlayerShooting()
    {
        CheckIfReloadIsNeeded();
        SwitchWeaponState(WeaponState.Active);
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

    public void StartGunReload()
    {
        if (ammunition == activeGun.maxAmmo || currentWeaponState == WeaponState.Reloading) return;
        isWaitingForAction = true;
        reloadCoroutine = RotateReloadGun(activeGun.reloadSpins, activeGun.reloadTime);
        StartCoroutine(reloadCoroutine);
    }

    private void StopReload()
    {
        StopCoroutine(reloadCoroutine);
        animator.enabled = true;
        SwitchWeaponState(WeaponState.Active);
    }

    #endregion

    #region GunSwitching
    public void PickStartingWeapons()
    {
        for (int i = 0; i < currentPlayerGuns.Length; i++)
        {
            currentPlayerGuns[i] = scriptablePlayer.startingGuns[i];
            currentPlayerGunsIndexes[i] = scriptablePlayer.startingWeaponsIndex[i];
            if (currentPlayerGuns[i]) GameCanvas.Instance.ChangeGunSlotIcon(((int)currentPlayerGuns[i].slot), currentPlayerGuns[i].gunIcon, currentPlayerGuns[i].gunName);
        }

        StartSlotSwitch(0);
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

    public void StartSlotSwitch(int slotIndex)
    {
        if (!currentPlayerGuns[slotIndex] || currentPlayerGuns[slotIndex] == activeGun) { print($"<color=red> NO WEAPON ON SLOT OR TRYING TO SWITCH TO SAME SLOT {!currentPlayerGuns[slotIndex]} {currentPlayerGuns[slotIndex] == activeGun} </color>"); return; }
        if (currentWeaponState == WeaponState.Reloading) StopReload();
        isWaitingForAction = true;
        StartCoroutine(SlotSwitch(slotIndex));
    }

    public void SwitchGun(int index)
    {
        print("Gets here");
        activeGunComponents = gunsComponents[index];

        barrelTip = gunsComponents[index].barrelTip;
        animator = gunsComponents[index].animator;

        if (player.IsLocal) GameCanvas.Instance.ChangeGunSlotIcon(((int)activeGun.slot), activeGun.gunIcon, activeGun.gunName);

        EnableActiveWeapon(activeGunComponents.gunSettings.weaponType);
    }

    public void SwitchMelee(int index)
    {
        activeMeleeComponents = meleesComponents[index];
        animator = meleesComponents[index].animator;
        if (player.IsLocal) GameCanvas.Instance.ChangeGunSlotIcon(((int)meleesComponents[index].meleeSettings.slot), meleesComponents[index].meleeSettings.gunIcon, activeGun.name);
        EnableActiveWeapon(WeaponType.melee);
    }

    public void PickUpGun(int slot, int pickedGunIndex)
    {
        print($"Trying to switch the weapon on slot {slot} to weapon of id {pickedGunIndex} which is {gunsComponents[pickedGunIndex].gunSettings.name}");
        Guns pickedGun = gunsComponents[pickedGunIndex].gunSettings;
        currentPlayerGuns[slot] = pickedGun;
        currentPlayerGunsIndexes[slot] = pickedGunIndex;
        StartSlotSwitch(((int)pickedGun.slot));
        ReplenishAmmo();
    }

    public void PickUpMelee(int pickedGunIndex)
    {
        Guns pickedMelee = meleesComponents[pickedGunIndex].meleeSettings;
        currentPlayerGuns[2] = pickedMelee;
        currentPlayerGunsIndexes[2] = pickedGunIndex;
        StartSlotSwitch(2);
    }

    public void FinishSwitching()
    {
        SwitchWeaponState(WeaponState.Active);
        CheckIfReloadIsNeeded();
    }
    #endregion

    public void AimDownSight(bool aim)
    {
        if (!activeGun.canAim) return;

        activeGunComponents.gunSway.ResetGunPosition();
        activeGunComponents.gunSway.enabled = !aim;
        headBobController.InstantlyResetGunPos();
        headBobController.gunCambob = !aim;
        animator.SetBool("Aiming", aim);
    }

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

        if (activeGun.weaponPickupSound) weaponAudioSource.PlayOneShot(activeGun.weaponPickupSound);

        if (weaponType != WeaponType.melee)
        {
            // Enables Gun
            activeGunComponents.gameObject.SetActive(true);

            // Places The Arms Targets On The Active Weapon
            if (player.IsLocal)
            {
                rightArmMesh.enabled = activeGunComponents.rightArmTarget;
                leftArmMesh.enabled = activeGunComponents.leftArmTarget;
            }

            if (activeGunComponents.rightArmTarget) rightArmIk.Target = activeGunComponents.rightArmTarget;
            else rightArmIk.enabled = false;
            if (activeGunComponents.leftArmTarget) leftArmIk.Target = activeGunComponents.leftArmTarget;
            else leftArmIk.enabled = false;

            // Enables The Scope If The Weapon Has One And The Player Is Local
            if (!activeGunComponents.gunSettings.canAim || !player.IsLocal) return;
            scopeCam.enabled = true;
            scopeCam.fieldOfView = activeGun.scopeFov;
            return;
        }

        // Enables Melee Weapon
        activeMeleeComponents.gameObject.SetActive(true);

        // Places The Arms Targets On The Active Weapon
        if (player.IsLocal)
        {
            rightArmMesh.enabled = activeMeleeComponents.rightArmTarget;
            leftArmMesh.enabled = activeMeleeComponents.leftArmTarget;
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

            if (!gunsComponents[i].gunSettings.canAim || !player.IsLocal) continue;

            scopeCam.enabled = false;
        }
    }

    public void DisableAllMelees()
    {
        for (int i = 0; i < meleesComponents.Length; i++)
        {
            meleesComponents[i].gameObject.SetActive(false);
        }
    }

    private void SendGunSwitch(int gunSlot)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.gunChanged);
        message.AddUShort(player.Id);
        message.AddByte((byte)gunSlot);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    public void SendPickedUpGun(int slot, int pickedGunIndex)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.pickedGun);
        message.AddUShort(player.Id);
        message.AddByte((byte)slot);
        message.AddByte((byte)pickedGunIndex);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    private void SendWeaponSync(ushort id)
    {
        if (!player) return;
        print($"Sending the guns of {player.name} to {id}");
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.weaponSync);
        message.AddUShort(player.Id);
        message.AddByte((byte)currentPlayerGunsIndexes[0]);
        message.AddByte((byte)currentPlayerGunsIndexes[1]);
        message.AddByte((byte)currentPlayerGunsIndexes[2]);

        message.AddByte((byte)activeGunSlot);
        NetworkManager.Singleton.Server.Send(message, id);
    }

    [MessageHandler((ushort)ClientToServerId.gunInput)]
    private static void GunInput(ushort fromClientId, Message message)
    {
        if (Player.list.TryGetValue(fromClientId, out Player player))
        {
            player.gunShoot.HandleClientInput(message.GetBool(), message.GetUShort());
        }
    }

    [MessageHandler((ushort)ClientToServerId.slotChange)]
    private static void ChangeWeaponSetting(ushort fromClientId, Message message)
    {
        if (Player.list.TryGetValue(fromClientId, out Player player))
        {
            player.gunShoot.StartSlotSwitch(message.GetInt());
        }
    }

    [MessageHandler((ushort)ServerToClientId.pickedGun)]
    private static void PickGun(Message message)
    {
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            player.gunShoot.PickUpGun((int)message.GetByte(), (int)message.GetByte());
        }
    }

    [MessageHandler((ushort)ServerToClientId.weaponSync)]
    private static void SyncWeapons(Message message)
    {
        print("Got the sync");
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            print("Player on list");
            player.gunShoot.PickSyncedWeapons((int)message.GetByte(), (int)message.GetByte(), (int)message.GetByte());
            player.gunShoot.StartSlotSwitch((int)message.GetByte());
        }
    }

    [MessageHandler((ushort)ClientToServerId.gunReload)]
    private static void ReloadGun(ushort fromClientId, Message message)
    {
        if (Player.list.TryGetValue(fromClientId, out Player player))
        {
            player.gunShoot.StartGunReload();
        }
    }

    [MessageHandler((ushort)ServerToClientId.gunChanged)]
    private static void ChangeGun(Message message)
    {
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            if (NetworkManager.Singleton.Server.IsRunning && player.IsLocal) return;
            player.gunShoot.StartSlotSwitch(message.GetByte());
        }
    }

    public void TiltGun(float angle, float duration)
    {
        if (tiltWeaponCoroutine != null) StopCoroutine(tiltWeaponCoroutine);
        tiltWeaponCoroutine = TiltWeapon(angle, duration);
        StartCoroutine(tiltWeaponCoroutine);
    }

    private IEnumerator SlotSwitch(int slotIndex)
    {
        while (currentWeaponState != WeaponState.Active) yield return null;
        print("Got here");
        isWaitingForAction = false;

        SwitchWeaponState(WeaponState.Switching);
        activeGunSlot = slotIndex;

        // This is saving the ammunition before changing guns
        if (activeGun) activeGun.currentAmmo = ammunition;

        // Changes guns
        activeGun = currentPlayerGuns[slotIndex];
        ammunition = activeGun.currentAmmo;

        if (activeGun.weaponType != WeaponType.melee) SwitchGun(currentPlayerGunsIndexes[slotIndex]);
        else SwitchMelee(currentPlayerGunsIndexes[slotIndex]);
        animator.Play("Raise");
        print("Got in here");
        if (NetworkManager.Singleton.Server.IsRunning) SendGunSwitch(slotIndex);
    }

    // FUCK QUATERNIONS
    public IEnumerator RotateReloadGun(int times, float duration)
    {
        while (currentWeaponState != WeaponState.Active) yield return null;
        SwitchWeaponState(WeaponState.Reloading);
        isWaitingForAction = false;

        if (activeGun.weaponSpinSound)
        {
            weaponAudioSource.clip = activeGun.weaponSpinSound;
            weaponAudioSource.loop = true;
            weaponAudioSource.Play();
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
            weaponAudioSource.Stop();
            weaponAudioSource.loop = false;
            if (activeGun.weaponReloadSound) weaponAudioSource.PlayOneShot(activeGun.weaponReloadSound);
        }

        SwitchWeaponState(WeaponState.Active);
    }

    private IEnumerator TiltWeapon(float tiltAngle, float duration)
    {
        Transform weaponTransform = activeGunComponents.transform;
        Quaternion startingAngle = weaponTransform.localRotation;
        Quaternion toAngle = Quaternion.Euler(new Vector3(0, 0, tiltAngle));
        float rotationDuration = 0;

        isWeaponTilted = !isWeaponTilted;
        while (weaponTransform.localRotation != toAngle)
        {
            weaponTransform.localRotation = Quaternion.Lerp(startingAngle, toAngle, rotationDuration / duration);
            rotationDuration += Time.deltaTime;
            yield return null;
        }
    }
}
