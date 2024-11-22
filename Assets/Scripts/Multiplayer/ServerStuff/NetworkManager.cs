using Riptide;
using System;
using System.Linq;
using Steamworks;
using Riptide.Utils;
using UnityEngine;
using Riptide.Transports.Steam;

public enum ServerToClientId : ushort
{
    serverTick = 1,

    newPlayer,
    newPlayerCatchUp,
    playerPing,
    playerChat,

    matchData,
    matchTimer,
    sceneChanged,
    scoreBoardChanged,
    scoreBoardReset,

    playerMovement,
    playerCrouch,
    playerDash,
    playerGroundSlam,
    playerMovementFreeze,
    playerMovementFree,

    playerSpawned,

    playerFired,
    playerAltFire,
    playerHit,
    playerAltFireConfirmation,
    weaponKill,
    gunChanged,
    gunReloading,
    pickedGun,

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

    playerChat,
    playerMovement,
    playerCrouch,
    playerDash,
    playerGroundSlam,
    playerInteract,

    playerSuicide,

    fireInput,
    altFireInput,
    altFireConfirmation,
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
    public static uint lagCompensationCacheSize { get; private set; } = 22; //64 ticks every 1000ms
    public static int overcompensationAmount { get; private set; } = 1;

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
            foreach (Player player in Player.list.Values) player.playerMovement.SaveSimulationState(cacheIndex);
        }
        Client.Update();
    }

    #region Lag Compensation
    public void SetAllPlayersPositionsTo(uint tick, ushort excludedPlayerId)
    {
        foreach (Player player in Player.list.Values)
        {
            if (player.Id == excludedPlayerId) continue;
            player.playerMovement.SetPlayerPositionToTick(tick);
        }
    }

    public void ResetPlayersPositions(ushort excludedPlayerId)
    {
        foreach (Player player in Player.list.Values)
        {
            if (player.Id == excludedPlayerId) continue;
            player.playerMovement.ResetPlayerPosition();
        }
    }
    #endregion

    #region  Server/Client Event Handlers
    // Local Player Joined Server
    private void JoinedServer(object sender, EventArgs e)
    {
        GameManager.Singleton.AlterCursorState(false);

        if (Server.IsRunning)
        {
            GameManager.Singleton.ServerIntroducePlayerToMatch(Client.Id, SteamFriends.GetPersonaName(), SteamUser.GetSteamID());
            GameManager.Singleton.LoadScene(GameManager.lobbyScene, "Host Joined Server");
        }
        else SendClientHandShake();
    }

    // Local Player Disconnected From Server
    private void PlayerDisconnected(object sender, DisconnectedEventArgs e)
    {
        GameManager.Singleton.AlterCursorState(true);
        GameManager.Singleton.chatKeyIndicator.SetActive(false);
        GameManager.Singleton.ClearChat();

        SteamMatchmaking.LeaveLobby(lobbyId);

        GameManager.Singleton.EndMatch();
        GameManager.Singleton.RemoveAllPlayersFromMatch();
        serverTick = 0;

        GameManager.Singleton.LoadScene(GameManager.menuScene, "PlayerDisconnected");
    }

    // Non Local Player Disconnected From Server
    private void ClientDisconnected(object sender, ClientDisconnectedEventArgs e)
    {
        GameManager.Singleton.RemovePlayerFromMatch(e.Id);
        Destroy(Player.list[e.Id].gameObject);
    }

    private void HandleClientHandShake(ushort id, string username, CSteamID steamId)
    {
        if (GameManager.playersOnLobby.ContainsKey(id)) return;

        // Introduces New Player To Match
        GameManager.Singleton.ServerIntroducePlayerToMatch(id, username, steamId);

        // REPLY HANDSHAKE?
        GameManager.Singleton.SendSceneToPlayer(id, GameManager.currentScene);

        // Sends Match Data To Player
        SendCatchUpToPlayer(id);

        SendPlayerToPlayers(id);
    }

    private void HandleServerCatchUp(ushort[] ids, PlayerData[] playerDatas, ushort[] placingFFA, ushort[] serverAllies, ushort[] serverEnemies, byte respawnTime, GameMode gamemode, MatchState matchState, byte serverAlliesTeamId, byte serverEnemiesTeamId)
    {
        GameManager.Singleton.CatchUpMatchData(respawnTime, gamemode, matchState);

        GameManager.playersPlacingFFA = placingFFA;

        if (serverAllies.Contains(Client.Id))
        {
            GameManager.Singleton.allyTeamId = serverAlliesTeamId;
            GameManager.Singleton.enemyTeamId = serverEnemiesTeamId;

            GameManager.playersPlacingAllyTeam = serverAllies;
            GameManager.playersPlacingEnemyTeam = serverEnemies;

            for (int i = 0; i < serverAllies.Length; i++) GameManager.allyTeam.Add(serverAllies[i], playerDatas[Array.IndexOf(ids, serverAllies[i])]);
            for (int i = 0; i < serverEnemies.Length; i++) GameManager.enemyTeam.Add(serverEnemies[i], playerDatas[Array.IndexOf(ids, serverEnemies[i])]);
        }

        else
        {
            GameManager.Singleton.allyTeamId = serverEnemiesTeamId;
            GameManager.Singleton.enemyTeamId = serverAlliesTeamId;

            GameManager.playersPlacingAllyTeam = serverEnemies;
            GameManager.playersPlacingEnemyTeam = serverAllies;

            for (int i = 0; i < serverEnemies.Length; i++) GameManager.allyTeam.Add(serverEnemies[i], playerDatas[Array.IndexOf(ids, serverEnemies[i])]);
            for (int i = 0; i < serverAllies.Length; i++) GameManager.enemyTeam.Add(serverAllies[i], playerDatas[Array.IndexOf(ids, serverAllies[i])]);
        }

        GameManager.Singleton.enemyTeamNameTxt.SetText(GameManager.Singleton.teams[GameManager.Singleton.enemyTeamId].teamName);
        GameManager.Singleton.allyTeamNameTxt.SetText(GameManager.Singleton.teams[GameManager.Singleton.allyTeamId].teamName);

        for (int i = 0; i < ids.Length; i++) GameManager.Singleton.ClientIntroducePlayerToMatch(ids[i], playerDatas[i], GameManager.allyTeam.ContainsKey(ids[i]));
        GameManager.Singleton.UpdateScoreBoardCapsules();
    }

    private void HandleServerPlayerInfo(ushort id, PlayerData playerData, ushort[] placingFFA, ushort[] serverAllies, ushort[] serverEnemies)
    {
        GameManager.playersPlacingFFA = placingFFA;

        if (serverAllies.Contains(Client.Id))
        {
            GameManager.playersPlacingAllyTeam = serverAllies;
            GameManager.playersPlacingEnemyTeam = serverEnemies;

            if (serverAllies.Contains(id)) GameManager.allyTeam.Add(id, playerData);
            else GameManager.enemyTeam.Add(id, playerData);
        }

        else
        {
            GameManager.playersPlacingAllyTeam = serverEnemies;
            GameManager.playersPlacingEnemyTeam = serverAllies;

            if (serverEnemies.Contains(id)) GameManager.allyTeam.Add(id, playerData);
            else GameManager.enemyTeam.Add(id, playerData);
        }

        GameManager.Singleton.ClientIntroducePlayerToMatch(id, playerData, GameManager.allyTeam.ContainsKey(id));
        GameManager.Singleton.UpdateScoreBoardCapsules();
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
        SteamMatchmaking.SetLobbyData(lobbyId, "status", GameManager.currentMatchState.ToString());

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
        message.AddULong((ulong)SteamUser.GetSteamID());
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

    private void SendCatchUpToPlayer(ushort id)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.newPlayerCatchUp);
        message.AddUShorts(GameManager.playersOnLobby.Keys.ToArray());
        message.AddSerializables<PlayerData>(GameManager.playersOnLobby.Values.ToArray());

        message.AddUShorts(GameManager.playersPlacingFFA);
        message.AddUShorts(GameManager.playersPlacingAllyTeam);
        message.AddUShorts(GameManager.playersPlacingEnemyTeam);

        message.AddByte((byte)GameManager.respawnTime);
        message.AddByte((byte)GameManager.currentGamemode);
        message.AddByte((byte)GameManager.currentMatchState);

        message.AddByte(GameManager.Singleton.allyTeamId);
        message.AddByte(GameManager.Singleton.enemyTeamId);
        Server.Send(message, id);
    }

    private void SendPlayerToPlayers(ushort id)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.newPlayer);
        message.AddUShort(id);
        message.AddSerializable<PlayerData>(GameManager.playersOnLobby[id]);
        message.AddUShorts(GameManager.playersPlacingFFA);
        message.AddUShorts(GameManager.playersPlacingAllyTeam);
        message.AddUShorts(GameManager.playersPlacingEnemyTeam);
        Server.SendToAll(message);
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

    [MessageHandler((ushort)ServerToClientId.newPlayerCatchUp)]
    private static void GetCatchUp(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        NetworkManager.Singleton.HandleServerCatchUp(message.GetUShorts(), message.GetSerializables<PlayerData>(), message.GetUShorts(), message.GetUShorts(), message.GetUShorts(), message.GetByte(), (GameMode)message.GetByte(), (MatchState)message.GetByte(), message.GetByte(), message.GetByte());
    }

    [MessageHandler((ushort)ServerToClientId.newPlayer)]
    private static void GetNewPlayer(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        ushort id = message.GetUShort();
        if (id == NetworkManager.Singleton.Client.Id) return;
        NetworkManager.Singleton.HandleServerPlayerInfo(id, message.GetSerializable<PlayerData>(), message.GetUShorts(), message.GetUShorts(), message.GetUShorts());
    }
    #endregion

    #region ClientToServerReceivers
    [MessageHandler((ushort)ClientToServerId.clientHandShake)]
    private static void GetClientHandShake(ushort fromClientId, Message message)
    {
        NetworkManager.Singleton.HandleClientHandShake(fromClientId, message.GetString(), (CSteamID)message.GetULong());
    }
    #endregion
}