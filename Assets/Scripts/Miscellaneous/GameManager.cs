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

    public static Scenes menuScene = new Scenes("NewMenu", false, false);
    public static Scenes lobbyScene = new Scenes("NewLobby", true, true);
    public static Scenes winScreen = new Scenes("WinScreen", false, false);

    public static Scenes currentScene;
    public static int playersLoadedScene;

    [Header("UI")]
    [SerializeField] private GameObject matchSettingsMenu;
    [SerializeField] private GameObject settingsMenu;
    [SerializeField] private GameObject loadingScreen;
    [SerializeField] private TextMeshProUGUI loadingPercentageTxt;
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private Button respawnBtn;
    [SerializeField] private KillFeedDisplay killFeedDisplayPrefab;
    [SerializeField] private Transform killFeedParent;
    [SerializeField] private float killFeedDisplayTime;

    [Header("Match Settings Menu")]
    [SerializeField] private TMP_Dropdown matchMapsDropdown;
    [SerializeField] private Slider matchDurationSlider;
    [SerializeField] private TextMeshProUGUI matchDurationTxt;
    [SerializeField] private Slider matchRespawnTimeSlider;
    [SerializeField] private TextMeshProUGUI matchRespawnTimeTxt;
    [SerializeField] private GameObject startMatchBtn;
    [SerializeField] private GameObject cancelMatchBtn;

    [Header("Audio")]
    [SerializeField] private AudioMixerGroup masterMixer;

    [SerializeField] Scenes[] matchMaps = new Scenes[5];
    private List<KillFeedDisplay> killFeedDisplayList = new List<KillFeedDisplay>();

    private void Awake()
    {
        Singleton = this;
        Physics.autoSyncTransforms = true;
    }

    private void Start()
    {
        Focused = true;

        SettingsManager.updatedPlayerPrefs += GetPreferences;
        AddMapsToDropdown();
        AddListenerToSettingsSliders();

        matchDurationSlider.onValueChanged.AddListener(delegate { UpdateSliderDisplayTxt(matchDurationTxt, matchDurationSlider); });
        matchRespawnTimeSlider.onValueChanged.AddListener(delegate { UpdateSliderDisplayTxt(matchRespawnTimeTxt, matchRespawnTimeSlider); });

        SettingsManager.VerifyJson();
        GetPreferences();
        DisableAllMenus();
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
    }

    #region Menus
    public void PauseUnpause()
    {
        Focused = !Focused;
        DisableAllMenus();
        pauseMenu.SetActive(!Focused);

        Cursor.lockState = Focused ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !Focused;
    }

    public void OpenSettingsMenu()
    {
        DisableAllMenus();
        settingsMenu.SetActive(true);

        GetAvailableResolutions();

        UpdateSettingsValues();
        DisableAllSettingsMenus();
        EnterGeneralMenu();
    }

    public void ReturnToPauseMenu()
    {
        DisableAllMenus();
        pauseMenu.SetActive(true);
    }

    public void OpenCloseMatchSettingsMenu()
    {
        Focused = !Focused;

        startMatchBtn.SetActive(false);
        cancelMatchBtn.SetActive(false);

        if (MatchManager.currentMatchState == MatchState.Waiting) startMatchBtn.SetActive(true);
        else cancelMatchBtn.SetActive(true);

        matchSettingsMenu.SetActive(!Focused);

        Cursor.lockState = Focused ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !Focused;
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
        DisableAllMenus();
        NetworkManager.Singleton.Client.Disconnect();
        SteamMatchmaking.LeaveLobby(NetworkManager.Singleton.lobbyId);
    }

    public void StartMatch()
    {
        Scenes map = matchMapsDropdown.value == 0 ? GetRandomMap() : matchMaps[matchMapsDropdown.value - 1];
        MatchManager.Singleton.StartMatch(GameMode.FreeForAll, map, (int)matchRespawnTimeSlider.value, (int)matchDurationSlider.value);
        OpenCloseMatchSettingsMenu();
    }

    public void CancelMatch()
    {
        MatchManager.Singleton.EndMatch();
        OpenCloseMatchSettingsMenu();
    }

    private void DisableAllMenus()
    {
        pauseMenu.SetActive(false);
        settingsMenu.SetActive(false);
        matchSettingsMenu.SetActive(false);
    }

    private Scenes GetRandomMap()
    {
        return matchMaps[UnityEngine.Random.Range(0, matchMaps.Length)];
    }
    #endregion

    #region KillFeed
    public void SpawnKillFeedCapsule(string killer, Sprite killMethod, string victim)
    {
        KillFeedDisplay killFeedCapsule = Instantiate(killFeedDisplayPrefab);
        killFeedDisplayList.Add(killFeedCapsule);
        killFeedCapsule.transform.SetParent(killFeedParent);
        killFeedCapsule.transform.localScale = Vector3.one;

        killFeedCapsule.killerTxt.SetText(killer);
        killFeedCapsule.killMethodImg.sprite = killMethod;
        killFeedCapsule.victimTxt.SetText(victim);

        Invoke("RemoveOldKillCapsule", killFeedDisplayTime);
    }

    private void RemoveOldKillCapsule()
    {
        Destroy(killFeedDisplayList[0].gameObject);
        killFeedDisplayList.Remove(killFeedDisplayList[0]);
    }
    #endregion

    #region Preferences
    private void GetPreferences()
    {
        UpdateSettingsValues();
        masterMixer.audioMixer.SetFloat("MasterVolume", Utilities.VolumeToDB(SettingsManager.playerPreferences.masterVolume));
        masterMixer.audioMixer.SetFloat("MusicVolume", Utilities.VolumeToDB(SettingsManager.playerPreferences.musicVolume));
    }
    #endregion

    #region Spawning

    #endregion

    #region SceneManaging
    private void HandlePlayerLoadedScene(ushort id)
    {
        print($"<color=red>Player {MatchManager.playersOnLobby[id].playerName} Loaded Scene</color>");
        // Sends Spawned Players To The Player Who Loaded The Scene
        foreach (Player otherPlayer in Player.list.Values)
        {
            otherPlayer.SendPlayersToPlayer(id);
            otherPlayer.playerShooting.SendWeaponSyncToPlayer(id);
        }

        GunSpawnManager.Instance.SendWeaponsSpawnersDataToPlayer(id);
        MatchManager.Singleton.SendMatchTimerToPlayer(id);

        if (MatchManager.playersOnLobby[id].onQueue && !currentScene.canSpawnOnJoin) return;
        playersLoadedScene++;
        if (currentScene.spawnable) Player.SpawnPlayer(id, MatchManager.playersOnLobby[id].playerName, Vector3.zero);
    }

    public void LoadScene(Scenes scene, string caller)
    {
        print($"<color=yellow>{new string('-', 30)}</color>");
        print($"<color=yellow> Caller {caller} asked to load scene {scene.sceneName}</color>");

        StartCoroutine(LoadSceneAsync(scene));
    }

    private IEnumerator LoadSceneAsync(Scenes scene)
    {
        print($"<color=yellow>Starting to load Scene {scene.sceneName} currently on Scene {SceneManager.GetActiveScene().name}</color>");
        playersLoadedScene = 0;

        loadingScreen.SetActive(true);
        AsyncOperation sceneLoadingOp = SceneManager.LoadSceneAsync(scene.sceneName);
        while (!sceneLoadingOp.isDone)
        {
            loadingPercentageTxt.SetText($"Loading {(sceneLoadingOp.progress * 100).ToString("#")}%");
            yield return null;
        }

        currentScene = scene;
        loadingScreen.SetActive(false);
        SpectateCameraManager.Singleton.mapCamera = FindObjectOfType<Camera>().gameObject;

        if (NetworkManager.Singleton.Server.IsRunning)
        {
            print("<color=blue>Reset PLS Counter</color>");
            playersLoadedScene++;
            if (currentScene.spawnable) Player.SpawnPlayer(NetworkManager.Singleton.Client.Id, MatchManager.playersOnLobby[NetworkManager.Singleton.Client.Id].playerName, Vector3.zero);
            SendSceneChanged(scene);
        }
        else SendClientSceneLoaded();

        print($"<color=yellow>Loaded Scene</color>");
        print($"<color=yellow>{new string('-', 30)}</color>");
    }
    #endregion

    #region ServerSenders
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
    #endregion

    #region ServerToClientHandlers
    [MessageHandler((ushort)ServerToClientId.sceneChanged)]
    private static void ReceiveSceneChanged(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        GameManager.Singleton.LoadScene(message.GetSerializable<Scenes>(), "ReceiveSceneChanged");
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
    #endregion
}
