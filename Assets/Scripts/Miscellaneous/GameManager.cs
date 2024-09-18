using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;
using TMPro;
using Riptide;
using Steamworks;
using System.Linq;

[Serializable]
public class Scenes : IMessageSerializable
{
    public string sceneName;
    public bool spawnable;
    public bool canSpawnOnJoin;

    public Scenes(string name, bool isSpawnable, bool spawnOnJoin)
    {
        sceneName = name;
        spawnable = isSpawnable;
        canSpawnOnJoin = spawnOnJoin;
    }

    public Scenes() { }

    public void Deserialize(Message message)
    {
        sceneName = message.GetString();
        spawnable = message.GetBool();
        canSpawnOnJoin = message.GetBool();
    }

    public void Serialize(Message message)
    {
        message.AddString(sceneName);
        message.AddBool(spawnable);
        message.AddBool(canSpawnOnJoin);
    }
}

[Serializable]
public class PlayerData : IMessageSerializable
{
    public string playerName;
    public CSteamID playerSteamId;
    public Sprite playerAvatar;

    public bool onQueue;
    public bool hasLoaded;

    public int kills;
    public int deaths;

    public short ping;

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

public class PingData : IMessageSerializable
{
    public ushort playerId;
    public short ping;

    public PingData(ushort id, short playerPing)
    {
        playerId = id;
        ping = playerPing;
    }

    public PingData() { }

    public void Deserialize(Message message)
    {
        playerId = message.GetUShort();
        ping = message.GetShort();
    }

    public void Serialize(Message message)
    {
        message.AddUShort(playerId);
        message.AddShort(ping);
    }
}

[Serializable]
public struct Team
{
    public string teamName;
    public Sprite teamIcon;
}

public enum GameMode : byte
{
    FreeForAll,
    TeamDeathMatch,
}

public enum MatchState : byte
{
    Waiting,
    Ongoing,
}

public class GameManager : SettingsMenu
{
    public static bool Focused { get; private set; } = true;

