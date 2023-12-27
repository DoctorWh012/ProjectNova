using UnityEngine;
using Riptide;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public enum GameMode : byte
{
    FreeForAll
}

public enum MatchState : byte
{
    OnMenu,
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

    public static int respawnTime { get; private set; }
    public static GameMode currentGamemode { get; private set; }
    public static MatchState currentMatchState = MatchState.OnMenu;

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

    private Dictionary<ushort, PlayerScoreCapsule> scoreCapsules = new System.Collections.Generic.Dictionary<ushort, PlayerScoreCapsule>();
    private float matchTime;
    private float timer;

    private void Awake()
    {
        Singleton = this;
    }

    private void Start()
    {
        ResetAllUITexts();
    }

    private void Update()
    {
        if (currentMatchState == MatchState.OnMenu) return;

        if (Input.GetKeyDown(SettingsManager.playerPreferences.scoreboardKey)) OpenCloseScoreBoard(true);
        if (Input.GetKeyUp(SettingsManager.playerPreferences.scoreboardKey)) OpenCloseScoreBoard(false);

        if (NetworkManager.Singleton.Server.IsRunning) AssignPingToCapsules();
    }

    #region Scoreboard
    public void OpenCloseScoreBoard(bool state)
    {
        scoreboard.SetActive(state);
    }

    private void AssignPingToCapsules()
    {
        foreach (Connection clients in NetworkManager.Singleton.Server.Clients) scoreCapsules[clients.Id].playerPingTxt.SetText(clients.SmoothRTT.ToString());
    }

    public void CreatePlayerScoreBoardCapsule(ushort id)
    {
        PlayerScoreCapsule scoreCapsule = Instantiate(playerScoreCapsulePrefab);
        scoreCapsules.Add(id, scoreCapsule);
        scoreCapsule.transform.SetParent(scoreboardCapsulesHolder);
        scoreCapsule.transform.localScale = Vector3.one;

        scoreCapsule.SetUpCapsule(id, NetworkManager.Singleton.playersOnLobby[id]);
    }

    public void DestroyPlayerScoreBoardCapsule(ushort id)
    {
        Destroy(scoreCapsules[id].gameObject);
        scoreCapsules.Remove(id);
    }
    #endregion

    private void ResetAllUITexts()
    {
        mediumTopText.SetText("");
        bigTopText.SetText("");
    }

    public void StartMatch(GameMode gamemode, string map, int respawnT, int matchDuration)
    {
        currentGamemode = gamemode;
        respawnTime = respawnT;
        matchTime = matchDuration;

        StartCoroutine(Match(map));
    }

    public void EndMatch()
    {
        StopAllCoroutines();
        currentMatchState = MatchState.Waiting;
        ResetAllUITexts();
    }

    private void StartFreeForAllMatch()
    {

    }

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

    private void SendMatchTimer(float time, string text)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.matchTimer);
        message.AddFloat(time);
        message.AddString(text);
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
    #endregion

    private IEnumerator Match(string map)
    {
        currentMatchState = MatchState.Ongoing;

        // Configures The Match For The Selected Gamemode
        switch (currentGamemode)
        {
            case GameMode.FreeForAll:
                StartFreeForAllMatch();
                break;
        }

        // Pre Match Start Countdown
        timer = preMatchStartTime;
        SendMatchTimer(preMatchStartTime, "Starting...");
        while (timer > 0)
        {
            timer -= Time.deltaTime;
            bigTopText.SetText(timer > 1 ? timer.ToString("#") : "Starting...");
            yield return null;
        }
        ResetAllUITexts();

        // Loads the map
        GameManager.Singleton.LoadScene(map, "Match");
        GameManager.Singleton.spawnPlayersAfterSceneLoad = true;

        // Should wait for all players to load In
        while (SceneManager.GetActiveScene().name != map) yield return null;
        bigTopText.SetText("Waiting For Players To Load In...");
        while (GameManager.clientsLoaded != NetworkManager.Singleton.playersOnLobby.Count) yield return null;

        foreach (Player player in Player.list.Values) SendPlayerMovementFreeze(player.Id);

        // Match Warm Up Countdown
        timer = matchWarmUpTime;
        SendMatchTimer(matchWarmUpTime, "Go!");
        while (timer > 0)
        {
            timer -= Time.deltaTime;
            bigTopText.SetText(timer > 1 ? timer.ToString("#") : "Go!");
            yield return null;
        }
        ResetAllUITexts();

        foreach (Player player in Player.list.Values) SendPlayerMovementFree(player.Id);

        // Match Countdown
        timer = matchTime;
        SendMatchTimer(matchTime, "Ending!");
        while (timer > 0)
        {
            timer -= Time.deltaTime;
            bigTopText.SetText(timer > 1 ? timer.ToString("#") : "Ending!");
            yield return null;
        }
        ResetAllUITexts();

        GameManager.Singleton.LoadScene(Scenes.Lobby, "Match");
        GameManager.Singleton.spawnPlayersAfterSceneLoad = true;
        currentMatchState = MatchState.Waiting;
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
