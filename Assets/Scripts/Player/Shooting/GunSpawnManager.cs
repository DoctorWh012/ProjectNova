using UnityEngine;
using Riptide;

public class GunSpawnManager : MonoBehaviour
{
    private static GunSpawnManager Instance;

    [Header("Components")]
    [SerializeField] GunSpawn[] weaponSpawners;

    private void Awake()
    {
        Instance = this;
        AssignIdToSpawners();
    }

    private void AssignIdToSpawners()
    {
        for (int i = 0; i < weaponSpawners.Length; i++) weaponSpawners[i].weaponSpawnerId = i;
    }

    private void HandleServerWeaponSpawn(int spawnerId, int weaponId, ushort tick)
    {
        if (weaponSpawners[spawnerId].lastReceivedWeaponSpawnTick >= tick) return;
        weaponSpawners[spawnerId].lastReceivedWeaponSpawnTick = tick;

        weaponSpawners[spawnerId].SpawnSpecificWeapon(weaponId);
    }

    private void HandleServerWeaponDespawn(int spawnerId, ushort tick)
    {
        if (weaponSpawners[spawnerId].lastReceivedWeaponDespawnTick >= tick) return;
        weaponSpawners[spawnerId].lastReceivedWeaponDespawnTick = tick;

        weaponSpawners[spawnerId].DespawnWeapon();
    }

    [MessageHandler((ushort)ServerToClientId.weaponSpawned)]
    private static void GetSpawnedWeapon(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        GunSpawnManager.Instance.HandleServerWeaponSpawn((int)message.GetByte(), (int)message.GetByte(), message.GetUShort());
    }

    [MessageHandler((ushort)ServerToClientId.weaponDespawned)]
    private static void GetWeaponDespawn(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        GunSpawnManager.Instance.HandleServerWeaponDespawn((int)message.GetByte(), message.GetUShort());
    }
}
