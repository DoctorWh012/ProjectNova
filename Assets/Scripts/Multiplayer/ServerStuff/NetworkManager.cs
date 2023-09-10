using Riptide;
using Riptide.Utils;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Riptide.Transports.Steam;
using System.Collections.Generic;

public enum ServerToClientId : ushort
{
    serverTick = 1,

    playerMovement,
    playerCrouch,
    playerDash,
    playerGroundSlam,

    playerSpawned,

    playerFired,
    gunChanged,
    gunReloading,
    pickedGun,
    weaponSync,

    healthChanged,
    playerDied,
    playerScore,

    gunSpawned,
    gunDespawned,

    matchStart,
    matchOver,
}

public enum ClientToServerId : ushort
{
    name = 1,
    playerMovement,
    playerCrouch,
    playerDash,
    playerGroundSlam,
    playerInteract,

    fireInput,
    slotChange,
    gunReload,
}

public class NetworkManager : MonoBehaviour

{
    private static NetworkManager _singleton;
    public static NetworkManager Singleton
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
                Debug.Log($"{nameof(NetworkManager)} instance already exists, destroying duplicate");
                Destroy(value.gameObject);
            }

        }
    }

    public const float ServerTickRate = 64f;

    public float minTimeBetweenTicks { get; private set; }
    public ushort serverTick { get; private set; }
    public static int lagCompensationCacheSize { get; private set; } = 20; //64 ticks every 1000ms

    public Client Client { get; private set; }
    public Server Server { get; private set; }


    [Header("Prefabs")]
    [SerializeField] public GameObject localPlayerPrefab;
    [SerializeField] public GameObject netPlayerPrefab;

    [Header("Settings")]
    [SerializeField] public ushort maxClientCount;

    private float timer;

    private void Awake()
    {
        Singleton = this;
        minTimeBetweenTicks = 1f / ServerTickRate;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("Steam is not initialized!");
            Application.Quit(); return;
        }

        SceneManager.sceneLoaded += SceneChangeDebug;

        RiptideLogger.Initialize(Debug.Log, Debug.Log, Debug.LogWarning, Debug.LogError, false);

        SteamServer steamServer = new SteamServer();
        Server = new Server(steamServer);
        Server.ClientDisconnected += PlayerLeft;

        Client = new Client(new SteamClient(steamServer));
        Client.Connected += DidConnect;
        Client.ConnectionFailed += FailedToConnect;
        Client.ClientDisconnected += PlayerLeft;
        Client.Disconnected += DidDisconnect;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        while (timer >= minTimeBetweenTicks)
        {
            timer -= minTimeBetweenTicks;
            if (Server.IsRunning)
            {
                Server.Update();
                serverTick++;
                SendTick();

                int cacheIndex = NetworkManager.Singleton.serverTick % lagCompensationCacheSize;

                for (int i = 0; i < Player.list.Count; i++)
                {
                    foreach (Player player in Player.list.Values) player.playerMovement.playerSimulationState[cacheIndex] = player.playerMovement.CurrentSimulationState();
                }
            }
            Client.Update();
        }
    }

    public void SetAllPlayersPositionsTo(ushort tick, ushort excludedPlayerId)
    {
        foreach (Player player in Player.list.Values)
        {
            if (player.Id == excludedPlayerId || player.playerHealth.currentPlayerState == PlayerState.Dead) continue;
            player.playerMovement.SetPlayerPositionToTick(tick);
        }
    }

    public void ResetPlayersPositions(ushort excludedPlayerId)
    {
        foreach (Player player in Player.list.Values)
        {
            if (player.Id == excludedPlayerId || player.playerHealth.currentPlayerState == PlayerState.Dead) continue;
            player.playerMovement.ResetPlayerPosition();
        }
    }

    private void OnApplicationQuit()
    {
        if (Server.IsRunning) Server.Stop();
        Server.ClientDisconnected -= PlayerLeft;

        Client.Disconnect();
        Client.Connected -= DidConnect;
        Client.ConnectionFailed -= FailedToConnect;
        Client.ClientDisconnected -= PlayerLeft;
        Client.Disconnected -= DidDisconnect;

    }

    internal void StopServer()
    {
        Server.Stop();
        DestroyAllPlayers();
    }

    internal void DisconnectClient()
    {
        Client.Disconnect();
        DestroyAllPlayers();
        GameObject[] toBeDestroyed = GameObject.FindGameObjectsWithTag("Destroy");
        foreach (GameObject go in toBeDestroyed) Destroy(go);
    }

    private void DestroyAllPlayers()
    {
        foreach (KeyValuePair<ushort, Player> player in Player.list) Destroy(player.Value.gameObject);
    }

    private void DidConnect(object sender, EventArgs e)
    {
        StartCoroutine(LobbyManager.Singleton.SendName());
    }

    private void FailedToConnect(object sender, EventArgs e)
    {
        SceneManager.LoadScene("Menu");
    }

    private void PlayerLeft(object sender, ClientDisconnectedEventArgs e)
    {
        Destroy(Player.list[e.Id].gameObject);
    }

    private void PlayerLeft(object sender, ServerDisconnectedEventArgs e)
    {
        Destroy(Player.list[e.Client.Id].gameObject);
    }

    private void DidDisconnect(object sender, EventArgs e)
    {
        LobbyManager.Singleton.LeaveLobby();
        SceneManager.LoadScene("Menu");
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void SceneChangeDebug(Scene loaded, LoadSceneMode i)
    {
        Debug.LogWarning($"Switched scene To {loaded.name}");
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
        if (tick > NetworkManager.Singleton.serverTick) NetworkManager.Singleton.serverTick = tick;
    }
}
