using System;
using UnityEngine;
using Riptide;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Steamworks;

[Serializable]
public class PlayerData : IMessageSerializable
{
    public string playerName;
    public Sprite playerAvatar;
    public CSteamID playerSteamId;

    public bool onQueue;

    public int kills;
    public int deaths;
    public PlayerScoreCapsule scoreCapsule;

    public PlayerData(string name, CSteamID steamId)
    {
        playerName = name;
        playerSteamId = steamId;
    }

    public PlayerData() { }

    public void Deserialize(Message message)
    {
        playerName = message.GetString();
        playerSteamId = (CSteamID)message.GetULong();
        onQueue = message.GetBool();

        kills = (int)message.GetUShort();
        deaths = (int)message.GetUShort();
    }

    public void Serialize(Message message)
    {
        message.AddString(playerName);
        message.AddULong((ulong)playerSteamId);
        message.AddBool(onQueue);
        message.AddUShort((ushort)kills);
        message.AddUShort((ushort)deaths);
    }
}
public enum GameMode : byte
{
    FreeForAll
}

public enum MatchState : byte
{
    Waiting,
    Ongoing,
}

/* WORK REMINDER

    Wait for players to load in to start unfreeze countdown

*/

public class MatchManager : MonoBehaviour
{
    private static MatchManager _singleton;
    public static MatchManager Singleton
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
                Debug.Log($"{nameof(MatchManager)} instance already exists, destroying duplicate");
                Destroy(value);
            }
        }
    }

    public static Dictionary<ushort, PlayerData> playersOnLobby = new Dictionary<ushort, PlayerData>(); // NEEDS CLEARING
    public static int matchingPlayers;
    public static int respawnTime { get; private set; } = 5;
    public static GameMode currentGamemode { get; private set; }
    public static MatchState currentMatchState = MatchState.Waiting;

    [Header("Prefabs")]
    [SerializeField] private PlayerScoreCapsule playerScoreCapsulePrefab;

    [Header("Components")]
    [SerializeField] private TextMeshProUGUI mediumTopText;
    [SerializeField] private TextMeshProUGUI bigTopText;
    [SerializeField] public GameObject scoreboard;
    [SerializeField] private Transform scoreboardCapsulesHolder;

    [Header("Settings")]
    [SerializeField] private int preMatchStartTime;
    [SerializeField] private int matchWarmUpTime;

    [Header("Debug")]
    [SerializeField] private List<PlayerData> playerDataDebug = new List<PlayerData>();
    [SerializeField] private int respawnTimeDebug = respawnTime;
    [SerializeField] private GameMode gamemodeDebug = currentGamemode;
    [SerializeField] private MatchState matchStateDebug = currentMatchState;

    protected Callback<AvatarImageLoaded_t> avatarLoaded;

    uint lastMatchDataTick;
    private float matchTime;
    private float timer;
    private string timerEndText = "";

    private void Awake()
    {
        Singleton = this;
    }

    private void Start()
    {
        avatarLoaded = Callback<AvatarImageLoaded_t>.Create(OnAvatarLoaded);
        ResetAllUITexts();
        OpenCloseScoreBoard(false);
    }

    private void Update()
    {
        if (!NetworkManager.Singleton.Client.IsConnected) return;

        if (Input.GetKeyDown(SettingsManager.playerPreferences.scoreboardKey)) OpenCloseScoreBoard(true);
        if (Input.GetKeyUp(SettingsManager.playerPreferences.scoreboardKey)) OpenCloseScoreBoard(false);

        if (NetworkManager.Singleton.Server.IsRunning) AssignPingToCapsules();
    }

    #region Player Handling
    public void IntroducePlayerToMatch(ushort id, string name, CSteamID steamId)
    {
        playersOnLobby.Add(id, new PlayerData(name, steamId));

        CreatePlayerScoreBoardCapsule(id);
        UpdateScoreBoardCapsule(id);

        if (!NetworkManager.Singleton.Server.IsRunning) return;

        SendMatchDataToPlayer(id);
        playersOnLobby[id].onQueue = currentMatchState == MatchState.Ongoing;

        playerDataDebug.Add(playersOnLobby[id]);
    }

    public void RemovePlayerFromMatch(ushort id)
    {
        playerDataDebug.Remove(playersOnLobby[id]);

        DestroyPlayerScoreBoardCapsule(id);
        playersOnLobby.Remove(id);

    }

    public void RemoveAllPlayersFromMatch()
    {
        foreach (ushort id in playersOnLobby.Keys.ToList())
        {
            playerDataDebug.Remove(playersOnLobby[id]);

            DestroyPlayerScoreBoardCapsule(id);
            playersOnLobby.Remove(id);
        }
    }
    #endregion

    #region Scoreboard
    public void OpenCloseScoreBoard(bool state)
    {
        scoreboard.SetActive(state);
    }

    private void AssignPingToCapsules()
    {
        foreach (Connection clients in NetworkManager.Singleton.Server.Clients)
        {
            if (!playersOnLobby.ContainsKey(clients.Id)) return;
            playersOnLobby[clients.Id].scoreCapsule.playerPingTxt.SetText(clients.SmoothRTT.ToString());
        }
    }

    public void AddKillToPlayerScore(ushort id)
    {
        playersOnLobby[id].kills++;
        UpdateScoreBoardCapsule(id);
    }

    public void AddDeathToPlayerScore(ushort id)
    {
        playersOnLobby[id].deaths++;
        UpdateScoreBoardCapsule(id);
    }

    private void HandleScoreboardChange(ushort id, int kills, int deaths)
    {
        if (!playersOnLobby.ContainsKey(id)) return;

        playersOnLobby[id].kills = kills;
        playersOnLobby[id].deaths = deaths;
        playersOnLobby[id].scoreCapsule.playerKDTxt.SetText($"{playersOnLobby[id].kills} / {playersOnLobby[id].deaths}");
    }

    public void UpdateScoreBoardCapsule(ushort id)
    {
        if (!playersOnLobby.ContainsKey(id)) return;

        playersOnLobby[id].scoreCapsule.playerKDTxt.SetText($"{playersOnLobby[id].kills} / {playersOnLobby[id].deaths}");

        if (NetworkManager.Singleton.Server.IsRunning) SendScoreBoardChanged(id);
    }

    private void ResetScoreBoard()
    {
        foreach (KeyValuePair<ushort, PlayerData> playerKD in playersOnLobby)
        {
            playerKD.Value.kills = 0;
            playerKD.Value.deaths = 0;
            UpdateScoreBoardCapsule(playerKD.Key);
        }
    }

    private void OnAvatarLoaded(AvatarImageLoaded_t callback)
    {
        print("ON AVATAR LOADED");
        foreach (PlayerData playerData in playersOnLobby.Values)
        {
            if (playerData.playerSteamId == callback.m_steamID)
            {
                playerData.playerAvatar = GetSmallAvatar(callback.m_steamID, callback.m_iImage);
                playerData.scoreCapsule.playerImg.sprite = playerData.playerAvatar;
            }
        }
    }

    private void CreatePlayerScoreBoardCapsule(ushort id)
    {
        PlayerScoreCapsule scoreCapsule = Instantiate(playerScoreCapsulePrefab);
        scoreCapsule.transform.SetParent(scoreboardCapsulesHolder);
        scoreCapsule.transform.localScale = Vector3.one;

        scoreCapsule.SetUpCapsule(id, playersOnLobby[id].playerName);
        playersOnLobby[id].scoreCapsule = scoreCapsule;

        int imageId = SteamFriends.GetLargeFriendAvatar(playersOnLobby[id].playerSteamId);
        if (imageId != -1)
        {
            playersOnLobby[id].playerAvatar = GetSmallAvatar(playersOnLobby[id].playerSteamId, imageId);
            playersOnLobby[id].scoreCapsule.playerImg.sprite = playersOnLobby[id].playerAvatar;
        }
    }

    private Sprite GetSmallAvatar(CSteamID user, int image)
    {
        uint imageWidth;
        uint imageHeight;
        bool success = SteamUtils.GetImageSize(image, out imageWidth, out imageHeight);

        if (success && imageWidth > 0 && imageHeight > 0)
        {
            byte[] imageByte = new byte[imageWidth * imageHeight * 4];
            Texture2D returnTexture = new Texture2D((int)imageWidth, (int)imageHeight, TextureFormat.RGBA32, false, true);
            success = SteamUtils.GetImageRGBA(image, imageByte, (int)(imageWidth * imageHeight * 4));
            if (success)
            {
                returnTexture.LoadRawTextureData(imageByte);
                returnTexture.Apply();
            }
            return Sprite.Create(returnTexture, new Rect(0, 0, (int)imageWidth, (int)imageHeight), new Vector2(0.5f, 0.5f));
        }

        return Sprite.Create(new Texture2D(50, 50), new Rect(0, 0, 50, 50), new Vector2(0.5f, 0.5f));
    }

    private void DestroyPlayerScoreBoardCapsule(ushort id)
    {
        Destroy(playersOnLobby[id].scoreCapsule.gameObject);
    }
    #endregion

    private void ResetAllUITexts()
    {
        mediumTopText.SetText("");
        bigTopText.SetText("");
    }

    #region Match
    private void HandleMatchData(uint tick, byte respawnT, GameMode gamemode, MatchState matchState)
    {
        if (tick <= lastMatchDataTick) return;
        lastMatchDataTick = tick;

        respawnTime = respawnT;
        respawnTimeDebug = respawnT;

        currentGamemode = gamemode;
        gamemodeDebug = gamemode;

        currentMatchState = matchState;
        matchStateDebug = matchState;
    }

    private void ChangeMatchStatus(MatchState state)
    {
        currentMatchState = state;
        matchStateDebug = currentMatchState;

        if (!NetworkManager.Singleton.Server.IsRunning) return;
        SendMatchData();
        SteamMatchmaking.SetLobbyData(NetworkManager.Singleton.lobbyId, "status", currentMatchState.ToString());
    }

    public void StartMatch(GameMode gamemode, Scenes scene, int respawnT, int matchDuration)
    {
        currentGamemode = gamemode;
        gamemodeDebug = gamemode;

        respawnTime = respawnT;
        respawnTimeDebug = respawnT;

        matchTime = matchDuration;

        StartCoroutine(Match(scene));
    }

    public void EndMatch()
    {
        // NEEDS REWORK
        ResetAllUITexts();
        StopAllCoroutines();
        ChangeMatchStatus(MatchState.Waiting);
    }

    private void StartFreeForAllMatch()
    {

    }
    #endregion

    #region ServerSenders
    private void SendPlayerMovementFreeze(ushort id)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.playerMovementFreeze);
        message.AddUShort(id);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    private void SendPlayerMovementFree(ushort id)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.playerMovementFree);
        message.AddUShort(id);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    private void SendMatchData()
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.matchData);
        message.AddUInt(NetworkManager.Singleton.serverTick);
        message.AddByte((byte)respawnTime);
        message.AddByte((byte)currentGamemode);
        message.AddByte((byte)currentMatchState);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    private void SendMatchDataToPlayer(ushort id)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.matchData);
        message.AddUInt(NetworkManager.Singleton.serverTick);
        message.AddByte((byte)respawnTime);
        message.AddByte((byte)currentGamemode);
        message.AddByte((byte)currentMatchState);
        NetworkManager.Singleton.Server.Send(message, id);
    }

    private void SendMatchTimer()
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.matchTimer);
        message.AddFloat(timer);
        message.AddString(timerEndText);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    public void SendMatchTimerToPlayer(ushort id)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.matchTimer);
        message.AddFloat(timer);
        message.AddString(timerEndText);
        NetworkManager.Singleton.Server.Send(message, id);
    }

    private void SendScoreBoardChanged(ushort id)
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.scoreBoardChanged);
        message.AddUShort(id);
        message.AddUShort((ushort)playersOnLobby[id].kills);
        message.AddUShort((ushort)playersOnLobby[id].deaths);
        NetworkManager.Singleton.Server.SendToAll(message);
    }
    #endregion

    #region ServerToClientHandlers
    [MessageHandler((ushort)ServerToClientId.matchTimer)]
    private static void GetMatchTimer(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        MatchManager.Singleton.StopAllCoroutines();
        MatchManager.Singleton.StartCoroutine(MatchManager.Singleton.MatchTimer(message.GetFloat(), message.GetString()));
    }

    [MessageHandler((ushort)ServerToClientId.matchData)]
    private static void GetMatchData(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        MatchManager.Singleton.HandleMatchData(message.GetUInt(), message.GetByte(), (GameMode)message.GetByte(), (MatchState)message.GetByte());
    }

    [MessageHandler((ushort)ServerToClientId.scoreBoardChanged)]
    private static void GetScoreboardChange(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        MatchManager.Singleton.HandleScoreboardChange(message.GetUShort(), message.GetUShort(), message.GetUShort());
    }
    #endregion

    private IEnumerator Match(Scenes scene)
    {
        ChangeMatchStatus(MatchState.Ongoing);

        // Configures The Match For The Selected Gamemode
        switch (currentGamemode)
        {
            case GameMode.FreeForAll:
                StartFreeForAllMatch();
                break;
        }

        // Pre Match Start Countdown
        timer = preMatchStartTime;
        timerEndText = "Starting...";
        SendMatchTimer();
        while (timer > 0)
        {
            timer -= Time.deltaTime;
            bigTopText.SetText(timer > 1 ? timer.ToString("#") : timerEndText);
            yield return null;
        }
        ResetAllUITexts();
        matchingPlayers = playersOnLobby.Count;

        // Loads the map
        GameManager.Singleton.LoadScene(scene, "Match");
        ResetScoreBoard();

        // Should wait for all players to load In
        bigTopText.SetText("Waiting For Players To Load In...");
        while (GameManager.playersLoadedScene < matchingPlayers) yield return null;

        foreach (Player player in Player.list.Values) SendPlayerMovementFreeze(player.Id);

        // Match Warm Up Countdown
        timer = matchWarmUpTime;
        timerEndText = "Go!";
        SendMatchTimer();
        while (timer > 0)
        {
            timer -= Time.deltaTime;
            bigTopText.SetText(timer > 1 ? timer.ToString("#") : timerEndText);
            yield return null;
        }
        ResetAllUITexts();

        foreach (Player player in Player.list.Values) SendPlayerMovementFree(player.Id);

        // Match Countdown
        timer = matchTime;
        timerEndText = "Ending!";
        SendMatchTimer();
        while (timer > 0)
        {
            timer -= Time.deltaTime;
            bigTopText.SetText(timer > 1 ? timer.ToString("#") : timerEndText);
            yield return null;
        }
        ResetAllUITexts();

        ResetScoreBoard();
        GameManager.Singleton.LoadScene(GameManager.lobbyScene, "Match");

        ChangeMatchStatus(MatchState.Waiting);
        foreach (PlayerData playerData in playersOnLobby.Values) playerData.onQueue = false;
    }

    private IEnumerator MatchTimer(float time, string text)
    {
        timer = time;
        while (timer > 0)
        {
            timer -= Time.deltaTime;
            bigTopText.SetText(timer > 1 ? timer.ToString("#") : $"{text}");
            yield return null;
        }
        ResetAllUITexts();
    }
}
