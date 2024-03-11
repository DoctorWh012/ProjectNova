using Riptide;
using System;
using Steamworks;
using Riptide.Utils;
using UnityEngine;
using Riptide.Transports.Steam;

public enum ServerToClientId : ushort
{
    serverTick = 1,

    playerInfo,

    matchData,
    matchTimer,
    sceneChanged,
    scoreBoardChanged,

    playerMovement,
    playerCrouch,
    playerDash,
    playerGroundSlam,
    playerMovementFreeze,
    playerMovementFree,

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
    clientHandShake = 1,
    playerLoadedScene,

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
        set
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

    public uint serverTick { get; private set; } // NEEDS CLEARING
    public static uint lagCompensationCacheSize { get; private set; } = 25; //64 ticks every 1000ms
    public static int overcompensationAmount { get; private set; } = 2;

    public Client Client { get; private set; }
    public Server Server { get; private set; }

    [Header("Prefabs")]
    [SerializeField] public GameObject localPlayerPrefab;
    [SerializeField] public GameObject netPlayerPrefab;
    [SerializeField] public DebugGhost debugGhost;

    protected Callback<LobbyCreated_t> lobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequested;
    protected Callback<LobbyEnter_t> lobbyEnter;
    public CSteamID lobbyId { get; private set; }

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

        Client = new Client(new Riptide.Transports.Steam.SteamClient(steamServer));
        Client.Connected += JoinedServer;
        Client.Disconnected += PlayerDisconnected;
        Client.ClientDisconnected += ClientDisconnected;

        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        lobbyEnter = Callback<LobbyEnter_t>.Create(OnLobbyEnter);
    }

    private void OnApplicationQuit()
    {
        Client.Disconnect();
        Client.Connected -= JoinedServer;
        Client.Disconnected -= PlayerDisconnected;
        Client.ClientDisconnected -= ClientDisconnected;

        if (Server.IsRunning) Server.Stop();
        SteamMatchmaking.LeaveLobby(lobbyId);
    }

    private void FixedUpdate()
    {
        if (Server.IsRunning)
        {
            Server.Update();
            serverTick++;
            SendTick();

            uint cacheIndex = serverTick % lagCompensationCacheSize;
            foreach (Player player in Player.list.Values) player.playerMovement.playerSimulationState[cacheIndex] = player.playerMovement.CurrentSimulationState();
        }
        Client.Update();
    }

    #region Lag Compensation
    public void SetAllPlayersPositionsTo(uint tick, ushort excludedPlayerId)
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
    // Local Player Joined Server
    private void JoinedServer(object sender, EventArgs e)
    {
        // Introduces Local Player Into Match
        MatchManager.Singleton.IntroducePlayerToMatch(Client.Id, SteamFriends.GetPersonaName());

        // Sends HandShake If Player Is A Client Or Loads The Lobby If Host
        if (!Server.IsRunning) SendClientHandShake();
        else GameManager.Singleton.LoadScene(GameManager.lobbyScene, "Host Joined Server");

    }

    // Local Player Disconnected From Server
    private void PlayerDisconnected(object sender, DisconnectedEventArgs e)
    {
        SteamMatchmaking.LeaveLobby(lobbyId);

        MatchManager.Singleton.EndMatch();
        MatchManager.Singleton.RemoveAllPlayersFromMatch();
        serverTick = 0;

        GameManager.Singleton.LoadScene(GameManager.menuScene, "PlayerDisconnected");
    }

    // Non Local Player Disconnected From Server
    private void ClientDisconnected(object sender, ClientDisconnectedEventArgs e)
    {
        MatchManager.Singleton.RemovePlayerFromMatch(e.Id);
        Destroy(Player.list[e.Id].gameObject);
    }

    private void HandleClientHandShake(ushort id, string username)
    {
        if (MatchManager.playersOnLobby.ContainsKey(id)) return;

        // Introduces New Player To Match
        MatchManager.Singleton.IntroducePlayerToMatch(id, username);

        // REPLY HANDSHAKE?
        GameManager.Singleton.SendSceneToPlayer(id, GameManager.currentScene);

        // Sends Existing Player Info To New Player
        foreach (ushort otherPlayerId in MatchManager.playersOnLobby.Keys) SendPlayersInfoToPlayer(id, otherPlayerId);

        // Sends New Player Info To Existing Players
        SendPlayerInfo(id);
    }

    private void HandleServerPlayerInfo(ushort id, PlayerData playerData)
    {
        if (!MatchManager.playersOnLobby.ContainsKey(id)) MatchManager.Singleton.IntroducePlayerToMatch(id, playerData.playerName);
        MatchManager.playersOnLobby[id].onQueue = playerData.onQueue;
        MatchManager.playersOnLobby[id].kills = playerData.kills;
        MatchManager.playersOnLobby[id].deaths = playerData.deaths;
        MatchManager.Singleton.UpdateScoreBoardCapsule(id);
    }
    #endregion

    #region Lobby Management
    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError("Failed to create steam lobby");
            MainMenu.Instance.ReturnToMainMenu();
            return;
        }

        lobbyId = new CSteamID(callback.m_ulSteamIDLobby);

        SteamMatchmaking.SetLobbyData(lobbyId, "HostCSteamId", SteamUser.GetSteamID().ToString());
        SteamMatchmaking.SetLobbyData(lobbyId, "name", lobbyName);
        SteamMatchmaking.SetLobbyData(lobbyId, "status", MatchManager.currentMatchState.ToString());

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
    private void SendClientHandShake()
    {
        Message message = Message.Create(MessageSendMode.Reliable, ClientToServerId.clientHandShake);
        message.AddString(SteamFriends.GetPersonaName());
        Client.Send(message);
    }
    #endregion

    #region ServerToClientSenders
    private void SendTick()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.serverTick);
        message.AddUInt(serverTick);
        Server.SendToAll(message);
    }

    private void SendPlayerInfo(ushort id)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.playerInfo);
        message.AddUShort(id);
        message.AddSerializable<PlayerData>(MatchManager.playersOnLobby[id]);
        Server.SendToAll(message);
    }

    private void SendPlayersInfoToPlayer(ushort id, ushort otherPlayerId)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.playerInfo);
        message.AddUShort(otherPlayerId);
        message.AddSerializable<PlayerData>(MatchManager.playersOnLobby[otherPlayerId]);
        Server.Send(message, id);
    }
    #endregion

    #region ServerToClientReceivers
    [MessageHandler((ushort)ServerToClientId.serverTick)]
    private static void SyncTick(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;

        uint tick = message.GetUInt();
        if (tick > NetworkManager.Singleton.serverTick) NetworkManager.Singleton.serverTick = tick;
    }

    [MessageHandler((ushort)ServerToClientId.playerInfo)]
    private static void GetPlayerInfo(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        NetworkManager.Singleton.HandleServerPlayerInfo(message.GetUShort(), message.GetSerializable<PlayerData>());
    }
    #endregion

    #region ClientToServerReceivers
    [MessageHandler((ushort)ClientToServerId.clientHandShake)]
    private static void GetClientHandShake(ushort fromClientId, Message message)
    {
        NetworkManager.Singleton.HandleClientHandShake(fromClientId, message.GetString());
    }
    #endregion
}