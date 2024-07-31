using System;
using System.Collections.Generic;
using Riptide;
using UnityEngine;
using DitzelGames.FastIK;

[Serializable]
public struct ArmMeshIK
{
    public SkinnedMeshRenderer handMesh;
    public SkinnedMeshRenderer lowerArmMesh;
    public SkinnedMeshRenderer upperArmMesh;

    public FastIKFabric handIK;
}

[Serializable]
public struct BodyIK
{
    public Transform bodyPart;
    public float rotateSpeed;
}

public class PlayerShooting : MonoBehaviour
{
    [Header("Components")]
    [Space(5)]
    [SerializeField] private Player player;
    [SerializeField] public PlayerHud playerHud;
    [SerializeField] public ScriptablePlayer scriptablePlayer;
    [SerializeField] public BoxCollider[] bodyColliders;
    [SerializeField] public LayerMask layersToIgnoreShootRaycast;
    [SerializeField] public LayerMask obstacleLayers;
    [SerializeField] public LayerMask playersLayer;
    [SerializeField] public Rigidbody rb;
    [SerializeField] public Transform playerCam;
    [SerializeField] public PlayerHealth playerHealth;
    [SerializeField] private PlayerMovement playerMovement;

    [Header("Materials")]
    [SerializeField] public Material ultGlowMat;
    [SerializeField] public Color fadedUltColor;

    [Header("IK")]
    [Space(5)]
    [SerializeField] private BodyIK playerRoot;
    [SerializeField] private BodyIK playerHead;
    [SerializeField] private BodyIK playerTorso;

    [Header("Weapons")]
    [Space(5)]
    [SerializeField] public BaseWeapon[] weapons;
    [SerializeField] private BaseWeapon[] currentWeapons;
    [SerializeField] public BaseWeapon currentWeapon;

    [Header("Arms")]
    [Space(5)]
    [SerializeField] public Animator armsAnimator;
    [SerializeField] public ArmMeshIK leftArm;
    [SerializeField] public ArmMeshIK rightArm;
    private bool renderArms;

    [Header("Debugging Serialized")]
    [Space(5)]

    [HideInInspector] public int playerLayer;
    [HideInInspector] public int netPlayerLayer;

    public uint lastShotTick = 0;
    public uint lastAltFireTick = 0;
    public uint lastAltFireConfirmationTick = 0;
    public uint lastWeaponKillsTick = 0;
    public uint lastReloadTick = 0;
    private uint lastSlotChangeTick = 0;
    private bool weaponTilted;

    private void Awake()
    {
        SettingsManager.updatedPlayerPrefs += GetPreferences;
        playerLayer = LayerMask.NameToLayer("Player");
        netPlayerLayer = LayerMask.NameToLayer("NetPlayer");
        currentWeapons = new BaseWeapon[3];
    }

    private void Start()
    {
        if (player.IsLocal) GetPreferences();
        if (NetworkManager.Singleton.Server.IsRunning) PickStartingWeapons();
    }

    private void OnDestroy()
    {
        SettingsManager.updatedPlayerPrefs -= GetPreferences;
    }

    private void OnApplicationQuit()
    {
        SettingsManager.updatedPlayerPrefs -= GetPreferences;
    }

    private void GetPreferences()
    {
        if (!player.IsLocal) return;
        renderArms = SettingsManager.playerPreferences.renderArms;

        if (!currentWeapon) return;

        bool leftArm = renderArms ? currentWeapon.leftHandTarget : false;
        bool rightArm = renderArms ? currentWeapon.rightHandTarget : false;

        EnableDisableHandsMeshes(leftArm, rightArm);
    }

    private void Update()
    {
        if (playerHealth.currentPlayerState == PlayerState.Dead || !player.IsLocal || !GameManager.Focused) return;

        GetInput();
    }

    private void LateUpdate()
    {
        BodyInverseKinematics();
    }

