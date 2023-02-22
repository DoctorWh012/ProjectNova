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
    private Guns[] currentPlayerGuns;
    private int[] currentPlayerGunsIndex;
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
    public bool shootInput;
    private bool canShoot = true;
    private bool isReloading = false;
    private float nextTimeToFire = 0f;
    public Guns activeGun;

    // Start is called before the first frame update
    private void Awake()
    {
        // if (!NetworkManager.Singleton.Server.IsRunning) { this.enabled = false; return; }
        currentPlayerGuns = new Guns[gunSlots];
        currentPlayerGunsIndex = new int[gunSlots];

        PickUpMelee(0);
        PickUpGun(0, 0);
    }

    // Update is called once per frame
    private void Update()
    {
        if (activeGun.weaponType == WeaponType.rifle || activeGun.weaponType == WeaponType.shotgun)
        {
            GetGunInput();
            CheckIfReloadIsNeeded();
        }
        else if (activeGun.weaponType == WeaponType.melee)
        {
            GetMeleeInput();
        }
    }

    private void GetGunInput()
    {
        if (shootFreeze) return;
        if (shootInput && canShoot && ammunition > 0 && Time.time >= nextTimeToFire)
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

    private void GetMeleeInput()
    {
        if (shootInput && canShoot && Time.time >= nextTimeToFire)
        {
            nextTimeToFire = Time.time + 1f / activeGun.fireRate;
            AtackMelee();
        }
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

        if (rayHits.Length <= 0) { SendShot(false, Vector2.zero, true); return; }

        for (int i = 0; i < rayHits.Length; i++)
        {
            if (rayHits[i].collider == col) continue;
            rayHit = rayHits[i];
            if (!rayHits[i].collider.CompareTag(playerTag)) break;

            GetHitPlayer(rayHits[i].collider.gameObject, activeGun.damage, true);
            break;
        }
        SendShot(true, Vector2.zero, true);
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

            rayHits = Physics.RaycastAll(playerCam.position, finalSpread + playerCam.forward, activeGun.range);
            System.Array.Sort(rayHits, (x, y) => x.distance.CompareTo(y.distance));

            if (rayHits.Length <= 0) { SendShot(false, new Vector2(spreadX, spreadY), shouldPlay); continue; }
            for (int j = 0; j < rayHits.Length; j++)
            {
                if (rayHits[j].collider == col) continue;
                rayHit = rayHits[j];
                if (!rayHits[j].collider.CompareTag(playerTag)) break;
                GetHitPlayer(rayHits[j].collider.gameObject, individualPelletDamage, (i == activeGun.pellets));
                break;
            }
            SendShot(true, new Vector2(spreadX, spreadY), shouldPlay);
        }
        ApplyRecoil();
    }

    private void ApplyRecoil()
    {
        if (Physics.Raycast(playerCam.position, playerCam.forward, activeGun.maxRecoilDistance))
            rb.AddForce(-playerCam.forward * activeGun.recoilForce, ForceMode.Impulse);
    }

    private void AtackMelee()
    {
        rayHits = Physics.RaycastAll(playerCam.position, playerCam.forward, activeGun.range);
        System.Array.Sort(rayHits, (x, y) => x.distance.CompareTo(y.distance));

        if (rayHits.Length <= 0) { SendMeleeAttack(false); return; }
        for (int i = 0; i < rayHits.Length; i++)
        {
            if (rayHits[i].collider == col) continue;
            rayHit = rayHits[i];
            if (!rayHits[i].collider.CompareTag(playerTag)) break;
            GetHitPlayer(rayHits[i].collider.gameObject, activeGun.damage, true);
            break;
        }
        SendMeleeAttack(true);
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
        if (!state) shootInput = false;
    }

    // Multiplayer Handler

    private void SendShot(bool didHit, Vector2 spread, bool shouldPlaySFx)
    {
        if (GameManager.Singleton.networking)
        {
            Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.playerShot);
            message.AddUShort(player.Id);
            message.AddBool(didHit);
            message.AddVector3(rayHit.point);
            message.AddVector2(spread);
            message.AddBool(shouldPlaySFx);
            NetworkManager.Singleton.Server.SendToAll(message);
        }
        else
        {
            player.playerShooting.BulletTrailEffect(didHit, rayHit.point, spread);
            player.playerShooting.ShootingAnimator(shouldPlaySFx, player.IsLocal);
        }
    }

    private void SendMeleeAttack(bool didHit)
    {
        if (GameManager.Singleton.networking)
        {
            Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.meleeAtack);
            message.AddUShort(player.Id);
            message.AddBool(didHit);
            message.AddVector3(rayHit.point);
            NetworkManager.Singleton.Server.SendToAll(message);
        }
        else
        {
            player.playerShooting.MeleeAtackAnimator();
            if (didHit) player.playerShooting.HitParticle(rayHit.point);
        }

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
            player.GunShoot.shootInput = message.GetBool();
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