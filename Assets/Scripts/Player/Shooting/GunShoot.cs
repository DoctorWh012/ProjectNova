using Riptide;
using UnityEngine;
using System.Collections;

public class GunShoot : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Player player;
    [SerializeField] private CapsuleCollider col;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private PlayerScore playerScore;
    [SerializeField] private Transform playerCam;
    [SerializeField] private PlayerShooting playerShooting;

    [Header("Settings")]
    [SerializeField] private int gunSlots;
    [SerializeField] private string playerTag;

    private RaycastHit rayHit;
    private RaycastHit[] rayHits;
    public Guns[] currentPlayerGuns;
    public int[] currentPlayerGunsIndex;
    private int _ammunition;
    private int ammunition
    {
        get { return _ammunition; }
        set
        {
            _ammunition = value;
            SendUpdatedAmmo();
        }
    }

    private bool shootFreeze = false;

    private bool canShoot = true;
    private bool isReloading = false;
    private float nextTimeToFire = 0f;
    public Guns activeGun;

    private ushort lastShotTick;

    // Start is called before the first frame update
    private void Awake()
    {
        currentPlayerGuns = new Guns[gunSlots];
        currentPlayerGunsIndex = new int[gunSlots];

        PickUpMelee(0);
        PickUpGun(0, 0);
    }

    public void FireTick()
    {
        if (!player.IsLocal && NetworkManager.Singleton.Server.IsRunning) GameManager.Singleton.SetAllPlayersPositionsTo(lastShotTick, player.Id);

        GameManager.Singleton.ActivateDeactivateAllPlayersCollisions(true);

        if (activeGun.weaponType == WeaponType.rifle || activeGun.weaponType == WeaponType.shotgun)
        {
            VerifyGunShoot();
            CheckIfReloadIsNeeded();
        }

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
        if (ammunition <= 0 && !isReloading && activeGun.weaponType != WeaponType.melee)
        {
            StartGunReload(activeGun.reloadSpins, activeGun.reloadTime);
        }
    }

    private void Shoot()
    {
        rayHits = Physics.RaycastAll(playerCam.position, playerCam.forward, activeGun.range);
        System.Array.Sort(rayHits, (x, y) => x.distance.CompareTo(y.distance));

        if (rayHits.Length <= 0)
        {
            ShootingEffects(false, Vector2.zero, true);
            if (GameManager.Singleton.networking) SendShot(false, Vector2.zero, true);
            return;
        }

        for (int i = 0; i < rayHits.Length; i++)
        {
            // Checks if the shot didn't hit yourself
            if (rayHits[i].collider == col) continue;

            // If the first thing it hit is not a player break
            rayHit = rayHits[i];
            if (!rayHits[i].collider.CompareTag(playerTag)) break;

            // If it's a player damages it 
            GetHitPlayer(rayHits[i].collider.gameObject, activeGun.damage, true);
            break;
        }

        ShootingEffects(true, Vector2.zero, true);
        if (GameManager.Singleton.networking) SendShot(true, Vector2.zero, true);
        ApplyRecoil();
    }

    private void ShotgunShoot()
    {
        int individualPelletDamage = activeGun.damage / activeGun.pellets;

        float spreadX = 0;
        float spreadY = 0;

        for (int i = 0; i < activeGun.pellets; i++)
        {
            bool shouldPlay = (i == activeGun.pellets - 1);
            spreadX = Random.Range(-activeGun.spread, activeGun.spread);
            spreadY = Random.Range(-activeGun.spread, activeGun.spread);
            Vector3 finalSpread = new Vector3(spreadX, spreadY, 0);

            rayHits = Physics.RaycastAll(playerCam.position, playerCam.forward + finalSpread, activeGun.range);
            System.Array.Sort(rayHits, (x, y) => x.distance.CompareTo(y.distance));

            if (rayHits.Length <= 0)
            {
                ShootingEffects(false, new Vector2(spreadX, spreadY), shouldPlay);
                if (GameManager.Singleton.networking) SendShot(false, new Vector2(spreadX, spreadY), shouldPlay);
                continue;
            }

            for (int j = 0; j < rayHits.Length; j++)
            {
                if (rayHits[j].collider == col) continue;
                rayHit = rayHits[j];
                if (!rayHits[j].collider.CompareTag(playerTag)) break;
                GetHitPlayer(rayHits[j].collider.gameObject, individualPelletDamage, (i == activeGun.pellets));
                break;
            }

            ShootingEffects(true, new Vector2(spreadX, spreadY), shouldPlay);
            if (GameManager.Singleton.networking) SendShot(true, new Vector2(spreadX, spreadY), shouldPlay);
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
            if (rayHits[i].collider == col) continue;

            rayHit = rayHits[i];
            if (!rayHits[i].collider.CompareTag(playerTag)) break;

            GetHitPlayer(rayHits[i].collider.gameObject, activeGun.damage, true);
            break;
        }

        MeleeEffects(false);
        if (GameManager.Singleton.networking) SendMeleeAttack(true);
    }

    private void ApplyRecoil()
    {
        Physics.autoSimulation = false;
        player.Movement.SetPlayerKinematic(false);

        if (Physics.Raycast(playerCam.position, playerCam.forward, activeGun.maxRecoilDistance)) { rb.AddForce(-playerCam.forward * activeGun.recoilForce, ForceMode.Impulse); }

        Physics.Simulate(GameManager.Singleton.minTimeBetweenTicks);
        player.Movement.SetPlayerKinematic(true);
        Physics.autoSimulation = true;
    }


    private void ReplenishAmmo()
    {
        ammunition = activeGun.maxAmmo;
        canShoot = true;
    }

    public void ReplenishAllAmmo()
    {
        foreach (GunComponents gun in playerShooting.gunsSettings)
        {
            gun.gunSettings.currentAmmo = gun.gunSettings.maxAmmo;
        }

        ammunition = activeGun.maxAmmo;
    }

    private void GetHitPlayer(GameObject playerHit, int damage, bool shouldPlaySFx)
    {
        if (!NetworkManager.Singleton.Server.IsRunning) return;

        ServerPlayerHealth playerHealth = playerHit.GetComponentInParent<ServerPlayerHealth>();
        ushort playerHitId = playerHit.GetComponentInParent<Player>().Id;

        if (playerHealth.ReceiveDamage(damage)) playerScore.kills++;

        SendHitPlayer(playerHitId, shouldPlaySFx);
    }

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

    public void PickUpGun(int slot, int pickedGunIndex)
    {
        Guns pickedGun = playerShooting.gunsSettings[pickedGunIndex].gunSettings;
        currentPlayerGuns[slot] = pickedGun;
        currentPlayerGunsIndex[slot] = pickedGunIndex;

        SwitchGun(((int)pickedGun.slot), false);
        ReplenishAllAmmo();
    }

    public void PickUpMelee(int pickedGunIndex)
    {
        Guns pickedMelee = playerShooting.meleeSettings[pickedGunIndex].meleeSettings;
        currentPlayerGuns[2] = pickedMelee;
        currentPlayerGunsIndex[2] = pickedGunIndex;
    }

    public void FreezePlayerShooting(bool state)
    {
        shootFreeze = state;
    }

    private void ShootingEffects(bool didHit, Vector2 spread, bool shouldPlaySFx)
    {
        playerShooting.BulletTrailEffect(didHit, rayHit.point, spread);
        playerShooting.ShootingAnimator(shouldPlaySFx, player.IsLocal);
    }

    private void MeleeEffects(bool didHit)
    {
        playerShooting.MeleeAtackAnimator();
        if (didHit) playerShooting.HitParticle(rayHit.point);
    }

    // Multiplayer Handler
    private void SendShot(bool didHit, Vector2 spread, bool shouldPlaySFx)
    {
        if (!NetworkManager.Singleton.Server.IsRunning) return;
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.playerShot);
        message.AddUShort(player.Id);
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
        if (GameManager.Singleton == null) return;
        if (GameManager.Singleton.networking)
        {
            if (!NetworkManager.Singleton.Server.IsRunning) return;
            Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.gunChanged);
            message.AddUShort(player.Id);
            message.AddBool(isMelee);
            message.AddInt(gunIndex);
            message.AddInt(activeGun.range);
            NetworkManager.Singleton.Server.SendToAll(message);
        }
        else
        {
            if (isMelee) player.playerShooting.SwitchMelee(gunIndex);
            else player.playerShooting.SwitchGun(gunIndex);
            player.playerShooting.range = activeGun.range;
        }
    }

    public void StartGunReload(int spins, float reloadTime)
    {
        if (player.GunShoot.ammunition == player.GunShoot.activeGun.maxAmmo) return;
        StartCoroutine(player.GunShoot.StartReload(reloadTime));

        if (GameManager.Singleton.networking)
        {
            if (!NetworkManager.Singleton.Server.IsRunning) return;
            Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.gunReload);
            message.AddUShort(player.Id);
            message.AddInt(spins);
            message.AddFloat(reloadTime + 0.1f);
            NetworkManager.Singleton.Server.Send(message, player.Id);
        }
        else player.playerShooting.DoTheSpin(spins, reloadTime);
    }

    private void SendUpdatedAmmo()
    {
        if (GameManager.Singleton == null) return;
        if (GameManager.Singleton.networking)
        {
            if (!NetworkManager.Singleton.Server.IsRunning) return;
            Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.ammoChanged);
            message.AddInt(ammunition);
            message.AddInt(activeGun.maxAmmo);
            NetworkManager.Singleton.Server.Send(message, player.Id);
        }
        else GameCanvas.Instance.UpdateAmmunition(ammunition, activeGun.maxAmmo);
    }


    #region Messages

    [MessageHandler((ushort)ClientToServerId.gunInput)]
    private static void GunInput(ushort fromClientId, Message message)
    {
        if (Player.list.TryGetValue(fromClientId, out Player player))
        {
            player.GunShoot.HandleClientInput(message.GetBool(), message.GetUShort());
        }
    }

    [MessageHandler((ushort)ClientToServerId.gunChange)]
    private static void ChangeWeaponSetting(ushort fromClientId, Message message)
    {
        if (Player.list.TryGetValue(fromClientId, out Player player))
        {
            player.GunShoot.SwitchGun(message.GetInt(), true);
        }
    }

    [MessageHandler((ushort)ClientToServerId.gunReload)]
    private static void ReloadGun(ushort fromClientId, Message message)
    {
        if (Player.list.TryGetValue(fromClientId, out Player player))
        {
            player.GunShoot.StartGunReload(player.GunShoot.activeGun.reloadSpins, player.GunShoot.activeGun.reloadTime);
        }
    }
    #endregion

    IEnumerator StartReload(float reloadTime)
    {
        isReloading = true;
        canShoot = false;
        yield return new WaitForSeconds(reloadTime);
        isReloading = false;
        ReplenishAmmo();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawRay(playerCam.position, playerCam.forward * 50);
    }
}