    private void GetInput()
    {
        if (Input.GetKey(SettingsManager.playerPreferences.fireBtn)) currentWeapon.PrimaryAction(NetworkManager.Singleton.serverTick);
        if (Input.GetKey(SettingsManager.playerPreferences.altFireBtn)) currentWeapon.SecondaryAction(NetworkManager.Singleton.serverTick);

        GunSwitchInput(SettingsManager.playerPreferences.primarySlotKey, 0);
        GunSwitchInput(SettingsManager.playerPreferences.secondarySlotKey, 1);
        GunSwitchInput(SettingsManager.playerPreferences.tertiarySlotKey, 2);

        if (Input.GetKeyDown(SettingsManager.playerPreferences.reloadKey)) currentWeapon.Reload();
    }

    private void GunSwitchInput(KeyCode keybind, int index)
    {
        if (Input.GetKeyDown(keybind)) SlotSwitch(index, NetworkManager.Singleton.serverTick);
    }

    public void PlayerDied()
    {
        currentWeapon.DeactivateWeapon();
        currentWeapons = new BaseWeapon[3];
        currentWeapon = null;
    }

    public void PlayerRespawned()
    {
        if (!NetworkManager.Singleton.Server.IsRunning) return;

        ReplenishAllAmmo();
        PickStartingWeapons();
    }

    #region Shooting
    private void HandleClientFired(int slot, uint tick)
    {
        bool compensatingForSwitch = tick <= lastSlotChangeTick && currentWeapon != currentWeapons[slot];
        int previousSlot = (int)currentWeapon.slot;

        if (compensatingForSwitch) currentWeapon = currentWeapons[slot];

        currentWeapon.PrimaryAction(tick, compensatingForSwitch);

        if (compensatingForSwitch) currentWeapon = currentWeapons[previousSlot];
    }

    private void HandleClientAltFire(int slot, uint tick)
    {
        if (tick <= lastAltFireTick) return;
        if ((int)currentWeapon.slot != slot) return;

        currentWeapon.SecondaryAction(tick);
    }

    private void HandleClientAltFireConfirm(int slot, uint tick)
    {
        if (tick <= lastAltFireConfirmationTick) return;
        if ((int)currentWeapon.slot != slot) return;

        lastAltFireConfirmationTick = tick;
    }

    private void HandleServerPlayerHit(ushort victimId, ushort damage, bool critical, bool damaged)
    {
        currentWeapon.HitPlayerEffects(critical);
        if (damaged && Player.list.TryGetValue(victimId, out Player player))
        {
            player.playerHealth.PlayerHitEffects(damage, critical);
        }
    }

    private void HandleServerWeaponKill(int slot, int kills, uint tick)
    {
        if (tick <= lastWeaponKillsTick) return;
        if ((int)currentWeapon.slot != slot) return;

        currentWeapon.HandleServerWeaponKill(kills, tick);
    }

    private List<DebugGhost> debugGhosts = new List<DebugGhost>();
    private void ClearGhosts()
    {
        foreach (DebugGhost debugGhost in debugGhosts) Destroy(debugGhost.gameObject);
        debugGhosts.Clear();
    }

    private void CreateDebugGhosts(Player player, bool rewound, uint tick)
    {
        DebugGhost debugGhost = Instantiate(NetworkManager.Singleton.debugGhost, player.playerMovement.playerCharacter.position, player.playerMovement.playerCharacter.rotation);
        debugGhost.SetupGhost(rewound, tick);
        debugGhosts.Add(debugGhost);
    }
    #endregion

    #region Reloading
    public void ReplenishAllAmmo()
    {
        for (int i = 0; i < weapons.Length; i++) weapons[i].currentAmmo = weapons[i].maxAmmo;
    }

    private void HandleClientReload(int slot, uint tick)
    {
        if (tick <= lastReloadTick || (int)currentWeapon.slot != slot) return;
        lastReloadTick = tick;

        currentWeapon.Reload();
    }
    #endregion

    #region GunSwitching
    public void PickStartingWeapons()
    {
        if (player.IsLocal) playerHud.ResetWeaponsOnSlots();
        if (currentWeapon) currentWeapon.DeactivateWeapon();

        currentWeapons = new BaseWeapon[3];
        currentWeapon = null;

        for (int i = 0; i < currentWeapons.Length; i++)
        {
            if (scriptablePlayer.startingWeaponsIndex[i] == -1) continue;
            if ((int)weapons[scriptablePlayer.startingWeaponsIndex[i]].slot != i) continue;

            currentWeapons[i] = weapons[scriptablePlayer.startingWeaponsIndex[i]];
            currentWeapons[i].OnWeaponPickUp();
        }

        int slot = currentWeapons[0] ? 0 : currentWeapons[1] ? 1 : 2;

        SlotSwitch(slot, NetworkManager.Singleton.serverTick);
    }

