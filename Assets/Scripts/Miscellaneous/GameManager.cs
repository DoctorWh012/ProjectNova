using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Riptide;

public class GameManager : MonoBehaviour
{
    public static int lagCompensationCacheSize { get; private set; } = 20; //64 ticks every 1000ms
    public bool networking { get; set; }
    public float minTimeBetweenTicks { get; private set; }
    public ushort serverTick { get; set; }

    private static GameManager _singleton;
    public static GameManager Singleton
    {
        get => _singleton;
        private set
        {
            if (_singleton == null)
            {
                _singleton = value;
            }

            else if (_singleton != value)
            {
                Debug.Log($"{nameof(GameManager)} instance already exists, destroying duplicate");
                Destroy(value.gameObject);
            }

        }
    }

    public GameObject LocalPlayerPrefab => localPlayerPrefab;
    public GameObject PlayerPrefab => playerPrefab;
    public ParticleSystem HitPrefab => hitPrefab;
    public ParticleSystem PlayerHitPrefab => playerHitPrefab;
    public TrailRenderer ShotTrail => shotTrail;

    [Header("Prefabs")]
    [SerializeField] private GameObject localPlayerPrefab;
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private ParticleSystem hitPrefab;
    [SerializeField] private ParticleSystem playerHitPrefab;
    [SerializeField] private TrailRenderer shotTrail;

    private float timer;

    private void Awake()
    {
        Singleton = this;
        DontDestroyOnLoad(gameObject);
        minTimeBetweenTicks = 1f / NetworkManager.ServerTickRate;
    }

    private void Update()
    {
        if (!NetworkManager.Singleton.Server.IsRunning) return;

        timer += Time.deltaTime;
        while (timer >= minTimeBetweenTicks)
        {
            timer -= minTimeBetweenTicks;
            SendTick();
            serverTick++;
            int cacheIndex = GameManager.Singleton.serverTick % lagCompensationCacheSize;
            foreach (Player player in Player.list.Values)
                player.Movement.playerSimulationState[cacheIndex] = player.Movement.CurrentSimulationState();
        }
    }

    public void SetAllPlayersPositionsTo(ushort tick, ushort excludedPlayerId)
    {
        foreach (Player player in Player.list.Values)
        {
            if (player.Id == excludedPlayerId || player.serverPlayerHealth.isDead) continue;
            player.Movement.SetPlayerPositionToTick(tick);
        }
    }

    public void ResetPlayersPositions(ushort excludedPlayerId)
    {
        foreach (Player player in Player.list.Values)
        {
            if (player.Id == excludedPlayerId) continue;
            player.Movement.ResetPlayerPosition();
        }
    }

    public void ActivateDeactivateAllPlayersCollisions(bool state)
    {
        foreach (Player player in Player.list.Values)
        {
            player.Movement.rb.detectCollisions = state;
        }
    }

    private void SendTick()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, (ushort)ServerToClientId.serverTick);
        message.AddUShort(serverTick);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    [MessageHandler((ushort)ServerToClientId.serverTick)]
    private static void SyncTick(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;

        ushort tick = message.GetUShort();
        if (tick > GameManager.Singleton.serverTick) GameManager.Singleton.serverTick = tick;
    }
}
