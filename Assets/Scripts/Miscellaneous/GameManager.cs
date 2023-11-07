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
        print("<color=green> Spawning All Players </color>");
        foreach (KeyValuePair<ushort, string> players in NetworkManager.Singleton.playersOnLobby)
        {
            Player.SpawnPlayer(players.Key, players.Value, Vector3.zero);
        }
        print($"Spawned {NetworkManager.Singleton.playersOnLobby.Count} players");
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

        SceneManager.LoadScene(Scenes.LoadingScreen);

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
        AsyncOperation sceneLoadingOp = SceneManager.LoadSceneAsync(sceneName);
        sceneLoadingOp.allowSceneActivation = false;

        while (!sceneLoadingOp.isDone) yield return null;

        sceneLoadingOp.allowSceneActivation = true;
        currentScene = sceneName;
        SteamMatchmaking.SetLobbyData(NetworkManager.Singleton.lobbyId, "map", sceneName);
    }

    #region ServerSenders
    private void SendSceneChanged(string sceneName)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.SceneChanged);
        message.AddString(sceneName);
        NetworkManager.Singleton.Server.SendToAll(message);
    }
    #endregion

    #region ServerToClientHandlers
    [MessageHandler((ushort)ServerToClientId.SceneChanged)]
    private static void ReceiveSceneChanged(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        GameManager.Singleton.LoadScene(message.GetString(), "ReceiveSceneChanged");
    }
    #endregion
}
