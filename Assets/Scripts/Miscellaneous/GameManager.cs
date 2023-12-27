using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;
using Steamworks;
using Riptide;
using UnityEngine.Audio;

public static class Scenes
{
    public static string Menu = "NewMenu";
    public static string Lobby = "RiptideLobby";
    public static string LoadingScreen = "LoadingScreen";
    public static string MapFacility = "Facility";
}

public class GameManager : MonoBehaviour
{
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

    public static int clientsLoaded { get; private set; }

    [Header("Audio")]
    [SerializeField] private AudioMixerGroup masterMixer;

    [HideInInspector] public bool spawnPlayersAfterSceneLoad = false;
    public string currentScene;

    private void Awake()
    {
        Singleton = this;
    }

    private void Start()
    {
        SceneManager.sceneLoaded += SceneChangeEvents;
        SettingsManager.updatedPlayerPrefs += GetPreferences;
    }

    private void OnApplicationQuit()
    {
        SceneManager.sceneLoaded -= SceneChangeEvents;
        SettingsManager.updatedPlayerPrefs -= GetPreferences;
    }

    #region Preferences
    private void GetPreferences()
    {
        masterMixer.audioMixer.SetFloat("MasterVolume", Utilities.VolumeToDB(SettingsManager.playerPreferences.masterVolume));
        masterMixer.audioMixer.SetFloat("MusicVolume", Utilities.VolumeToDB(SettingsManager.playerPreferences.musicVolume));
    }
    #endregion

    #region Spawning
    private void SpawnAllPlayers()
    {
        foreach (KeyValuePair<ushort, string> players in NetworkManager.Singleton.playersOnLobby) Player.SpawnPlayer(players.Key, players.Value, Vector3.zero);
    }

    public void AttemptToSpawnPlayer(ushort id, string username)
    {
        Player.SpawnPlayer(id, username, Vector3.zero);
    }
    #endregion

    #region  SceneManaging
    public void LoadScene(string sceneName, string caller)
    {
        print($"<color=yellow> Caller {caller} asked to load scene {sceneName}</color>");

        StartCoroutine(LoadSceneAsync(sceneName));
        if (NetworkManager.Singleton.Server.IsRunning) SendSceneChanged(sceneName);
    }

    private void SceneChangeEvents(Scene scene, LoadSceneMode loadSceneMode)
    {
        if (scene.name == Scenes.LoadingScreen) return;

        if (spawnPlayersAfterSceneLoad)
        {
            SpawnAllPlayers();
            spawnPlayersAfterSceneLoad = false;
        }
    }
    #endregion

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        clientsLoaded = 0;
        SceneManager.LoadScene(Scenes.LoadingScreen);

        AsyncOperation sceneLoadingOp = SceneManager.LoadSceneAsync(sceneName);

        while (!sceneLoadingOp.isDone) yield return null;

        currentScene = sceneName;
        SendClientSceneLoaded();
        if (NetworkManager.Singleton.Server.IsRunning) SteamMatchmaking.SetLobbyData(NetworkManager.Singleton.lobbyId, "map", sceneName);
    }

    #region ServerSenders
    private void SendSceneChanged(string sceneName)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.sceneChanged);
        message.AddString(sceneName);
        NetworkManager.Singleton.Server.SendToAll(message);
    }
    #endregion

    #region ClientSenders
    private void SendClientSceneLoaded()
    {
        Message message = Message.Create(MessageSendMode.Reliable, ClientToServerId.playerLoadedScene);
        NetworkManager.Singleton.Client.Send(message);
    }
    #endregion

    #region ServerToClientHandlers
    [MessageHandler((ushort)ServerToClientId.sceneChanged)]
    private static void ReceiveSceneChanged(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        GameManager.Singleton.LoadScene(message.GetString(), "ReceiveSceneChanged");
    }
    #endregion

    #region ClientToServerHandlers
    [MessageHandler((ushort)ClientToServerId.playerLoadedScene)]
    private static void ReceiveClientLoadedScene(ushort fromClientId, Message message)
    {
        GameManager.clientsLoaded++;
    }
    #endregion
}