    private void HandleServerWeaponSwitch(int primaryIndex, int secondaryIndex, int tertiaryIndex, int slot, int ammo, int kills, uint tick)
    {
        if (tick <= lastSlotChangeTick) return;

        if (primaryIndex != -1) currentWeapons[0] = weapons[primaryIndex];
        if (secondaryIndex != -1) currentWeapons[1] = weapons[secondaryIndex];
        if (tertiaryIndex != -1) currentWeapons[2] = weapons[tertiaryIndex];

        currentWeapons[slot].currentAmmo = ammo;
        currentWeapons[slot].killsPerformed = kills;
        SlotSwitch(slot, tick, true);
    }

    private void HandleClientWeaponSwitch(uint tick, int slot)
    {
        if (tick < lastSlotChangeTick) return;
        SlotSwitch(slot, tick);
    }

    public void SlotSwitch(int slotIndex, uint tick, bool askedByServer = false)
    {
        if (!currentWeapons[slotIndex] || currentWeapons[slotIndex] == currentWeapon) return;

        if (currentWeapon) currentWeapon.DeactivateWeapon();
        currentWeapon = currentWeapons[slotIndex];
        currentWeapon.ActivateWeapon();

        lastSlotChangeTick = tick;

        if (NetworkManager.Singleton.Server.IsRunning) SendGunSwitch();
        else if (player.IsLocal && !askedByServer) SendSlotSwitch();
    }

    public void PickUpGun(int pickedGunIndex, uint tick)
    {
        BaseWeapon weapon = weapons[pickedGunIndex];
        if (weapon != currentWeapons[(int)weapon.slot])
        {
            currentWeapons[(int)weapon.slot] = weapon;
            weapon.OnWeaponPickUp();
        }
        currentWeapons[(int)weapon.slot].ReplenishAmmo();
        SlotSwitch((int)weapon.slot, tick);
    }
    #endregion

    #region IKStuff
    public void EnableDisableHandsMeshes(bool leftState, bool rightState)
    {
        leftArm.handMesh.enabled = leftState;
        leftArm.lowerArmMesh.enabled = leftState;
        leftArm.upperArmMesh.enabled = leftState;


        rightArm.handMesh.enabled = rightState;
        rightArm.lowerArmMesh.enabled = rightState;
        rightArm.upperArmMesh.enabled = rightState;
    }

    private Vector3 rootForward;
    private Vector3 torsoForward;
    private Vector3 headForward;

    private void BodyInverseKinematics()
    {
        playerRoot.bodyPart.forward = rootForward;
        if (playerMovement.moveDir != Vector3.zero) playerRoot.bodyPart.forward = Vector3.Lerp(playerRoot.bodyPart.forward, -playerMovement.moveDir, playerRoot.rotateSpeed * Time.deltaTime);

        playerTorso.bodyPart.forward = torsoForward;
        playerHead.bodyPart.forward = headForward;

        playerHead.bodyPart.forward = Vector3.Lerp(playerHead.bodyPart.forward, -playerCam.forward * 30, playerHead.rotateSpeed * Time.deltaTime);

        playerTorso.bodyPart.forward = Vector3.Lerp(playerTorso.bodyPart.forward, -playerCam.forward, playerTorso.rotateSpeed * Time.deltaTime);
        if (playerTorso.bodyPart.forward.y * 90f > 25) playerTorso.bodyPart.forward = new Vector3(playerTorso.bodyPart.forward.x, 25f / 90f, playerTorso.bodyPart.forward.z);
        if (playerTorso.bodyPart.forward.y * 90f < -25) playerTorso.bodyPart.forward = new Vector3(playerTorso.bodyPart.forward.x, -25f / 90f, playerTorso.bodyPart.forward.z);

        rootForward = playerRoot.bodyPart.forward;
        torsoForward = playerTorso.bodyPart.forward;
        headForward = playerHead.bodyPart.forward;
    }
    #endregion

