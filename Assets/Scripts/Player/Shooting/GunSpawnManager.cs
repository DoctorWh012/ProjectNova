using System;
using Riptide;
using UnityEngine;
using System.Collections.Generic;

public class GunSpawnManager : MonoBehaviour
{
    public static GunSpawnManager Instance;

    public List<GunSpawn> gunSpawns = new List<GunSpawn>();

    private void Awake()
    {
        Instance = this;
        SetGunSpawnerIndex();
    }

    private void Start()
    {
        if (!NetworkManager.Singleton.Server.IsRunning) return;
        StartGunSpawnTimers();
        NetworkManager.Singleton.Client.ClientConnected += SendGunsSpawners;
    }

    private void SetGunSpawnerIndex()
    {
        for (int i = 0; i < gunSpawns.Count; i++) gunSpawns[i].gunSpawnerIndex = i;
    }

    private void StartGunSpawnTimers()
    {
        for (int i = 0; i < gunSpawns.Count; i++) gunSpawns[i].StartGunSpawnTimer(gunSpawns[i].gunSpawnDelay);
    }

    public void SendGunSpawnMessage(int gunSpawnerIndex, int gunIndex)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.gunSpawned);
        message.AddInt(gunSpawnerIndex);
        message.AddInt(gunIndex);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    public void SendGunDespawnMessage(int gunSpawnerIndex)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.gunDespawned);
        message.AddInt(gunSpawnerIndex);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    public void SendGunsSpawners(object sender, EventArgs e)
    {
        for (int i = 0; i < gunSpawns.Count; i++)
        {
            SendGunSpawnMessage(i, gunSpawns[i].gunIndex);
        }
    }

    [MessageHandler((ushort)ServerToClientId.gunSpawned)]
    private static void SpawnGun(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        GunSpawnManager.Instance.gunSpawns[message.GetInt()].SpawnNewGun(message.GetInt());
    }

    [MessageHandler((ushort)ServerToClientId.gunDespawned)]
    private static void DespawnGun(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        GunSpawnManager.Instance.gunSpawns[message.GetInt()].DespawnGun();
    }
}