    private static GameManager _singleton;
    public static GameManager Singleton
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
                Debug.Log($"{nameof(GameManager)} instance already exists, destroying duplicate");
                Destroy(value);
            }
        }
    }

    // Scene
    public static Scenes menuScene = new Scenes("NewMenu", false, false);
    public static Scenes lobbyScene = new Scenes("NewLobby", true, true);
    public static Scenes winScreen = new Scenes("WinScreen", false, false);

    public static Scenes currentScene;

    // Match Players
    public static Dictionary<ushort, PlayerData> playersOnLobby = new Dictionary<ushort, PlayerData>(); // NEEDS CLEARING
    public static Dictionary<ushort, PlayerData> allyTeam = new Dictionary<ushort, PlayerData>(); // NEEDS CLEARING
    public static Dictionary<ushort, PlayerData> enemyTeam = new Dictionary<ushort, PlayerData>(); // NEEDS CLEARING

    public static ushort[] playersPlacingFFA = new ushort[0];
    public static ushort[] playersPlacingAllyTeam = new ushort[0];
    public static ushort[] playersPlacingEnemyTeam = new ushort[0];

    // Match
    public static int respawnTime { get; private set; } = 1;
    public static GameMode currentGamemode { get; private set; } = GameMode.FreeForAll;
    public static MatchState currentMatchState = MatchState.Waiting;

    [Header("Audio")]
    [SerializeField] private AudioMixerGroup masterMixer;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI matchTimerTxt;
    [SerializeField] private GameObject matchSettingsMenu;
    [SerializeField] private GameObject settingsMenu;
    [SerializeField] private GameObject loadingScreen;
    [SerializeField] private TextMeshProUGUI loadingPercentageTxt;
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private Button respawnBtn;

    [Header("Match Settings Menu")]
    [SerializeField] private TMP_Dropdown matchGamemodeDropdown;
    [SerializeField] private TMP_Dropdown matchMapsDropdown;
    [SerializeField] private Slider matchDurationSlider;
    [SerializeField] private TextMeshProUGUI matchDurationTxt;
    [SerializeField] private Slider matchRespawnTimeSlider;
    [SerializeField] private TextMeshProUGUI matchRespawnTimeTxt;
    [SerializeField] private GameObject startMatchBtn;
    [SerializeField] private GameObject cancelMatchBtn;

    [Header("Match Settings")]
    [SerializeField] private int maxWaitForLoadTime;
    [SerializeField] private int preMatchStartTime;
    [SerializeField] private int matchWarmUpTime;

    [Header("Team Settings")]
    [SerializeField] public Team[] teams;

    [Header("Scoreboard")]
    [SerializeField] private GameObject ScoreBoardDisplay;
    [SerializeField] public GameObject scoreboardFFADisplay;
    [SerializeField] public GameObject scoreboardTeamsDisplay;

    [SerializeField] public TextMeshProUGUI allyTeamNameTxt;
    [SerializeField] public TextMeshProUGUI enemyTeamNameTxt;

    [SerializeField] public TextMeshProUGUI allyTeamScoreTxt;
    [SerializeField] public TextMeshProUGUI enemyTeamScoreTxt;

    [SerializeField] private Transform scoreboardCapsulesHolder;
    [SerializeField] private Transform allyTeamScoreboardCapsulesHolder;
    [SerializeField] private Transform enemyTeamScoreboardCapsulesHolder;

    [SerializeField] private PlayerScoreCapsule playerScoreCapsulePrefab;
    [SerializeField] private PlayerScoreCapsule playerMiniScoreCapsulePrefab;

    [Header("Killfeed")]
    [SerializeField] private KillFeedDisplay killFeedDisplayPrefab;
    [SerializeField] private Transform killFeedParent;
    [SerializeField] private float killFeedDisplayTime;

    [Header("ChatBox")]
    [SerializeField] public GameObject chatKeyIndicator;
    [SerializeField] private TextMeshProUGUI chatIndicatorKeyTxt;
    [SerializeField] private Transform chatMessagesHolder;
    [SerializeField] private ChatMessageCapsule chatMessageCapsulePrefab;
    [SerializeField] private GameObject chatBoxScroll;
    [SerializeField] private TMP_InputField chatInputField;
    [SerializeField] private int maxMessages;
    [SerializeField] private float messageDisplayTime;

    [Header("Maps")]
    [SerializeField] Scenes[] matchMaps = new Scenes[5];

    [Header("Debug")]
    [SerializeField] private List<PlayerData> playerDataDebug = new List<PlayerData>();
    [SerializeField] private int respawnTimeDebug = respawnTime;
    [SerializeField] private GameMode gamemodeDebug = currentGamemode;
    [SerializeField] private MatchState matchStateDebug = currentMatchState;

    protected Callback<AvatarImageLoaded_t> avatarLoaded;
    private List<KillFeedDisplay> killFeedDisplayList = new List<KillFeedDisplay>();
    private List<PlayerScoreCapsule> scoreCapsulesFFA = new List<PlayerScoreCapsule>();
    private List<PlayerScoreCapsule> scoreCapsulesAllyTeam = new List<PlayerScoreCapsule>();
    private List<PlayerScoreCapsule> scoreCapsulesEnemyTeam = new List<PlayerScoreCapsule>();
    private List<ChatMessageCapsule> chatMessageCapsules = new List<ChatMessageCapsule>();

    public byte allyTeamId = 0;
    public byte enemyTeamId = 0;

    public ushort allyTeamScore;
    public ushort enemyTeamScore;

    uint lastMatchDataTick;
    private float matchTime;
    private float timer;
    private float wflTimer;
    private string timerEndText = "";

    private float lastPingSentTime;

    private void Awake()
    {
        Singleton = this;
        Physics.autoSyncTransforms = true;
    }

    private void Start()
    {
        Focused = true;
        chatKeyIndicator.SetActive(false);
        avatarLoaded = Callback<AvatarImageLoaded_t>.Create(OnAvatarLoaded);

        SettingsManager.updatedPlayerPrefs += GetPreferences;
        AddMapsToDropdown();
        ConfigureScoreBoardDisplay();
        AddListenerToSettingsSliders();

        matchDurationSlider.onValueChanged.AddListener(delegate { UpdateSliderDisplayTxt(matchDurationTxt, matchDurationSlider); });
        matchRespawnTimeSlider.onValueChanged.AddListener(delegate { UpdateSliderDisplayTxt(matchRespawnTimeTxt, matchRespawnTimeSlider); });

        SettingsManager.VerifyJson();
        GetPreferences();
        DisableAllOverlays();
    }

    private void OnApplicationQuit()
    {
        SettingsManager.updatedPlayerPrefs -= GetPreferences;
    }

    private void OnDestroy()
    {
        SettingsManager.updatedPlayerPrefs -= GetPreferences;
    }

    private void Update()
    {
        if (!NetworkManager.Singleton.Client.IsConnected) return;

        if (Input.GetKeyDown(SettingsManager.playerPreferences.pauseKey)) PauseUnpause();

        if (Input.GetKeyDown(SettingsManager.playerPreferences.scoreboardKey)) OpenCloseScoreBoard();

        if (Input.GetKeyDown(SettingsManager.playerPreferences.chatKey)) EnterLeaveChat();


        if (NetworkManager.Singleton.Server.IsRunning) AssignPingServer();
    }

    public void AlterCursorState(bool free)
    {
        Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = free;
    }

    private void GetPreferences()
    {
        UpdateSettingsValues();
        chatIndicatorKeyTxt.SetText(SettingsManager.playerPreferences.chatKey.ToString());
        masterMixer.audioMixer.SetFloat("MasterVolume", Utilities.VolumeToDB(SettingsManager.playerPreferences.masterVolume));
        masterMixer.audioMixer.SetFloat("MusicVolume", Utilities.VolumeToDB(SettingsManager.playerPreferences.musicVolume));
    }

    #region Player Handling
    public void ServerIntroducePlayerToMatch(ushort id, string name, CSteamID steamId)
    {
        print($"</color=red>SIPTM {name}</color>");
        playersOnLobby.Add(id, new PlayerData(name, steamId));

        int imageId = SteamFriends.GetLargeFriendAvatar(steamId);
        if (imageId != -1) playersOnLobby[id].playerAvatar = GetSmallAvatar(playersOnLobby[id].playerSteamId, imageId);

        if (currentGamemode == GameMode.FreeForAll) CreateFFAPlayerScoreBoardCapsule();
        else PlaceNewPlayerOnTeam(id);

        RankPlayers();

        playersOnLobby[id].onQueue = currentMatchState == MatchState.Ongoing;
        playerDataDebug.Add(playersOnLobby[id]);
    }

    public void ClientIntroducePlayerToMatch(ushort id, PlayerData playerData, bool ally)
    {
        print($"<color=yellow>CIPTM {playerData.playerName}</color>");

        playersOnLobby.Add(id, playerData);

        int imageId = SteamFriends.GetLargeFriendAvatar(playersOnLobby[id].playerSteamId);
        if (imageId != -1) playersOnLobby[id].playerAvatar = GetSmallAvatar(playersOnLobby[id].playerSteamId, imageId);

        if (currentGamemode == GameMode.FreeForAll) CreateFFAPlayerScoreBoardCapsule();
        else CreateTeamPlayerScoreBoardCapsule(ally);
    }

    private void PlaceNewPlayerOnTeam(ushort id)
    {
        if (allyTeam.Count <= enemyTeam.Count)
        {
            allyTeam.Add(id, playersOnLobby[id]);
            CreateTeamPlayerScoreBoardCapsule(true);
        }
        else
        {
            enemyTeam.Add(id, playersOnLobby[id]);
            CreateTeamPlayerScoreBoardCapsule(false);
        }
    }

    public void RemovePlayerFromMatch(ushort id)
    {
        playerDataDebug.Remove(playersOnLobby[id]);
        playersOnLobby.Remove(id);

        if (currentGamemode == GameMode.FreeForAll)
        {
            Destroy(scoreCapsulesFFA[scoreCapsulesFFA.Count - 1].gameObject);
            scoreCapsulesFFA.RemoveAt(scoreCapsulesFFA.Count - 1);
        }

        else
        {
            if (allyTeam.ContainsKey(id))
            {
                Destroy(scoreCapsulesAllyTeam[scoreCapsulesAllyTeam.Count - 1].gameObject);
                scoreCapsulesAllyTeam.RemoveAt(scoreCapsulesAllyTeam.Count - 1);
                allyTeam.Remove(id);
            }
            else
            {
                Destroy(scoreCapsulesEnemyTeam[scoreCapsulesEnemyTeam.Count - 1].gameObject);
                scoreCapsulesEnemyTeam.RemoveAt(scoreCapsulesEnemyTeam.Count - 1);
                enemyTeam.Remove(id);
            }
        }

        RankPlayers();
    }

    public void RemoveAllPlayersFromMatch()
    {
        DestroyPlayersScoreBoardCapsules();
        allyTeam.Clear();
        enemyTeam.Clear();
        playersOnLobby.Clear();
        playerDataDebug.Clear();
    }
    #endregion

    #region Menus
    public void PauseUnpause()
    {
        bool state = !pauseMenu.activeInHierarchy;
        DisableAllOverlays();
        pauseMenu.SetActive(state);
        Focused = !state;
        AlterCursorState(state);
    }

    public void OpenSettingsMenu()
    {
        DisableAllOverlays();
        settingsMenu.SetActive(true);

        GetAvailableResolutions();

        UpdateSettingsValues();
        DisableAllSettingsMenus();
        EnterGeneralMenu();
    }

    public void ReturnToPauseMenu()
    {
        DisableAllOverlays();
        pauseMenu.SetActive(true);
    }

    public void OpenCloseMatchSettingsMenu()
    {
        Focused = !Focused;
        AlterCursorState(!Focused);

        startMatchBtn.SetActive(false);
        cancelMatchBtn.SetActive(false);

        if (GameManager.currentMatchState == MatchState.Waiting) startMatchBtn.SetActive(true);
        else cancelMatchBtn.SetActive(true);

        matchSettingsMenu.SetActive(!Focused);
    }

    public void Respawn()
    {
        if (!Player.list.TryGetValue(NetworkManager.Singleton.Client.Id, out Player player)) return;
        if (player.playerHealth.currentPlayerState == PlayerState.Dead) return;

        if (NetworkManager.Singleton.Server.IsRunning) player.playerHealth.InstaKill();
        else SendSuicideMessage();

        respawnBtn.interactable = false;
        Invoke("EnableRespawnBtn", 10f);
    }

    private void EnableRespawnBtn()
    {
        respawnBtn.interactable = true;
    }

    private void AddMapsToDropdown()
    {
        for (int i = 0; i < matchMaps.Length; i++) matchMapsDropdown.options.Add(new TMP_Dropdown.OptionData(matchMaps[i].sceneName));
    }

    public void ExitMatch()
    {
        if (NetworkManager.Singleton.Server.IsRunning) NetworkManager.Singleton.Server.Stop();
        if (!Focused) PauseUnpause();
        DisableAllOverlays();
        NetworkManager.Singleton.Client.Disconnect();
        SteamMatchmaking.LeaveLobby(NetworkManager.Singleton.lobbyId);
    }

    public void StartMatch()
    {
        Scenes map = matchMapsDropdown.value == 0 ? GetRandomMap() : matchMaps[matchMapsDropdown.value - 1];
        StartMatch((GameMode)matchGamemodeDropdown.value, map, (int)matchRespawnTimeSlider.value, (int)matchDurationSlider.value);
        OpenCloseMatchSettingsMenu();
    }

    public void CancelMatch()
    {
        GameManager.Singleton.EndMatch();
        OpenCloseMatchSettingsMenu();
    }

    private void DisableAllOverlays()
    {
        pauseMenu.SetActive(false);
        settingsMenu.SetActive(false);
        matchSettingsMenu.SetActive(false);
        ScoreBoardDisplay.SetActive(false);
        chatInputField.gameObject.SetActive(false);
        chatBoxScroll.SetActive(false);
    }

    private Scenes GetRandomMap()
    {
        return matchMaps[UnityEngine.Random.Range(0, matchMaps.Length)];
    }
    #endregion

    #region Scoreboard
    public void OpenCloseScoreBoard()
    {
        if (pauseMenu.activeInHierarchy || settingsMenu.activeInHierarchy || matchSettingsMenu.activeInHierarchy) return;
        bool state = !ScoreBoardDisplay.activeInHierarchy;
        DisableAllOverlays();
        ScoreBoardDisplay.SetActive(state);
        Focused = !state;
        AlterCursorState(state);
    }

    private void ConfigureScoreBoardDisplay()
    {
        scoreboardFFADisplay.SetActive(false);
        scoreboardTeamsDisplay.SetActive(false);

        if (currentGamemode == GameMode.FreeForAll) scoreboardFFADisplay.SetActive(true);
        else scoreboardTeamsDisplay.SetActive(true);
    }

    private void CreateFFAPlayerScoreBoardCapsule()
    {
        scoreCapsulesFFA.Add(Instantiate(playerScoreCapsulePrefab, scoreboardCapsulesHolder));
    }

    private void CreateTeamPlayerScoreBoardCapsule(bool ally)
    {
        if (ally) scoreCapsulesAllyTeam.Add(Instantiate(playerMiniScoreCapsulePrefab, allyTeamScoreboardCapsulesHolder));
        else scoreCapsulesEnemyTeam.Add(Instantiate(playerMiniScoreCapsulePrefab, enemyTeamScoreboardCapsulesHolder));
    }

    private void HandlePlayersPings(PingData[] pingDatas)
    {
        foreach (PingData pingData in pingDatas)
        {
            playersOnLobby[pingData.playerId].ping = pingData.ping;
            GetScoreCapsuleOfPlayer(pingData.playerId).playerPingTxt.SetText(pingData.ping.ToString());
        }
    }

    private void AssignPingServer()
    {
        foreach (Connection connection in NetworkManager.Singleton.Server.Clients)
        {
            if (!playersOnLobby.ContainsKey(connection.Id) || GetScoreCapsuleOfPlayer(connection.Id) == null) continue;
            playersOnLobby[connection.Id].ping = connection.SmoothRTT;
            GetScoreCapsuleOfPlayer(connection.Id).playerPingTxt.SetText(playersOnLobby[connection.Id].ping.ToString());
        }

        if (Time.time > lastPingSentTime + 1) { SendPlayersPings(); lastPingSentTime = Time.time; }
    }

    public void AddKillToPlayerScore(ushort id)
    {
        playersOnLobby[id].kills++;
        RankPlayers();
        if (NetworkManager.Singleton.Server.IsRunning) SendScoreBoardChanged(id);
    }

    public void AddDeathToPlayerScore(ushort id)
    {
        playersOnLobby[id].deaths++;
        UpdateScoreBoardCapsule(id);
        if (NetworkManager.Singleton.Server.IsRunning) SendScoreBoardChanged(id);
    }

    private void HandleScoreboardChange(ushort id, int kills, int deaths, ushort[] playersPlacing, ushort[] alliesPlacing, ushort[] enemiesPlacing)
    {
        playersOnLobby[id].kills = kills;
        playersOnLobby[id].deaths = deaths;

        playersPlacingFFA = playersPlacing;
        playersPlacingAllyTeam = alliesPlacing;
        playersPlacingEnemyTeam = enemiesPlacing;

        UpdateScoreBoardCapsules();
    }

    public void UpdateScoreBoardCapsules()
    {
        if (currentGamemode == GameMode.FreeForAll)
        {
            for (int i = 0; i < playersPlacingFFA.Length; i++)
            {
                PlayerData playerData = playersOnLobby[playersPlacingFFA[i]];
                scoreCapsulesFFA[i].SetUpCapsule(playersPlacingFFA[i], playerData.playerName, playerData.playerAvatar, playerData.kills, playerData.deaths);
            }
        }

        else
        {
            for (int i = 0; i < playersPlacingAllyTeam.Length; i++)
            {
                PlayerData playerData = playersOnLobby[playersPlacingAllyTeam[i]];
                scoreCapsulesAllyTeam[i].SetUpCapsule(playersPlacingAllyTeam[i], playerData.playerName, playerData.playerAvatar, playerData.kills, playerData.deaths);
            }

            for (int i = 0; i < playersPlacingEnemyTeam.Length; i++)
            {
                PlayerData playerData = playersOnLobby[playersPlacingEnemyTeam[i]];
                scoreCapsulesEnemyTeam[i].SetUpCapsule(playersPlacingEnemyTeam[i], playerData.playerName, playerData.playerAvatar, playerData.kills, playerData.deaths);
            }
        }
    }

    private void UpdateScoreBoardCapsule(ushort id)
    {
        if (currentGamemode == GameMode.FreeForAll)
        {
            PlayerData playerData = playersOnLobby[id];
            scoreCapsulesFFA[Array.IndexOf(playersPlacingFFA, id)].SetUpCapsule(id, playerData.playerName, playerData.playerAvatar, playerData.kills, playerData.deaths);
        }

        else
        {
            PlayerData playerData = playersOnLobby[id];
            if (allyTeam.ContainsKey(id))
                scoreCapsulesAllyTeam[Array.IndexOf(playersPlacingAllyTeam, id)].SetUpCapsule(id, playerData.playerName, playerData.playerAvatar, playerData.kills, playerData.deaths);

            else scoreCapsulesEnemyTeam[Array.IndexOf(playersPlacingEnemyTeam, id)].SetUpCapsule(id, playerData.playerName, playerData.playerAvatar, playerData.kills, playerData.deaths);
        }
    }

    private void ResetScoreBoard()
    {
        foreach (KeyValuePair<ushort, PlayerData> playerKD in playersOnLobby)
        {
            playerKD.Value.kills = 0;
            playerKD.Value.deaths = 0;
            if (NetworkManager.Singleton.Server.IsRunning) SendScoreBoardReset();
        }

        RankPlayers();
    }

    private void DestroyPlayersScoreBoardCapsules()
    {
        for (int i = 0; i < scoreCapsulesFFA.Count; i++) Destroy(scoreCapsulesFFA[i].gameObject);
        scoreCapsulesFFA.Clear();

        for (int i = 0; i < scoreCapsulesAllyTeam.Count; i++) Destroy(scoreCapsulesAllyTeam[i].gameObject);
        scoreCapsulesAllyTeam.Clear();

        for (int i = 0; i < scoreCapsulesEnemyTeam.Count; i++) Destroy(scoreCapsulesEnemyTeam[i].gameObject);
        scoreCapsulesEnemyTeam.Clear();
    }

    private PlayerScoreCapsule GetScoreCapsuleOfPlayer(ushort id)
    {
        if (currentGamemode == GameMode.FreeForAll)
        {
            for (int i = 0; i < scoreCapsulesFFA.Count; i++) if (scoreCapsulesFFA[i].playerId == id) return scoreCapsulesFFA[i];
            return null;
        }

        else
        {
            if (allyTeam.ContainsKey(id)) { for (int i = 0; i < scoreCapsulesAllyTeam.Count; i++) if (scoreCapsulesAllyTeam[i].playerId == id) return scoreCapsulesAllyTeam[i]; }
            else for (int i = 0; i < scoreCapsulesEnemyTeam.Count; i++) if (scoreCapsulesEnemyTeam[i].playerId == id) return scoreCapsulesEnemyTeam[i];
            return null;
        }
    }

    private void RankPlayers()
    {
        switch (currentGamemode)
        {
            case GameMode.FreeForAll:
                // Rank Players By Kills
                playersPlacingFFA = playersOnLobby.OrderByDescending(x => x.Value.kills).Select(x => x.Key).ToArray();
                break;

            case GameMode.TeamDeathMatch:
                // Rank Players By Kills
                playersPlacingAllyTeam = allyTeam.OrderByDescending(x => x.Value.kills).Select(x => x.Key).ToArray();
                playersPlacingEnemyTeam = enemyTeam.OrderByDescending(x => x.Value.kills).Select(x => x.Key).ToArray();
                break;
        }

        UpdateScoreBoardCapsules();
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

    private void OnAvatarLoaded(AvatarImageLoaded_t callback)
    {
        foreach (PlayerData playerData in playersOnLobby.Values)
        {
            if (playerData.playerSteamId != callback.m_steamID) continue;
            playerData.playerAvatar = GetSmallAvatar(callback.m_steamID, callback.m_iImage);
            UpdateScoreBoardCapsules();
        }
    }
    #endregion

    #region KillFeed
    public void SpawnKillFeedCapsule(string killer, Sprite killMethod, string victim)
    {
        KillFeedDisplay killFeedCapsule = Instantiate(killFeedDisplayPrefab, killFeedParent);
        killFeedDisplayList.Add(killFeedCapsule);
        killFeedCapsule.transform.localScale = Vector3.one;

        killFeedCapsule.killerTxt.SetText(killer);
        killFeedCapsule.killMethodImg.sprite = killMethod;
        killFeedCapsule.victimTxt.SetText(victim);

        Invoke(nameof(RemoveOldKillCapsule), killFeedDisplayTime);
    }

    private void RemoveOldKillCapsule()
    {
        Destroy(killFeedDisplayList[0].gameObject);
        killFeedDisplayList.Remove(killFeedDisplayList[0]);
    }
    #endregion

    #region Chat
    private void HandlePlayerChat(ushort id, string chatMsg)
    {
        if (chatMessageCapsules.Count > maxMessages)
        {
            Destroy(chatMessageCapsules[0].gameObject);
            chatMessageCapsules.Remove(chatMessageCapsules[0]);
        }

        ChatMessageCapsule chatMsgCapsule = Instantiate(chatMessageCapsulePrefab, chatMessagesHolder);
        chatMessageCapsules.Add(chatMsgCapsule);
        chatMsgCapsule.transform.localScale = Vector3.one;

        chatMsgCapsule.SetupChatMessage($"<<color=#00FFFF>{playersOnLobby[id].playerName}</color>> {chatMsg}");
        LayoutRebuilder.ForceRebuildLayoutImmediate(chatMessagesHolder.GetComponent<RectTransform>());

        if (NetworkManager.Singleton.Server.IsRunning) SendPlayerChat(id, chatMsg);
    }

    public void EnterLeaveChat()
    {
        if (pauseMenu.activeInHierarchy || settingsMenu.activeInHierarchy || matchSettingsMenu.activeInHierarchy) return;
        bool state = !chatInputField.gameObject.activeInHierarchy;
        DisableAllOverlays();

        Focused = !state;
        chatBoxScroll.SetActive(state);
        chatKeyIndicator.SetActive(!state);
        AlterCursorState(state);

        if (state)
        {
            EnableDisableAllMessages(true);
            chatInputField.gameObject.SetActive(state);
            chatInputField.ActivateInputField();
        }

        else
        {
            EnableDisableAllMessages(false);
            if (!string.IsNullOrEmpty(chatInputField.text.Trim()))
            {
                HandlePlayerChat(NetworkManager.Singleton.Client.Id, chatInputField.text.Trim());
                if (!NetworkManager.Singleton.Server.IsRunning) SendClientChat(chatInputField.text.Trim());
            }
            chatInputField.text = string.Empty;
            chatInputField.gameObject.SetActive(state);
        }
    }

    private void EnableDisableAllMessages(bool state)
    {
        for (int i = 0; i < chatMessageCapsules.Count; i++)
        {
            if (state) chatMessageCapsules[i].ActivateMessage();
            else chatMessageCapsules[i].DeactivateMessage();
        }
    }

    public void ClearChat()
    {
        for (int i = 0; i < chatMessageCapsules.Count; i++) Destroy(chatMessageCapsules[i].gameObject);
        chatMessageCapsules.Clear();
    }
    #endregion

    #region Match
    public void CatchUpMatchData(byte respawnT, GameMode gamemode, MatchState matchState)
    {
        respawnTime = respawnT;
        respawnTimeDebug = respawnT;

        currentGamemode = gamemode;
        gamemodeDebug = gamemode;

        currentMatchState = matchState;
        matchStateDebug = matchState;
        ConfigureScoreBoardDisplay();
    }

    public void HandleMatchData(uint tick, byte respawnT, GameMode gamemode, MatchState matchState, ushort[] placingFFA, ushort[] serverAllies, ushort[] serverEnemies, byte serverAlliesTeamId, byte serverEnemiesTeamId)
    {
        if (tick <= lastMatchDataTick) return;
        lastMatchDataTick = tick;

        allyTeam.Clear();
        enemyTeam.Clear();

        playersPlacingFFA = placingFFA;
        if (serverAllies.Contains(NetworkManager.Singleton.Client.Id))
        {
            allyTeamId = serverAlliesTeamId;
            enemyTeamId = serverEnemiesTeamId;

            playersPlacingAllyTeam = serverAllies;
            playersPlacingEnemyTeam = serverEnemies;

            for (int i = 0; i < serverAllies.Length; i++) allyTeam.Add(serverAllies[i], playersOnLobby[serverAllies[i]]);
            for (int i = 0; i < serverEnemies.Length; i++) enemyTeam.Add(serverEnemies[i], playersOnLobby[serverEnemies[i]]);
        }

        else
        {
            allyTeamId = serverEnemiesTeamId;
            enemyTeamId = serverAlliesTeamId;

            playersPlacingAllyTeam = serverEnemies;
            playersPlacingEnemyTeam = serverAllies;

            for (int i = 0; i < serverEnemies.Length; i++) allyTeam.Add(serverEnemies[i], playersOnLobby[serverEnemies[i]]);
            for (int i = 0; i < serverAllies.Length; i++) enemyTeam.Add(serverAllies[i], playersOnLobby[serverAllies[i]]);
        }

        enemyTeamNameTxt.SetText(teams[enemyTeamId].teamName);
        allyTeamNameTxt.SetText(teams[allyTeamId].teamName);

        respawnTime = respawnT;
        respawnTimeDebug = respawnT;

        currentGamemode = gamemode;
        gamemodeDebug = gamemode;

        switch (currentGamemode)
        {
            case GameMode.FreeForAll:
                StartFreeForAllMatch();
                break;
            case GameMode.TeamDeathMatch:
                StartTeamDeathMatch();
                break;
        }

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

    private void StartMatch(GameMode gamemode, Scenes scene, int respawnT, int matchDuration)
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
        timer = 0;
        timerEndText = "";
        matchTimerTxt.SetText("");
        SendMatchTimer();

        StopAllCoroutines();
        ChangeMatchStatus(MatchState.Waiting);
    }

    private void StartFreeForAllMatch()
    {
        DestroyPlayersScoreBoardCapsules();
        for (int i = 0; i < playersOnLobby.Count; i++) CreateFFAPlayerScoreBoardCapsule();
        ConfigureScoreBoardDisplay();

        if (NetworkManager.Singleton.Server.IsRunning) RankPlayers();
        else UpdateScoreBoardCapsules();
    }

    private void StartTeamDeathMatch()
    {
        if (NetworkManager.Singleton.Server.IsRunning) SplitPlayersIntoTeams();

        DestroyPlayersScoreBoardCapsules();
        for (int i = 0; i < allyTeam.Count; i++) CreateTeamPlayerScoreBoardCapsule(true);
        for (int i = 0; i < enemyTeam.Count; i++) CreateTeamPlayerScoreBoardCapsule(false);
        ConfigureScoreBoardDisplay();

        if (NetworkManager.Singleton.Server.IsRunning) RankPlayers();
        else UpdateScoreBoardCapsules();
    }

    private void SplitPlayersIntoTeams()
    {
        allyTeam.Clear();
        enemyTeam.Clear();

        allyTeamId = (byte)UnityEngine.Random.Range(0, teams.Length);
        enemyTeamId = (byte)UnityEngine.Random.Range(0, teams.Length);

        allyTeamNameTxt.SetText(teams[allyTeamId].teamName);
        enemyTeamNameTxt.SetText(teams[enemyTeamId].teamName);

        List<ushort> playersKeys = playersOnLobby.Keys.ToList();
        playersKeys.Remove(NetworkManager.Singleton.Client.Id);
        playersKeys.Shuffle();

        allyTeam.Add(NetworkManager.Singleton.Client.Id, playersOnLobby[NetworkManager.Singleton.Client.Id]);
        for (int i = 0; i < playersKeys.Count; i++)
        {
            if (i % 2 == 0) enemyTeam.Add(playersKeys[i], playersOnLobby[playersKeys[i]]);
            else allyTeam.Add(playersKeys[i], playersOnLobby[playersKeys[i]]);
        }
    }

    private IEnumerator Match(Scenes scene)
    {
        // Configures The Match For The Selected Gamemode
        switch (currentGamemode)
        {
            case GameMode.FreeForAll:
                StartFreeForAllMatch();
                break;
            case GameMode.TeamDeathMatch:
                StartTeamDeathMatch();
                break;
        }
        ResetScoreBoard();
        ChangeMatchStatus(MatchState.Ongoing);

        // Pre Match Start Countdown
        timer = preMatchStartTime;
        timerEndText = "Starting...";
        SendMatchTimer();

        while (!CountdownTimer()) yield return null;

        // Loads the map
        GameManager.Singleton.LoadScene(scene, "Match");

        // Waits for host to load in
        while (!playersOnLobby[NetworkManager.Singleton.Client.Id].hasLoaded) yield return null;

        // Should wait for all players to load In
        matchTimerTxt.SetText("Waiting For Players To Load In...");
        wflTimer = maxWaitForLoadTime;
        while (!WaitForLoadingPlayersTimer()) yield return null;

        // Match Warm Up Countdown
        timer = matchWarmUpTime;
        timerEndText = "Go!";
        SendMatchTimer();
        while (!CountdownTimer()) yield return null;
        matchTimerTxt.SetText("");

        // Match Countdown
        timer = matchTime;
        timerEndText = "Ending!";
        SendMatchTimer();
        while (!CountdownTimer()) yield return null;
        matchTimerTxt.SetText("");

        // Match Over
        // Loads the map
        GameManager.Singleton.LoadScene(winScreen, "Match");

        // Waits for host to load in
        while (!playersOnLobby[NetworkManager.Singleton.Client.Id].hasLoaded) yield return null;

        // Should wait for all players to load In
        matchTimerTxt.SetText("Waiting For Players To Load In...");
        wflTimer = maxWaitForLoadTime;
        while (!WaitForLoadingPlayersTimer()) yield return null;

        yield return new WaitForSeconds(5);

        GameManager.Singleton.LoadScene(lobbyScene, "Match");
        ResetScoreBoard();

        // Waits for host to load in
        while (!playersOnLobby[NetworkManager.Singleton.Client.Id].hasLoaded) yield return null;

        // Should wait for all players to load In
        matchTimerTxt.SetText("Waiting For Players To Load In...");
        wflTimer = maxWaitForLoadTime;
        while (!WaitForLoadingPlayersTimer()) yield return null;

        ChangeMatchStatus(MatchState.Waiting);
        foreach (PlayerData playerData in playersOnLobby.Values) playerData.onQueue = false;
    }

    private IEnumerator MatchTimer(float time, string text)
    {
        timer = time;
        timerEndText = text;
        while (!CountdownTimer()) yield return null;
    }

    private bool CountdownTimer()
    {
        if (timer > 0)
        {
            timer -= Time.deltaTime;
            matchTimerTxt.SetText(timer > 1 ? timer.ToString("#") : timerEndText);
            return false;
        }
        matchTimerTxt.SetText("");
        return true;
    }
    #endregion

    #region SceneManaging
    private void HandlePlayerLoadedScene(ushort id)
    {
        print($"<color=red>Player {GameManager.playersOnLobby[id].playerName} Loaded Scene</color>");
        playersOnLobby[id].hasLoaded = true;

        // Sends Spawned Players To The Player Who Loaded The Scene
        foreach (Player otherPlayer in Player.list.Values)
        {
            otherPlayer.SendPlayersToPlayer(id);
            otherPlayer.playerShooting.SendWeaponSyncToPlayer(id);
        }

        GunSpawnManager.Instance.SendWeaponsSpawnersDataToPlayer(id);
        GameManager.Singleton.SendMatchTimerToPlayer(id);

        if (GameManager.playersOnLobby[id].onQueue && !currentScene.canSpawnOnJoin) return;
        if (currentScene.spawnable) Player.SpawnPlayer(id, GameManager.playersOnLobby[id].playerName, Vector3.zero);
    }

    public void LoadScene(Scenes scene, string caller)
    {
        print($"<color=yellow>{new string('-', 30)}</color>");
        print($"<color=yellow> Caller {caller} asked to load scene {scene.sceneName}</color>");

        StartCoroutine(LoadSceneAsync(scene));
    }

    private void ResetPlayersLoaded()
    {
        foreach (KeyValuePair<ushort, PlayerData> player in playersOnLobby) player.Value.hasLoaded = false;
    }

    private IEnumerator LoadSceneAsync(Scenes scene)
    {
        print($"<color=yellow>Starting to load Scene {scene.sceneName} currently on Scene {SceneManager.GetActiveScene().name}</color>");
        print("<color=blue>Reset PLS Counter</color>");
        ResetPlayersLoaded();

        // Start Loading
        loadingScreen.SetActive(true);
        AsyncOperation sceneLoadingOp = SceneManager.LoadSceneAsync(scene.sceneName);

        // Update Percentage
        while (!sceneLoadingOp.isDone)
        {
            loadingPercentageTxt.SetText($"Loading {(sceneLoadingOp.progress * 100).ToString("#")}%");
            yield return null;
        }

        // Finish Loading
        currentScene = scene;
        loadingScreen.SetActive(false);
        SpectateCameraManager.Singleton.mapCamera = FindObjectOfType<Camera>().gameObject;

        if (NetworkManager.Singleton.Server.IsRunning)
        {
            playersOnLobby[NetworkManager.Singleton.Client.Id].hasLoaded = true;
            if (currentScene.spawnable) Player.SpawnPlayer(NetworkManager.Singleton.Client.Id, GameManager.playersOnLobby[NetworkManager.Singleton.Client.Id].playerName, Vector3.zero);
            SendSceneChanged(scene);
        }
        else SendClientSceneLoaded();

        print($"<color=yellow>Loaded Scene</color>");
        print($"<color=yellow>{new string('-', 30)}</color>");
    }

    private bool CheckForAllPlayersLoadedScene()
    {
        foreach (KeyValuePair<ushort, PlayerData> player in playersOnLobby) if (!player.Value.hasLoaded) return false;
        return true;
    }

    private bool WaitForLoadingPlayersTimer()
    {
        if (!CheckForAllPlayersLoadedScene())
        {
            wflTimer -= Time.deltaTime;
            if (wflTimer <= 0) KickSceneLoadingStuckPlayers();
            return false;
        }
        matchTimerTxt.SetText("");
        return true;
    }

    private void KickSceneLoadingStuckPlayers()
    {
        foreach (KeyValuePair<ushort, PlayerData> player in playersOnLobby)
        {
            if (player.Value.hasLoaded) continue;
            NetworkManager.Singleton.Server.DisconnectClient(player.Key);
        }
    }
    #endregion

    #region ServerSenders
    private void SendPlayersPings()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.playerPing);

        List<PingData> pingDatas = new List<PingData>();
        foreach (KeyValuePair<ushort, PlayerData> playerData in playersOnLobby) pingDatas.Add(new PingData(playerData.Key, playerData.Value.ping));

        message.AddSerializables(pingDatas.ToArray());
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    private void SendPlayerChat(ushort id, string chatMsg)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.playerChat);
        message.AddUShort(id);
        message.AddString(chatMsg);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    private void SendSceneChanged(Scenes scene)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.sceneChanged);
        message.AddSerializable<Scenes>(scene);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    public void SendSceneToPlayer(ushort id, Scenes scene)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.sceneChanged);
        message.AddSerializable<Scenes>(scene);
        NetworkManager.Singleton.Server.Send(message, id);
    }

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

        message.AddUShorts(playersPlacingFFA);
        message.AddUShorts(playersPlacingAllyTeam);
        message.AddUShorts(playersPlacingEnemyTeam);

        message.AddByte(allyTeamId);
        message.AddByte(enemyTeamId);
        NetworkManager.Singleton.Server.SendToAll(message);
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

        message.AddUShorts(playersPlacingFFA);
        message.AddUShorts(playersPlacingAllyTeam);
        message.AddUShorts(playersPlacingEnemyTeam);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    private void SendScoreBoardReset()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.scoreBoardReset);
        NetworkManager.Singleton.Server.SendToAll(message);
    }
    #endregion

    #region ClientSenders
    private void SendClientSceneLoaded()
    {
        Message message = Message.Create(MessageSendMode.Reliable, ClientToServerId.playerLoadedScene);
        NetworkManager.Singleton.Client.Send(message);
    }

    private void SendSuicideMessage()
    {
        Message message = Message.Create(MessageSendMode.Reliable, ClientToServerId.playerSuicide);
        NetworkManager.Singleton.Client.Send(message);
    }

    private void SendClientChat(string chatMsg)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ClientToServerId.playerChat);
        message.AddString(chatMsg);
        NetworkManager.Singleton.Client.Send(message);
    }
    #endregion

    #region ServerToClientHandlers
    [MessageHandler((ushort)ServerToClientId.playerPing)]
    private static void ReceivePlayersPings(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        GameManager.Singleton.HandlePlayersPings(message.GetSerializables<PingData>());
    }

    [MessageHandler((ushort)ServerToClientId.playerChat)]
    private static void ReceivePlayersChat(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        ushort id = message.GetUShort();
        if (id == NetworkManager.Singleton.Client.Id) return;
        GameManager.Singleton.HandlePlayerChat(id, message.GetString());
    }

    [MessageHandler((ushort)ServerToClientId.sceneChanged)]
    private static void ReceiveSceneChanged(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        GameManager.Singleton.LoadScene(message.GetSerializable<Scenes>(), "ReceiveSceneChanged");
    }

    [MessageHandler((ushort)ServerToClientId.matchTimer)]
    private static void GetMatchTimer(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        GameManager.Singleton.StopAllCoroutines();
        GameManager.Singleton.StartCoroutine(GameManager.Singleton.MatchTimer(message.GetFloat(), message.GetString()));
    }

    [MessageHandler((ushort)ServerToClientId.matchData)]
    private static void GetMatchData(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        GameManager.Singleton.HandleMatchData(message.GetUInt(), message.GetByte(), (GameMode)message.GetByte(), (MatchState)message.GetByte(), message.GetUShorts(), message.GetUShorts(), message.GetUShorts(), message.GetByte(), message.GetByte());
    }

    [MessageHandler((ushort)ServerToClientId.scoreBoardChanged)]
    private static void GetScoreboardChange(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        GameManager.Singleton.HandleScoreboardChange(message.GetUShort(), message.GetUShort(), message.GetUShort(), message.GetUShorts(), message.GetUShorts(), message.GetUShorts());
    }

    [MessageHandler((ushort)ServerToClientId.scoreBoardReset)]
    private static void GetScoreboardReset(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        GameManager.Singleton.ResetScoreBoard();
    }
    #endregion

    #region ClientToServerHandlers
    [MessageHandler((ushort)ClientToServerId.playerLoadedScene)]
    private static void ReceiveClientLoadedScene(ushort fromClientId, Message message)
    {
        GameManager.Singleton.HandlePlayerLoadedScene(fromClientId);
    }

    [MessageHandler((ushort)ClientToServerId.playerSuicide)]
    private static void ReceiveSuicide(ushort fromClientId, Message message)
    {
        if (Player.list.TryGetValue(fromClientId, out Player player))
        {
            player.playerHealth.InstaKill();
        }
    }

    [MessageHandler((ushort)ClientToServerId.playerChat)]
    private static void ReceiveClientChat(ushort fromClientId, Message message)
    {
        GameManager.Singleton.HandlePlayerChat(fromClientId, message.GetString());
    }
    #endregion
}