    #region ServerSenders
    public void SendServerFire()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.playerFired);
        message.AddUShort(player.Id);
        message.AddByte((byte)currentWeapon.slot);
        message.AddUInt(lastShotTick);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    public void SendServerAltFire()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.playerAltFire);
        message.AddUShort(player.Id);
        message.AddByte((byte)currentWeapon.slot);
        message.AddUInt(lastAltFireTick);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    public void SendServerAltFireConfirmation()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.playerAltFireConfirmation);
        message.AddUShort(player.Id);
        message.AddByte((byte)currentWeapon.slot);
        message.AddUInt(lastAltFireConfirmationTick);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    public void SendHitPlayer(ushort victimId, int damage, bool critical, bool damaged)
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.playerHit);
        message.AddUShort(victimId);
        message.AddUShort((ushort)damage);
        message.AddBool(critical);
        message.AddBool(damaged);
        NetworkManager.Singleton.Server.Send(message, player.Id);
    }

    public void SendWeaponKill(int killCount)
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.weaponKill);
        message.AddUShort(player.Id);
        message.AddByte((byte)currentWeapon.slot);
        message.AddInt(killCount);
        message.AddUInt(lastWeaponKillsTick);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    private void SendGunSwitch()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.gunChanged);
        message.AddUShort(player.Id);

        message.AddSByte((sbyte)Array.IndexOf(weapons, currentWeapons[0]));
        message.AddSByte((sbyte)Array.IndexOf(weapons, currentWeapons[1]));
        message.AddSByte((sbyte)Array.IndexOf(weapons, currentWeapons[2]));


        message.AddByte((byte)currentWeapon.slot);
        message.AddUShort((ushort)currentWeapon.currentAmmo);
        message.AddInt(currentWeapon.killsPerformed);

        message.AddUInt(lastSlotChangeTick);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    public void SendWeaponSyncToPlayer(ushort receiverId)
    {
        if (!currentWeapon) return;
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.gunChanged);
        message.AddUShort(player.Id);

        message.AddSByte((sbyte)Array.IndexOf(weapons, currentWeapons[0]));
        message.AddSByte((sbyte)Array.IndexOf(weapons, currentWeapons[1]));
        message.AddSByte((sbyte)Array.IndexOf(weapons, currentWeapons[2]));


        message.AddByte((byte)currentWeapon.slot);
        message.AddUShort((ushort)currentWeapon.currentAmmo);
        message.AddInt(currentWeapon.killsPerformed);

        message.AddUInt(lastSlotChangeTick);
        NetworkManager.Singleton.Server.Send(message, receiverId);
    }

    public void SendReloading()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.gunReloading);
        message.AddUShort(player.Id);
        message.AddByte((byte)currentWeapon.slot);
        message.AddUInt(lastReloadTick);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    // public void SendPickedUpGun(int pickedGunIndex)
    // {
    //     Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.pickedGun);
    //     message.AddUShort(player.Id);
    //     message.AddByte((byte)pickedGunIndex);
    //     message.AddUInt(lastSlotChangeTick);
    //     NetworkManager.Singleton.Server.SendToAll(message);
    // }
    #endregion

    #region ClientSenders
    public void SendClientFire()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ClientToServerId.fireInput);
        message.AddByte((byte)currentWeapon.slot);
        message.AddUInt(lastShotTick);
        NetworkManager.Singleton.Client.Send(message);
    }

    public void SendClientAltFire()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ClientToServerId.altFireInput);
        message.AddByte((byte)currentWeapon.slot);
        message.AddUInt(lastAltFireTick);
        NetworkManager.Singleton.Client.Send(message);
    }

    public void SendClientAltFireConfirmation()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ClientToServerId.altFireConfirmation);
        message.AddByte((byte)currentWeapon.slot);
        message.AddUInt(lastAltFireConfirmationTick);
        NetworkManager.Singleton.Client.Send(message);
    }

    public void SendReload()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ClientToServerId.gunReload);
        message.AddByte((byte)currentWeapon.slot);
        message.AddUInt(lastReloadTick);
        NetworkManager.Singleton.Client.Send(message);
    }

    public void SendSlotSwitch()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ClientToServerId.slotChange);
        message.AddUInt(lastSlotChangeTick);
        message.AddByte((byte)currentWeapon.slot);
        NetworkManager.Singleton.Client.Send(message);
    }
    #endregion

    #region ClientToServerHandlers
    [MessageHandler((ushort)ClientToServerId.fireInput)]
    private static void FireInput(ushort fromClientId, Message message)
    {
        if (Player.list.TryGetValue(fromClientId, out Player player))
        {
            player.playerShooting.HandleClientFired(message.GetByte(), message.GetUInt());
        }
    }

    [MessageHandler((ushort)ClientToServerId.altFireInput)]
    private static void AltFireInput(ushort fromClientId, Message message)
    {
        if (Player.list.TryGetValue(fromClientId, out Player player))
        {
            player.playerShooting.HandleClientAltFire((int)message.GetByte(), message.GetUInt());
        }
    }

    [MessageHandler((ushort)ClientToServerId.altFireConfirmation)]
    private static void AltFireConfirmation(ushort fromClientId, Message message)
    {
        if (Player.list.TryGetValue(fromClientId, out Player player))
        {
            player.playerShooting.HandleClientAltFireConfirm((int)message.GetByte(), message.GetUInt());
        }
    }

    [MessageHandler((ushort)ClientToServerId.slotChange)]
    private static void ChangeSlot(ushort fromClientId, Message message)
    {
        if (Player.list.TryGetValue(fromClientId, out Player player))
        {
            player.playerShooting.HandleClientWeaponSwitch(message.GetUInt(), (int)message.GetByte());
        }
    }

    [MessageHandler((ushort)ClientToServerId.gunReload)]
    private static void ReloadGun(ushort fromClientId, Message message)
    {
        if (Player.list.TryGetValue(fromClientId, out Player player))
        {
            player.playerShooting.HandleClientReload((int)message.GetByte(), message.GetUInt());
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
            player.playerShooting.HandleClientFired((int)message.GetByte(), message.GetUInt());
        }
    }

    [MessageHandler((ushort)ServerToClientId.playerAltFire)]
    private static void PlayerAltFired(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            if (player.IsLocal) return;
            player.playerShooting.HandleClientAltFire((int)message.GetByte(), message.GetUInt());
        }
    }

    [MessageHandler((ushort)ServerToClientId.playerAltFireConfirmation)]
    private static void PlayerAltFireConfirm(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            if (player.IsLocal) return;
            player.playerShooting.HandleClientAltFireConfirm((int)message.GetByte(), message.GetUInt());
        }
    }

    [MessageHandler((ushort)ServerToClientId.playerHit)]
    private static void PlayerHit(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        if (Player.list.TryGetValue(NetworkManager.Singleton.Client.Id, out Player player))
        {
            player.playerShooting.HandleServerPlayerHit(message.GetUShort(), message.GetUShort(), message.GetBool(), message.GetBool());
        }
    }

    [MessageHandler((ushort)ServerToClientId.weaponKill)]
    private static void HandleWeaponKill(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            player.playerShooting.HandleServerWeaponKill((int)message.GetByte(), message.GetInt(), message.GetUInt());
        }
    }

    [MessageHandler((ushort)ServerToClientId.pickedGun)]
    private static void PickGun(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            player.playerShooting.PickUpGun((int)message.GetByte(), message.GetUInt());
        }
    }

    [MessageHandler((ushort)ServerToClientId.gunReloading)]
    private static void PlayerGunReloading(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            if (player.IsLocal) return;
            player.playerShooting.HandleClientReload((int)message.GetByte(), message.GetUInt());
        }
    }

    [MessageHandler((ushort)ServerToClientId.gunChanged)]
    private static void ChangeGun(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            player.playerShooting.HandleServerWeaponSwitch((int)message.GetSByte(), (int)message.GetSByte(), (int)message.GetSByte(), (int)message.GetSByte(), (int)message.GetUShort(), message.GetInt(), message.GetUInt());
        }
    }
    #endregion
}
