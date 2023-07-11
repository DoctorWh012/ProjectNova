using Riptide;
using UnityEngine;
using System.Collections;
using EZCameraShake;
using DitzelGames.FastIK;

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

    [SerializeField] private WeaponState currentWeaponState = WeaponState.Active;

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
    [SerializeField] private AudioSource playerAudioSource;

    [Header("Weapons")]
    [SerializeField] private Guns[] currentPlayerGuns;
    [SerializeField] private int[] currentPlayerGunsIndexes;
    [SerializeField] public GunComponents[] gunsComponents;
    [SerializeField] public MeleeComponents[] meleesComponents;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hitMarkerSfx;
    [SerializeField] private AudioClip spinSFX;
    [SerializeField] private AudioClip reloadSFX;

    [Header("Arms")]
    [SerializeField] private SkinnedMeshRenderer leftArmMesh;
    [SerializeField] private SkinnedMeshRenderer rightArmMesh;
    [SerializeField] private FastIKFabric leftArmTarget;
    [SerializeField] private FastIKFabric rightArmTarget;

    private IEnumerator tiltWeaponCoroutine;
    private IEnumerator reloadCoroutine;
    private GunComponents activeGunComponents;
    private MeleeComponents activeMeleeComponents;
    private Animator animator;
    private Transform barrelTip;
    private ParticleSystem weaponEffectParticle;
    private RaycastHit rayHit;
    private RaycastHit[] rayHits;
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
    public Guns activeGun;
    private int activeGunSlot;

    private ushort lastShotTick;

    private void Awake()
    {
        currentPlayerGuns = new Guns[3];
        currentPlayerGunsIndexes = new int[3];
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
    public void FireTick()
    {
        if (activeGun.weaponType == WeaponType.rifle || activeGun.weaponType == WeaponType.shotgun) VerifyGunShoot();
        else if (activeGun.weaponType == WeaponType.melee) VerifyMeleeAttack();
    }

    private void VerifyGunShoot()
    {
        if (currentWeaponState != WeaponState.Active) return;
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
        if (currentWeaponState != WeaponState.Active) return;
        if (Time.time >= nextTimeToFire)
        {
            nextTimeToFire = Time.time + 1f / activeGun.fireRate;

            if (!player.IsLocal && NetworkManager.Singleton.Server.IsRunning) GameManager.Singleton.SetAllPlayersPositionsTo(lastShotTick, player.Id);

            AttackMelee();

            if (!player.IsLocal && NetworkManager.Singleton.Server.IsRunning) GameManager.Singleton.ResetPlayersPositions(player.Id);
        }
    }

    public void HandleClientInput(bool shooting, ushort tick)
    {
        if (!shooting) return;
        lastShotTick = tick;
        FireTick();
    }

    private void Shoot()
    {
        rayHits = Physics.RaycastAll(playerCam.position, playerCam.forward, activeGun.range, ~layersToIgnoreShootRaycast);
        System.Array.Sort(rayHits, (x, y) => x.distance.CompareTo(y.distance));

        for (int i = 0; i < rayHits.Length; i++) print($"{rayHits[i].collider.name} == {rayHits[i].collider.tag}");

        // If didn't hit anything sends NoHit and returns
        if (rayHits.Length <= 0)
        {
            ShootingEffects(false, Vector2.zero, true);
            if (GameManager.Singleton.networking) SendShot(false, Vector2.zero, true);
            return;
        }

        for (int i = 0; i < rayHits.Length; i++)
        {
            // Checks if the shot didn't hit yourself
            if (CompareHitCollider(rayHits[i].collider)) continue;

            // If the first thing it hit is not a player break
            rayHit = rayHits[i];
            if (!CheckPlayerHit(rayHit.collider)) break;

            // If it's a player damages it 
            GetHitPlayer(rayHit.collider.gameObject, activeGun.damage, true);
            break;
        }

        ShootingEffects(true, Vector2.zero, true);
        if (GameManager.Singleton.networking) SendShot(true, Vector2.zero, true);
        ApplyRecoil();
    }

    private void ShotgunShoot()
    {
        float individualPelletDamage = activeGun.damage / activeGun.pellets;

        for (int i = 0; i < activeGun.pellets; i++)
        {
            bool shouldPlay = (i == activeGun.pellets - 1);
            Vector3 spread = new Vector3(activeGun.shotgunSpreadPatterns[i].x, activeGun.shotgunSpreadPatterns[i].y, activeGun.spread).normalized;

            rayHits = Physics.RaycastAll(playerCam.position, playerCam.rotation * spread, activeGun.range, ~layersToIgnoreShootRaycast);
            System.Array.Sort(rayHits, (x, y) => x.distance.CompareTo(y.distance));

            if (rayHits.Length <= 0)
            {
                ShootingEffects(false, activeGun.shotgunSpreadPatterns[i], shouldPlay);
                if (GameManager.Singleton.networking) SendShot(false, activeGun.shotgunSpreadPatterns[i], shouldPlay);
                continue;
            }

            for (int j = 0; j < rayHits.Length; j++)
            {
                // Check if the shot didn't hit yourself
                if (CompareHitCollider(rayHits[j].collider)) continue;

                // If the first thing it hit is not a player break
                rayHit = rayHits[j];
                if (!CheckPlayerHit(rayHit.collider)) break;

                // If it's a player damages it 
                GetHitPlayer(rayHit.collider.gameObject, individualPelletDamage, (i == activeGun.pellets));
                break;
            }

            ShootingEffects(true, activeGun.shotgunSpreadPatterns[i], shouldPlay);
            if (GameManager.Singleton.networking) SendShot(true, activeGun.shotgunSpreadPatterns[i], shouldPlay);
        }

        ApplyRecoil();
    }

    private void AttackMelee()
    {
        rayHits = Physics.RaycastAll(playerCam.position, playerCam.forward, activeGun.range, ~layersToIgnoreShootRaycast);
        System.Array.Sort(rayHits, (x, y) => x.distance.CompareTo(y.distance));

        if (rayHits.Length <= 0)
        {
            MeleeEffects(false);
            if (GameManager.Singleton.networking) SendMeleeAttack(false);
            return;
        }

        for (int i = 0; i < rayHits.Length; i++)
        {
            // Check if the ray didn't hit yourself
            if (CompareHitCollider(rayHits[i].collider)) continue;

            // If the first thing it hit is not a player break
            rayHit = rayHits[i];
            if (!CheckPlayerHit(rayHit.collider)) break;

            GetHitPlayer(rayHit.collider.gameObject, activeGun.damage, true);
            break;
        }

        MeleeEffects(true);
        if (GameManager.Singleton.networking) SendMeleeAttack(true);
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

    private void ApplyRecoil()
    {
        if (Physics.Raycast(playerCam.position, playerCam.forward, activeGun.maxRecoilDistance, ~layersToIgnoreShootRaycast)) rb.AddForce(-playerCam.forward * activeGun.recoilForce, ForceMode.Impulse);
    }

    private void GetHitPlayer(GameObject playerHit, float damage, bool shouldPlaySFx)
    {
        if (!NetworkManager.Singleton.Server.IsRunning) return;

        Player player = playerHit.GetComponentInParent<Player>();

        if (player.playerHealth.ReceiveDamage(damage * damageMultiplier)) playerScore.kills++;

        SendHitPlayer(player.Id, shouldPlaySFx);
    }

    private void ShootingEffects(bool didHit, Vector2 spread, bool shouldPlaySFx)
    {
        BulletTrailEffect(didHit, rayHit.point, spread);
        ShootingAnimator(shouldPlaySFx, player.IsLocal);
    }

    private void MeleeEffects(bool didHit)
    {
        MeleeAtackAnimator();
        if (didHit) HitParticle(rayHit.point);
    }

    public void FinishPlayerShooting()
    {
        SwitchWeaponState(WeaponState.Active);
        CheckIfReloadIsNeeded();
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
        if (ammunition == activeGun.maxAmmo || currentWeaponState != WeaponState.Active) return;
        reloadCoroutine = RotateReloadGun(activeGun.reloadSpins, activeGun.reloadTime);
        StartCoroutine(reloadCoroutine);
    }

    private void StopReload()
    {
        StopCoroutine(reloadCoroutine);
        SwitchWeaponState(WeaponState.Active);
        animator.enabled = true;
        animator.Play("Idle");
    }

    #endregion

    #region  GunSwitching
    public void PickStartingWeapons()
    {
        for (int i = 0; i < currentPlayerGuns.Length; i++)
        {
            currentPlayerGuns[i] = scriptablePlayer.startingGuns[i];
            currentPlayerGunsIndexes[i] = scriptablePlayer.startingWeaponsIndex[i];
            if (currentPlayerGuns[i]) GameCanvas.Instance.ChangeGunSlotIcon(((int)currentPlayerGuns[i].slot), currentPlayerGuns[i].gunIcon, currentPlayerGuns[i].gunName);
        }

        SwitchToSlot(0);
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

    public void SwitchToSlot(int slotIndex)
    {
        if (currentPlayerGuns[slotIndex] == null || currentPlayerGuns[slotIndex] == activeGun) return;

        if (currentWeaponState == WeaponState.Reloading) StopReload();

        SwitchWeaponState(WeaponState.Switching);
        activeGunSlot = slotIndex;


        // This is saving the ammunition before changing guns
        if (activeGun != null) activeGun.currentAmmo = ammunition;

        // Changes guns
        activeGun = currentPlayerGuns[slotIndex];
        ammunition = activeGun.currentAmmo;

        if (activeGun.weaponType != WeaponType.melee) SwitchGun(currentPlayerGunsIndexes[slotIndex]);
        else SwitchMelee(currentPlayerGunsIndexes[slotIndex]);

        animator.Play("Raise");
        Invoke("FinishSwitching", 0.3f);

        if (NetworkManager.Singleton.Server.IsRunning) SendGunSwitch(slotIndex);
    }

    public void SwitchGun(int index)
    {
        activeGunComponents = gunsComponents[index];

        barrelTip = gunsComponents[index].barrelTip;
        animator = gunsComponents[index].animator;
        weaponEffectParticle = gunsComponents[index].muzzleFlash;

        if (player.IsLocal) GameCanvas.Instance.ChangeGunSlotIcon(((int)activeGun.slot), activeGun.gunIcon, activeGun.gunName);

        EnableActiveGunMesh(activeGunComponents.gunSettings.weaponType);
    }

    public void SwitchMelee(int index)
    {
        activeMeleeComponents = meleesComponents[index];
        animator = meleesComponents[index].animator;
        if (player.IsLocal) GameCanvas.Instance.ChangeGunSlotIcon(((int)meleesComponents[index].meleeSettings.slot), meleesComponents[index].meleeSettings.gunIcon, activeGun.name);
        EnableActiveGunMesh(WeaponType.melee);
    }

    public void PickUpGun(int slot, int pickedGunIndex)
    {
        print($"Trying to switch the weapon on slot {slot} to weapon of id {pickedGunIndex} which is {gunsComponents[pickedGunIndex].gunSettings.name}");
        Guns pickedGun = gunsComponents[pickedGunIndex].gunSettings;
        currentPlayerGuns[slot] = pickedGun;
        currentPlayerGunsIndexes[slot] = pickedGunIndex;
        SwitchToSlot(((int)pickedGun.slot));
        ReplenishAmmo();
    }

    public void PickUpMelee(int pickedGunIndex)
    {
        Guns pickedMelee = meleesComponents[pickedGunIndex].meleeSettings;
        currentPlayerGuns[2] = pickedMelee;
        currentPlayerGunsIndexes[2] = pickedGunIndex;
        SwitchToSlot(2);
    }

    private void FinishSwitching()
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

    public void EnableActiveGunMesh(WeaponType weaponType)
    {
        DisableAllMeleeMesh();
        DisableAllGunMeshes();

        if (weaponType != WeaponType.melee)
        {
            // Enables Gun Mesh
            for (int i = 0; i < activeGunComponents.gunMesh.Length; i++) activeGunComponents.gunMesh[i].enabled = true;

            // Places The Arms Targets On The Active Weapon
            rightArmMesh.enabled = activeGunComponents.rightArmTarget;
            leftArmMesh.enabled = activeGunComponents.leftArmTarget;
            if (activeGunComponents.rightArmTarget) rightArmTarget.Target = activeGunComponents.rightArmTarget;
            if (activeGunComponents.leftArmTarget) leftArmTarget.Target = activeGunComponents.leftArmTarget;

            activeGunComponents.gunTrail.enabled = true;

            // Enables The Scope If The Weapon Has One And The Player Is Local
            if (!activeGunComponents.gunSettings.canAim || !player.IsLocal) return;
            activeGunComponents.scopeMesh.enabled = true;
            scopeCam.enabled = true;
            scopeCam.fieldOfView = activeGun.scopeFov;

            return;
        }

        // Enables Melee Weapon Mesh
        for (int i = 0; i < activeMeleeComponents.meleeMesh.Length; i++) activeMeleeComponents.meleeMesh[i].enabled = true;
        rightArmMesh.enabled = activeMeleeComponents.rightArmTarget;
        leftArmMesh.enabled = activeMeleeComponents.leftArmTarget;
        if (activeMeleeComponents.rightArmTarget) rightArmTarget.Target = activeMeleeComponents.rightArmTarget;
        if (activeMeleeComponents.leftArmTarget) leftArmTarget.Target = activeMeleeComponents.leftArmTarget;
    }

    public void DisableAllGunMeshes()
    {
        for (int i = 0; i < gunsComponents.Length; i++)
        {
            for (int j = 0; j < gunsComponents[i].gunMesh.Length; j++) gunsComponents[i].gunMesh[j].enabled = false;

            gunsComponents[i].gunTrail.enabled = false;

            if (!gunsComponents[i].gunSettings.canAim || !player.IsLocal) continue;

            gunsComponents[i].scopeMesh.enabled = false;
            scopeCam.enabled = false;
        }
    }

    public void DisableAllMeleeMesh()
    {
        for (int i = 0; i < meleesComponents.Length; i++)
        {
            for (int j = 0; j < meleesComponents[i].meleeMesh.Length; j++) meleesComponents[i].meleeMesh[j].enabled = false;
        }
    }

    public void ShootingAnimator(bool shouldPlay, bool playerIsLocal)
    {
        // if (shouldPlay) SoundManager.Instance.PlaySound(playerAudioSource, activeGunComponents.gunShootSounds[0]);
        if (playerIsLocal && shouldPlay) ShootShaker();
        weaponEffectParticle.Play();
        animator.Play("Recoil");
    }

    public void MeleeAtackAnimator()
    {
        animator.Play("Attack");
        SoundManager.Instance.PlaySound(playerAudioSource, activeMeleeComponents.meleeSounds[0]);
        weaponEffectParticle.Play();
    }

    public void HitParticle(Vector3 hitPos)
    {
        Instantiate(GameManager.Singleton.HitPrefab, hitPos, Quaternion.identity);
    }

    public void BulletTrailEffect(bool didHit, Vector3 hitPos, Vector2 spread)
    {
        // If the raycast hit something places the GameObject at rayHit.point
        if (didHit)
        {
            HitParticle(hitPos);
            TrailRenderer tracer = Instantiate(GameManager.Singleton.ShotTrail, barrelTip.position, Quaternion.identity);
            tracer.AddPosition(barrelTip.position);
            tracer.transform.position = hitPos;
        }

        // If it didn't hit something just moves the GameObject foward
        else
        {
            TrailRenderer tracer = Instantiate(GameManager.Singleton.ShotTrail, barrelTip.position, Quaternion.LookRotation(barrelTip.forward));
            tracer.AddPosition(barrelTip.position);
            if (spread != Vector2.zero) tracer.transform.position += (barrelTip.rotation * new Vector3(spread.x, spread.y, activeGun.spread)) * activeGun.range;
            tracer.transform.position += (barrelTip.forward * activeGun.range);

            //Fix Needed
        }
    }

    private void HitEffect(Vector3 position)
    {
        ParticleSystem hitEffect = Instantiate(GameManager.Singleton.PlayerHitPrefab, position, Quaternion.identity);
    }

    private void PlayHitmarker(bool shouldPlay)
    {
        if (shouldPlay) SoundManager.Instance.PlaySound(audioSource, hitMarkerSfx);
    }

    private void ShootShaker()
    {
        for (int i = 0; i < activeGunComponents.shakeAmmount; i++)
        {
            CameraShaker.Instance.ShakeOnce(activeGunComponents.shakeIntensity, activeGunComponents.shakeRoughness, activeGunComponents.fadeinTime, activeGunComponents.fadeOutTime);
        }
    }

    // Multiplayer Handler
    private void SendShot(bool didHit, Vector2 spread, bool shouldPlaySFx)
    {
        if (!NetworkManager.Singleton.Server.IsRunning) return;
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.playerShot);
        message.AddUShort(player.Id);
        message.AddUShort((ushort)ammunition);
        message.AddBool(didHit);
        message.AddVector3(rayHit.point);
        message.AddVector2(spread);
        message.AddBool(shouldPlaySFx);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    private void SendMeleeAttack(bool didHit)
    {
        if (!NetworkManager.Singleton.Server.IsRunning) return;
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.meleeAtack);
        message.AddUShort(player.Id);
        message.AddBool(didHit);
        message.AddVector3(rayHit.point);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    private void SendHitPlayer(ushort playerHitId, bool shouldPlaySFx)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.playerHit);
        message.AddUShort(playerHitId);
        message.AddVector3(rayHit.point);
        message.AddBool(shouldPlaySFx);
        NetworkManager.Singleton.Server.SendToAll(message);
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
            player.gunShoot.SwitchToSlot(message.GetInt());
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
            player.gunShoot.SwitchToSlot((int)message.GetByte());
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

    [MessageHandler((ushort)ServerToClientId.playerShot)]
    private static void PlayerShot(Message message)
    {
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            if (NetworkManager.Singleton.Server.IsRunning) return;
            if (player.IsLocal) { player.gunShoot.ammunition = message.GetUShort(); return; }
            player.gunShoot.BulletTrailEffect(message.GetBool(), message.GetVector3(), message.GetVector2());
            player.gunShoot.ShootingAnimator(message.GetBool(), player.IsLocal);
        }
    }

    [MessageHandler((ushort)ServerToClientId.meleeAtack)]
    private static void PlayerAtackedMelee(Message message)
    {
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            if (NetworkManager.Singleton.Server.IsRunning || player.IsLocal) return;
            player.gunShoot.MeleeAtackAnimator();
            if (message.GetBool()) player.gunShoot.HitParticle(message.GetVector3());
        }
    }

    [MessageHandler((ushort)ServerToClientId.playerHit)]
    private static void PlayerHit(Message message)
    {
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            player.gunShoot.HitEffect(message.GetVector3());
            if (!player.IsLocal) player.gunShoot.PlayHitmarker(message.GetBool());
        }
    }

    [MessageHandler((ushort)ServerToClientId.gunChanged)]
    private static void ChangeGun(Message message)
    {
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            if (NetworkManager.Singleton.Server.IsRunning && player.IsLocal) return;
            player.gunShoot.SwitchToSlot(message.GetByte());
        }
    }

    public void TiltGun(float angle, float duration)
    {
        if (tiltWeaponCoroutine != null) StopCoroutine(tiltWeaponCoroutine);
        tiltWeaponCoroutine = TiltWeapon(angle, duration);
        StartCoroutine(tiltWeaponCoroutine);
    }

    public IEnumerator RotateReloadGun(int times, float duration)
    {
        // while (currentWeaponState == WeaponState.Shooting) yield return null;

        SwitchWeaponState(WeaponState.Reloading);
        // FUCK QUATERNIONS
        activeGunComponents.animator.enabled = false;

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
        activeGunComponents.animator.enabled = true;
        ReplenishAmmo();
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