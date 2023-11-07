using Riptide;
using System;
using Steamworks;
using Riptide.Utils;
using UnityEngine;
using Riptide.Transports.Steam;
using System.Collections.Generic;

public enum ServerToClientId : ushort
{
    serverTick = 1,

    SceneChanged,

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
    playerRespawned,
    weaponSpawned,
    weaponDespawned,
}

public enum ClientToServerId : ushort
{
    playerInfo = 1,
    playerMovement,
    playerCrouch,
    playerDash,
    playerGroundSlam,
    playerInteract,

    playerSuicide,

    fireInput,
    slotChange,
    gunReload,
}

public class NetworkManager : MonoBehaviour
{
    private static NetworkManager _singleton;
    public static NetworkManager Singleton
    {
        get { return _singleton; }
        private set
        {
            if (_singleton == null)
            {
                _singleton = value;
            }

            else if (_singleton != value)
            {
                Debug.Log($"{nameof(NetworkManager)} instance already exists, destroying duplicate");
                Destroy(value);
            }

        }
    }

    public ushort serverTick { get; private set; }
    public static int lagCompensationCacheSize { get; private set; } = 22; //64 ticks every 1000ms

    public Client Client { get; private set; }
    public Server Server { get; private set; }

    [Header("Prefabs")]
    [SerializeField] public GameObject localPlayerPrefab;
    [SerializeField] public GameObject netPlayerPrefab;

    protected Callback<LobbyCreated_t> lobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequested;
    protected Callback<LobbyEnter_t> lobbyEnter;

    public CSteamID lobbyId { get; private set; }
    public Dictionary<ushort, string> playersOnLobby = new Dictionary<ushort, string>();

    public int maxPlayers;
    public string lobbyName;

    private void Awake()
    {
        Singleton = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("Steam is not initialized!");
            Application.Quit(); return;
        }

        RiptideLogger.Initialize(Debug.Log, Debug.Log, Debug.LogWarning, Debug.LogError, false);

        SteamServer steamServer = new SteamServer();
        Server = new Server(steamServer);
        Server.ClientConnected += ClientJoined;
        Server.ClientDisconnected += ClientLeft;

        Client = new Client(new Riptide.Transports.Steam.SteamClient(steamServer));
        Client.Connected += JoinedServer;
        Client.Disconnected += PlayerDisconnected;

        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        lobbyEnter = Callback<LobbyEnter_t>.Create(OnLobbyEnter);
    }

    private void OnApplicationQuit()
    {
        Client.Disconnect();
        Client.Connected -= JoinedServer;
        Client.Disconnected -= PlayerDisconnected;

        if (Server.IsRunning) Server.Stop();
        Server.ClientConnected -= ClientJoined;
        Server.ClientDisconnected -= ClientLeft;
        SteamMatchmaking.LeaveLobby(NetworkManager.Singleton.lobbyId);
    }

    private void FixedUpdate()
    {
        if (Server.IsRunning)
        {
            Server.Update();
            serverTick++;
            SendTick();

            int cacheIndex = NetworkManager.Singleton.serverTick % lagCompensationCacheSize;
            foreach (Player player in Player.list.Values) player.playerMovement.playerSimulationState[cacheIndex] = player.playerMovement.CurrentSimulationState();
        }
        Client.Update();
    }

    #region Lag Compensation
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
    #endregion

    #region  Server/Client Event Handlers
    private void JoinedServer(object sender, EventArgs e)
    {
        if (Server.IsRunning)
        {
            GameManager.Singleton.LoadScene(Scenes.Lobby, "JoinedServer ServerOn");
            HandleReceivedPlayerData(Client.Id, SteamFriends.GetPersonaName());
            GameManager.Singleton.spawnPlayersAfterSceneLoad = true;
        }
        else
        {
            GameManager.Singleton.LoadScene(SteamMatchmaking.GetLobbyData(lobbyId, "map"), "JoinedServer ServerOff");
            SendPlayerInfo();
        }
    }

    private void ClientJoined(object sender, ServerConnectedEventArgs e)
    {

    }

    private void ClientLeft(object sender, ServerDisconnectedEventArgs e)
    {
        playersOnLobby.Remove(e.Client.Id);
        Destroy(Player.list[e.Client.Id].gameObject);
    }

    private void PlayerDisconnected(object sender, DisconnectedEventArgs e)
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SteamMatchmaking.LeaveLobby(NetworkManager.Singleton.lobbyId);
        GameManager.Singleton.LoadScene(Scenes.Menu, "PlayerDisconnected");
        playersOnLobby.Clear();
    }

    private void HandleReceivedPlayerData(ushort id, string username)
    {
        playersOnLobby.Add(id, username);
        if (Server.IsRunning && id != Client.Id) GameManager.Singleton.AttemptToSpawnPlayer(id, username);
    }

    #endregion

    #region Lobby Management
    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK) { Debug.LogError("Failed to create steam lobby"); return; }

        lobbyId = new CSteamID(callback.m_ulSteamIDLobby);

        SteamMatchmaking.SetLobbyData(lobbyId, "HostCSteamId", SteamUser.GetSteamID().ToString());
        SteamMatchmaking.SetLobbyData(lobbyId, "name", lobbyName);
        SteamMatchmaking.SetLobbyData(lobbyId, "status", "WIP");
        SteamMatchmaking.SetLobbyData(lobbyId, "map", GameManager.Singleton.currentScene);

        Server.Start(0, (ushort)maxPlayers);
        Client.Connect("127.0.0.1");
    }

    public void JoinLobby(ulong lobbyId)
    {
        SteamMatchmaking.JoinLobby(new CSteamID(lobbyId));
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyEnter(LobbyEnter_t callback)
    {
        if (Server.IsRunning) return;
        lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        Client.Connect(SteamMatchmaking.GetLobbyData(lobbyId, "HostCSteamId"));
    }
    #endregion

    #region ClientToServerSenders

    private void SendPlayerInfo()
    {
        Message message = Message.Create(MessageSendMode.Reliable, ClientToServerId.playerInfo);
        message.AddString(SteamFriends.GetPersonaName());
        Client.Send(message);
    }

    #endregion

    #region ServerToClientSenders
    private void SendTick()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.serverTick);
        message.AddUShort(serverTick);
        Server.SendToAll(message);
    }
    #endregion

    #region ServerToClientReceivers
    [MessageHandler((ushort)ServerToClientId.serverTick)]
    private static void SyncTick(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;

        ushort tick = message.GetUShort();
        if (tick > NetworkManager.Singleton.serverTick) NetworkManager.Singleton.serverTick = tick;
    }
    #endregion

    #region ClientToServerReceivers
    [MessageHandler((ushort)ClientToServerId.playerInfo)]
    private static void GetPlayerInfo(ushort fromClientId, Message message)
    {
        NetworkManager.Singleton.HandleReceivedPlayerData(fromClientId, message.GetString());
    }
    #endregion
}