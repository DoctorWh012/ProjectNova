using System;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine;
using Riptide;
using UnityEngine.Audio;

public class Scenes : IMessageSerializable
{
    public string sceneName;
    public bool canSpawnOnLoad;
    public bool canSpawnOnJoin;

    public Scenes(string name, bool spawnOnLoad, bool spawnOnJoin)
    {
        sceneName = name;
        canSpawnOnLoad = spawnOnLoad;
        canSpawnOnJoin = spawnOnJoin;
    }

    public Scenes() { }

    public void Deserialize(Message message)
    {
        sceneName = message.GetString();
        canSpawnOnLoad = message.GetBool();
        canSpawnOnJoin = message.GetBool();
    }

    public void Serialize(Message message)
    {
        message.AddString(sceneName);
        message.AddBool(canSpawnOnLoad);
        message.AddBool(canSpawnOnJoin);
    }
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

    public static Scenes menuScene = new Scenes("NewMenu", false, false);
    public static Scenes loadingScreenScene = new Scenes("LoadingScreen", false, false);
    public static Scenes lobbyScene = new Scenes("RiptideLobby", true, true);
    public static Scenes facilityScene = new Scenes("Facility", true, false);

    public static Scenes currentScene;
    public static int playersLoadedScene;

    [Header("Audio")]
    [SerializeField] private AudioMixerGroup masterMixer;

    private void Awake()
    {
        Singleton = this;
    }

    private void Start()
    {
        SettingsManager.updatedPlayerPrefs += GetPreferences;
    }

    private void OnApplicationQuit()
    {
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

    #endregion

    #region SceneManaging
    private void HandlePlayerLoadedScene(ushort id)
    {
        // This Is Here And Not In The Scene Loading Coroutine Because Unity's Scene Loading System Is A Cunt
        if (id == NetworkManager.Singleton.Client.Id)
        {
            playersLoadedScene++;
            Player.SpawnPlayer(id, MatchManager.playersOnLobby[id].playerName, Vector3.zero);
            return;
        }

        // Sends Spawned Players To The Player Who Loaded The Scene
        foreach (Player otherPlayer in Player.list.Values)
        {
            otherPlayer.SendPlayersToPlayer(id);
            otherPlayer.playerShooting.SendWeaponSync(id); // BROKEN
        }

        GunSpawnManager.Instance.SendWeaponsSpawnersDataToPlayer(id);
        MatchManager.Singleton.SendMatchTimerToPlayer(id);

        if (MatchManager.playersOnLobby[id].onQueue) return;
        playersLoadedScene++;
        Player.SpawnPlayer(id, MatchManager.playersOnLobby[id].playerName, Vector3.zero);
    }

    public void LoadScene(Scenes scene, string caller)
    {
        print($"<color=yellow>{new string('-', 30)}</color>");
        print($"<color=yellow> Caller {caller} asked to load scene {scene.sceneName}</color>");

        StartCoroutine(LoadSceneAsync(scene));
    }

    private IEnumerator LoadSceneAsync(Scenes scene)
    {
        playersLoadedScene = 0;
        if (NetworkManager.Singleton.Server.IsRunning) SendSceneChanged(scene);

        print($"<color=yellow>Starting to load Scene {scene.sceneName} currently on Scene {SceneManager.GetActiveScene().name}</color>");
        SceneManager.LoadScene(loadingScreenScene.sceneName);
        AsyncOperation sceneLoadingOp = SceneManager.LoadSceneAsync(scene.sceneName);
        while (!sceneLoadingOp.isDone) yield return null;

        currentScene = scene;
        SendClientSceneLoaded();

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
    #endregion
}
