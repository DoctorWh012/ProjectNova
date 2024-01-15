using System;
using UnityEngine;
using TMPro;
using Riptide;

[Serializable]
public struct PickableWeapons
{
    public GameObject weaponObject;
    public Guns weaponSettings;
}

public class GunSpawn : Interactable
{
    public int spawnedWeaponId { get; private set; }

    [Header("Components")]
    [SerializeField] PickableWeapons[] weapons;
    [SerializeField] TextMeshProUGUI idText;

    [Header("Settings")]
    [SerializeField] private float weaponChangeDelay;
    [SerializeField] private float weaponRespawnDelay;
    [Space(15)]
    [SerializeField] private bool spawnSpecificWeapon = false;
    [SerializeField] private int specificWeaponId;


    private Guns spawnedWeaponSettings;
    public int weaponSpawnerId;

    [HideInInspector] public uint lastReceivedWeaponSpawnTick;
    [HideInInspector] public uint lastReceivedWeaponDespawnTick;

    void Start()
    {
        idText.SetText($"#{weaponSpawnerId}");

        HideAllWeapons();
        if (NetworkManager.Singleton.Server.IsRunning)
        {
            if (spawnSpecificWeapon) InvokeRepeating("FixedWeaponSpawn", weaponChangeDelay, weaponChangeDelay);
            else InvokeRepeating("SpawnWeapon", weaponChangeDelay, weaponChangeDelay);
        }
    }

    private void Update()
    {
        if (!NetworkManager.Singleton.Server.IsRunning) return;
        if (players.Count == 0) return;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].playerInteractions.interactTimeCounter > 0 && spawnedWeaponSettings)
            {
                if (spawnedWeaponSettings.weaponType != WeaponType.melee) players[i].playerShooting.PickUpGun((int)spawnedWeaponSettings.slot, spawnedWeaponId, NetworkManager.Singleton.serverTick);
                else players[i].playerShooting.PickUpMelee(spawnedWeaponId, NetworkManager.Singleton.serverTick);

                DespawnWeapon();
            }
        }
    }

    private void HideAllWeapons()
    {
        for (int i = 0; i < weapons.Length; i++) weapons[i].weaponObject.SetActive(false);
    }

    private void FixedWeaponSpawn()
    {
        SpawnSpecificWeapon(specificWeaponId);
    }

    public void SpawnSpecificWeapon(int weaponId)
    {
        weapons[spawnedWeaponId].weaponObject.SetActive(false);

        spawnedWeaponId = weaponId;
        weapons[spawnedWeaponId].weaponObject.SetActive(true);
        spawnedWeaponSettings = weapons[spawnedWeaponId].weaponSettings;

        if (NetworkManager.Singleton.Server.IsRunning) SendWeaponSpawned();

    }

    public void SpawnWeapon()
    {
        weapons[spawnedWeaponId].weaponObject.SetActive(false);

        spawnedWeaponId = UnityEngine.Random.Range(0, weapons.Length);
        weapons[spawnedWeaponId].weaponObject.SetActive(true);
        spawnedWeaponSettings = weapons[spawnedWeaponId].weaponSettings;

        SendWeaponSpawned();
    }

    public void DespawnWeapon()
    {
        weapons[spawnedWeaponId].weaponObject.SetActive(false);
        spawnedWeaponSettings = null;

        if (!NetworkManager.Singleton.Server.IsRunning) return;
        if (spawnSpecificWeapon)
        {
            CancelInvoke("FixedWeaponSpawn");
            InvokeRepeating("FixedWeaponSpawn", weaponRespawnDelay, weaponChangeDelay);
        }
        else
        {
            CancelInvoke("SpawnWeapon");
            InvokeRepeating("SpawnWeapon", weaponRespawnDelay, weaponChangeDelay);
        }

        SendWeaponDespawned();
    }

    private void SendWeaponSpawned()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.weaponSpawned);
        message.AddByte((byte)weaponSpawnerId);
        message.AddByte((byte)spawnedWeaponId);
        message.AddUInt(NetworkManager.Singleton.serverTick);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    public void SendWeaponSpawnerDataToPlayer(ushort id)
    {
        if (!spawnedWeaponSettings) return;
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.weaponSpawned);
        message.AddByte((byte)weaponSpawnerId);
        message.AddByte((byte)spawnedWeaponId);
        message.AddUInt(NetworkManager.Singleton.serverTick);
        NetworkManager.Singleton.Server.Send(message, id);
    }

    private void SendWeaponDespawned()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.weaponDespawned);
        message.AddByte((byte)weaponSpawnerId);
        message.AddUInt(NetworkManager.Singleton.serverTick);
        NetworkManager.Singleton.Server.SendToAll(message);
    }
}
