using Riptide;
using UnityEngine;
using System.Collections;
using EZCameraShake;

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
                x = 2f * Random.value - 1f;
                y = 2f * Random.value - 1f;
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
    public int activeGunIndex { get; private set; }
    public bool isWeaponTilted { get; private set; } = false;
    public WeaponType activeWeaponType { get; private set; }
    public Animator animator { get; private set; }

    [Header("Components")]
    [SerializeField] private Player player;
    [SerializeField] private ScriptablePlayer scriptablePlayer;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private BoxCollider[] bodyColliders;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private PlayerScore playerScore;
    [SerializeField] private Transform playerCam;
    [SerializeField] private HeadBobController headBobController;
    [SerializeField] private Camera scopeCam;
    [SerializeField] private AudioSource playerAudioSource;

    [Header("Weapons")]
    [SerializeField] public GunComponents[] gunsComponents;
    [SerializeField] public MeleeComponents[] meleesComponents;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hitMarkerSfx;
    [SerializeField] private AudioClip spinSFX;
    [SerializeField] private AudioClip reloadSFX;

    [Header("Settings")]
    [SerializeField] private int gunSlots;

    IEnumerator tiltWeaponCoroutine;
    private Transform barrelTip;
    private ParticleSystem weaponEffectParticle;
    private RaycastHit rayHit;
    private RaycastHit[] rayHits;
    public Guns[] currentPlayerGuns;
    private float damageMultiplier;
    public int[] currentPlayerGunsIndex;
    private int _ammunition;
    public int ammunition
    {
        get { return _ammunition; }
        set
        {
            _ammunition = value;
            if (player.IsLocal) GameCanvas.Instance.UpdateAmmunition(ammunition, activeGun.maxAmmo);
            CheckIfReloadIsNeeded();
        }
    }

    private bool shootFreeze = false;

    public bool canShoot = true;
    public bool isReloading = false;
    private float nextTimeToFire = 0f;
    public Guns activeGun;

    private ushort lastShotTick;

    private void Awake()
    {
        currentPlayerGuns = new Guns[gunSlots];
        currentPlayerGunsIndex = new int[gunSlots];

        SendGunSettings(0);
        PickUpMelee(0);
        PickUpGun(0, 0);
    }

    public void FireTick()
    {
        if (!player.IsLocal && NetworkManager.Singleton.Server.IsRunning) GameManager.Singleton.SetAllPlayersPositionsTo(lastShotTick, player.Id);
        GameManager.Singleton.ActivateDeactivateAllPlayersCollisions(true);

        if (activeGun.weaponType == WeaponType.rifle || activeGun.weaponType == WeaponType.shotgun) VerifyGunShoot();
        else if (activeGun.weaponType == WeaponType.melee) VerifyMeleeAttack();

        GameManager.Singleton.ActivateDeactivateAllPlayersCollisions(false);
        if (!player.IsLocal && NetworkManager.Singleton.Server.IsRunning) GameManager.Singleton.ResetPlayersPositions(player.Id);
    }

    private void VerifyGunShoot()
    {
        if (shootFreeze) return;
        if (canShoot && ammunition > 0 && Time.time >= nextTimeToFire)
        {
            nextTimeToFire = Time.time + 1f / activeGun.fireRate;
            ammunition--;

            switch (activeGun.weaponType)
            {
                case WeaponType.rifle:
                    Shoot();
                    break;

                case WeaponType.shotgun:
                    ShotgunShoot();
                    break;
            }
        }
    }

    private void VerifyMeleeAttack()
    {
        if (canShoot && Time.time >= nextTimeToFire)
        {
            nextTimeToFire = Time.time + 1f / activeGun.fireRate;
            AttackMelee();
        }
    }

    public void HandleClientInput(bool shooting, ushort tick)
    {
        if (!shooting) return;
        lastShotTick = tick;
        FireTick();
    }

    private void CheckIfReloadIsNeeded()
    {
        if (ammunition <= 0 && !isReloading && activeGun.weaponType != WeaponType.melee) StartGunReload();
    }

    private void Shoot()
    {
        rayHits = Physics.RaycastAll(playerCam.position, playerCam.forward, activeGun.range);
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

            rayHits = Physics.RaycastAll(playerCam.position, playerCam.rotation * spread, activeGun.range);
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
        rayHits = Physics.RaycastAll(playerCam.position, playerCam.forward, activeGun.range);
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

        MeleeEffects(false);
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
        Physics.autoSimulation = false;
        playerMovement.SetPlayerKinematic(false);

        if (Physics.Raycast(playerCam.position, playerCam.forward, activeGun.maxRecoilDistance)) rb.AddForce(-playerCam.forward * activeGun.recoilForce, ForceMode.Impulse);

        Physics.Simulate(GameManager.Singleton.minTimeBetweenTicks);
        playerMovement.SetPlayerKinematic(true);
        Physics.autoSimulation = true;
    }


    public void ReplenishAmmo()
    {
        ammunition = activeGun.maxAmmo;
        canShoot = true;
    }

    public void ReplenishAllAmmo()
    {
        for (int i = 0; i < gunsComponents.Length; i++)
        {
            gunsComponents[i].gunSettings.currentAmmo = gunsComponents[i].gunSettings.maxAmmo;
        }

        ammunition = activeGun.maxAmmo;
    }

    private void GetHitPlayer(GameObject playerHit, float damage, bool shouldPlaySFx)
    {
        if (!NetworkManager.Singleton.Server.IsRunning) return;

        Player player = playerHit.GetComponentInParent<Player>();

        if (player.playerHealth.ReceiveDamage(damage * damageMultiplier)) playerScore.kills++;

        SendHitPlayer(player.Id, shouldPlaySFx);
    }

    // GUN SWITCHING
    public void SwitchGun(int slotIndex, bool shouldSwitch)
    {
        if (currentPlayerGuns[slotIndex] == null) return;

        // This is saving the ammunition before changing guns
        if (shouldSwitch) activeGun.currentAmmo = ammunition;

        // Changes guns
        activeGun = currentPlayerGuns[slotIndex];
        ammunition = activeGun.currentAmmo;

        if (!NetworkManager.Singleton.Server.IsRunning) return;

        //Checks If Its Melee
        if (slotIndex == 2) SendGunSwitch(currentPlayerGunsIndex[slotIndex], true);
        else SendGunSwitch(currentPlayerGunsIndex[slotIndex], false);
    }

    public void SwitchGun(int index)
    {
        activeGunIndex = index;
        activeWeaponType = activeGun.weaponType;
        barrelTip = gunsComponents[index].barrelTip;
        animator = gunsComponents[index].animator;
        weaponEffectParticle = gunsComponents[index].muzzleFlash;
        if (player.IsLocal) GameCanvas.Instance.ChangeGunSlotIcon(((int)activeGun.slot), activeGun.gunIcon, activeGun.gunName);
        EnableActiveGunMesh(index);
    }

    public void SwitchMelee(int index)
    {
        activeGunIndex = index;
        activeWeaponType = meleesComponents[index].meleeSettings.weaponType;
        animator = meleesComponents[index].animator;
        weaponEffectParticle = meleesComponents[index].meleeParticles;
        if (player.IsLocal) GameCanvas.Instance.ChangeGunSlotIcon(((int)meleesComponents[index].meleeSettings.slot), meleesComponents[index].meleeSettings.gunIcon, activeGun.name);
        EnableActiveGunMesh(index);
    }

    public void PickUpGun(int slot, int pickedGunIndex)
    {
        Guns pickedGun = gunsComponents[pickedGunIndex].gunSettings;
        currentPlayerGuns[slot] = pickedGun;
        currentPlayerGunsIndex[slot] = pickedGunIndex;
        SendPickedUpGun(slot, pickedGunIndex);

        SwitchGun(((int)pickedGun.slot), false);
        ReplenishAllAmmo();
    }

    public void PickUpMelee(int pickedGunIndex)
    {
        Guns pickedMelee = meleesComponents[pickedGunIndex].meleeSettings;
        currentPlayerGuns[2] = pickedMelee;
        currentPlayerGunsIndex[2] = pickedGunIndex;
    }

    public void FreezePlayerShooting(bool state)
    {
        shootFreeze = state;
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

    public void StartGunReload()
    {
        if (ammunition == activeGun.maxAmmo || isReloading) return;

        StartCoroutine(RotateGun(activeGun.reloadSpins, activeGun.reloadTime));
    }

    public void AimDownSight(bool aim)
    {
        if (!activeGun.canAim) return;

        gunsComponents[activeGunIndex].gunSway.ResetGunPosition();
        gunsComponents[activeGunIndex].gunSway.enabled = !aim;
        headBobController.InstantlyResetGunPos();
        headBobController.gunCambob = !aim;
        animator.SetBool("Aiming", aim);
    }

    public void EnableActiveGunMesh(int index)
    {
        DisableAllMeleeMesh();
        DisableAllGunMeshes();
        if (activeWeaponType != WeaponType.melee)
        {
            foreach (MeshRenderer mesh in gunsComponents[index].gunMesh) mesh.enabled = true;
            if (player.IsLocal) foreach (SkinnedMeshRenderer armMesh in gunsComponents[index].armMesh) armMesh.enabled = true;

            gunsComponents[index].gunTrail.enabled = true;

            if (!gunsComponents[index].gunSettings.canAim || !player.IsLocal) return;
            print($"Trying to activate scope for weapon {gunsComponents[index].name}");

            gunsComponents[index].scopeMesh.enabled = true;
            scopeCam.enabled = true;
            scopeCam.fieldOfView = activeGun.scopeFov;
            return;
        }

        foreach (MeshRenderer mesh in meleesComponents[index].meleeMesh)
        {
            mesh.enabled = true;
        }
        if (player.IsLocal) foreach (SkinnedMeshRenderer armMesh in meleesComponents[index].armMesh) armMesh.enabled = true;

    }

    public void DisableAllGunMeshes()
    {
        print($"Disabled all the meshes for {gameObject.name}");
        for (int i = 0; i < gunsComponents.Length; i++)
        {
            foreach (MeshRenderer mesh in gunsComponents[i].gunMesh) mesh.enabled = false;
            foreach (SkinnedMeshRenderer armMesh in gunsComponents[i].armMesh) armMesh.enabled = false;

            gunsComponents[i].gunTrail.enabled = false;

            if (!gunsComponents[i].gunSettings.canAim || !player.IsLocal) continue;
            print($"Trying to disable scope for weapon {gunsComponents[i].name}");
            
            gunsComponents[i].scopeMesh.enabled = false;
            scopeCam.enabled = false;
        }
    }

    public void DisableAllMeleeMesh()
    {
        for (int i = 0; i < meleesComponents.Length; i++)
        {
            foreach (MeshRenderer mesh in meleesComponents[i].meleeMesh) mesh.enabled = false;
            foreach (SkinnedMeshRenderer armMesh in meleesComponents[i].armMesh) armMesh.enabled = false;
        }
    }

    public void ShootingAnimator(bool shouldPlay, bool playerIsLocal)
    {
        if (shouldPlay) SoundManager.Instance.PlaySound(playerAudioSource, gunsComponents[activeGunIndex].gunShootSounds[0]);
        if (playerIsLocal && shouldPlay) ShootShaker();
        weaponEffectParticle.Play();
        animator.Play("Recoil");
    }

    public void MeleeAtackAnimator()
    {
        animator.Play("Attack");
        SoundManager.Instance.PlaySound(playerAudioSource, meleesComponents[activeGunIndex].meleeSounds[0]);
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
            tracer.transform.position += (barrelTip.rotation * new Vector3(spread.x, spread.y, activeGun.spread)) * activeGun.range;
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
        GunComponents gun = gunsComponents[activeGunIndex];
        for (int i = 0; i < gun.shakeAmmount; i++)
        {
            CameraShaker.Instance.ShakeOnce(gun.shakeIntensity, gun.shakeRoughness, gun.fadeinTime, gun.fadeOutTime);
        }
    }

    // Multiplayer Handler
    public void SendGunSettings(int index)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ClientToServerId.gunChange);
        message.AddInt(index);
        NetworkManager.Singleton.Client.Send(message);
    }

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

    private void SendGunSwitch(int gunIndex, bool isMelee)
    {
        if (GameManager.Singleton.networking)
        {
            if (!NetworkManager.Singleton.Server.IsRunning) return;
            Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.gunChanged);
            message.AddUShort(player.Id);
            message.AddByte((byte)gunIndex);
            message.AddBool(isMelee);
            NetworkManager.Singleton.Server.SendToAll(message);
        }
        else
        {
            if (isMelee) SwitchMelee(gunIndex);
            else SwitchGun(gunIndex);
        }
    }

    private void SendPickedUpGun(int slot, int pickedGunIndex)
    {
        if (!NetworkManager.Singleton.Server.IsRunning || player.IsLocal) return;
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.pickedGun);
        message.AddUShort(player.Id);
        message.AddByte((byte)slot);
        message.AddByte((byte)pickedGunIndex);
        NetworkManager.Singleton.Server.Send(message, player.Id);
    }


    [MessageHandler((ushort)ClientToServerId.gunInput)]
    private static void GunInput(ushort fromClientId, Message message)
    {
        if (Player.list.TryGetValue(fromClientId, out Player player))
        {
            player.gunShoot.HandleClientInput(message.GetBool(), message.GetUShort());
        }
    }

    [MessageHandler((ushort)ClientToServerId.gunChange)]
    private static void ChangeWeaponSetting(ushort fromClientId, Message message)
    {
        if (Player.list.TryGetValue(fromClientId, out Player player))
        {
            player.gunShoot.SwitchGun(message.GetInt(), true);
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
            int index = message.GetByte();
            if (message.GetBool()) player.gunShoot.SwitchMelee(index);
            else player.gunShoot.SwitchGun(index);
        }
    }

    public void TiltGun(float angle, float duration)
    {
        if (tiltWeaponCoroutine != null) StopCoroutine(tiltWeaponCoroutine);
        tiltWeaponCoroutine = TiltWeapon(angle, duration);
        StartCoroutine(tiltWeaponCoroutine);
    }

    public IEnumerator RotateGun(int times, float duration)
    {
        isReloading = true;
        canShoot = false;
        // FUCK QUATERNIONS
        GunComponents actvGun = gunsComponents[activeGunIndex];
        yield return new WaitForEndOfFrame();
        while (animator.GetCurrentAnimatorStateInfo(0).IsName("Recoil")) yield return null;

        SoundManager.Instance.PlaySound(audioSource, spinSFX);
        actvGun.animator.enabled = false;

        Vector3 startingAngle = actvGun.gunModelPos.localEulerAngles;
        float toAngle = startingAngle.x + -360 * times;
        float t = 0;

        while (t < duration)
        {
            t += Time.deltaTime;
            float xRot = Mathf.Lerp(startingAngle.x, toAngle, t / duration);
            actvGun.gunModelPos.localEulerAngles = new Vector3(xRot, startingAngle.y, startingAngle.z);
            yield return null;
        }

        actvGun.gunModelPos.localEulerAngles = startingAngle;
        actvGun.animator.enabled = true;

        SoundManager.Instance.PlaySound(audioSource, reloadSFX);
        yield return new WaitForSeconds(0.4f);
        audioSource.Stop();

        isReloading = false;
        ReplenishAmmo();
    }

    private IEnumerator TiltWeapon(float tiltAngle, float duration)
    {
        Transform weaponTransform = gunsComponents[activeGunIndex].transform;
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