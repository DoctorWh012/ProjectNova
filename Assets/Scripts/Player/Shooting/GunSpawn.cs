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

    private Guns spawnedWeaponSettings;
    public int weaponSpawnerId;

    [HideInInspector] public ushort lastReceivedWeaponSpawnTick;
    [HideInInspector] public ushort lastReceivedWeaponDespawnTick;

    void Start()
    {
        idText.SetText($"#{weaponSpawnerId}");

        NetworkManager.Singleton.Server.ClientConnected += SendWeaponSpawnerDataToPlayer;

        HideAllWeapons();
        if (NetworkManager.Singleton.Server.IsRunning) InvokeRepeating("SpawnWeapon", weaponChangeDelay, weaponChangeDelay);
    }

    private void OnApplicationQuit()
    {
        NetworkManager.Singleton.Server.ClientConnected -= SendWeaponSpawnerDataToPlayer;
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
        CancelInvoke("SpawnWeapon");
        InvokeRepeating("SpawnWeapon", weaponRespawnDelay, weaponChangeDelay);
        SendWeaponDespawned();
    }

    private void SendWeaponSpawned()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.weaponSpawned);
        message.AddByte((byte)weaponSpawnerId);
        message.AddByte((byte)spawnedWeaponId);
        message.AddUShort(NetworkManager.Singleton.serverTick);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    private void SendWeaponSpawnerDataToPlayer(object sender, ServerConnectedEventArgs e)
    {
        if (!spawnedWeaponSettings) return;
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.weaponSpawned);
        message.AddByte((byte)weaponSpawnerId);
        message.AddByte((byte)spawnedWeaponId);
        message.AddUShort(NetworkManager.Singleton.serverTick);
        NetworkManager.Singleton.Server.Send(message, e.Client.Id);
    }

    private void SendWeaponDespawned()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.weaponDespawned);
        message.AddByte((byte)weaponSpawnerId);
        message.AddUShort(NetworkManager.Singleton.serverTick);
        NetworkManager.Singleton.Server.SendToAll(message);
    }
}
