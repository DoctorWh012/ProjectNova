using Riptide;
using Riptide.Utils;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Riptide.Transports.Steam;
using System.Collections.Generic;

public enum ServerToClientId : ushort
{
    serverTick = 1,
    playerMovement,
    playerSpawned,
    playerShot,
    playerHit,
    playerDied,
    playerScore,
    gunChanged,
    healthChanged,
    pickedGun,
    gunSpawned,
    gunDespawned,
    meleeAtack,
    matchStart,
    matchOver,
    weaponSync,
}

public enum ClientToServerId : ushort
{
    name = 1,
    playerMovement,
    gunInput,
    slotChange,
    gunReload,
}

public class NetworkManager : MonoBehaviour
{
    private static NetworkManager _singleton;

    public static NetworkManager Singleton
    {
        get => _singleton;
        private set
        {
            if (_singleton == null)
            {
                _singleton = value;
            }

            else if (_singleton != value)
            {
                Debug.Log($"{nameof(NetworkManager)} instance already exists, destroying duplicate");
                Destroy(value.gameObject);
            }

        }
    }

    public Client Client { get; private set; }

    public Server Server { get; private set; }

    public const float ServerTickRate = 64f;
    [SerializeField] public ushort maxClientCount;

    private float timer;

    private void Awake()
    {
        Singleton = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (!SteamManager.Initialized) { Debug.LogError("Steam is not initialized!"); Application.Quit(); return; }

        SceneManager.sceneLoaded += SceneChangeDebug;
        SteamServer steamServer = new SteamServer();
        Server = new Server(steamServer);
        Server.ClientDisconnected += PlayerLeft;

        RiptideLogger.Initialize(Debug.Log, Debug.Log, Debug.LogWarning, Debug.LogError, false);
        Client = new Client(new SteamClient(steamServer));
        Client.Connected += DidConnect;
        Client.ConnectionFailed += FailedToConnect;
        Client.ClientDisconnected += PlayerLeft;
        Client.Disconnected += DidDisconnect;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        while (timer >= GameManager.Singleton.minTimeBetweenTicks)
        {
            timer -= GameManager.Singleton.minTimeBetweenTicks;
            if (Server.IsRunning) Server.Update();
            Client.Update();
        }
    }

    private void OnApplicationQuit()
    {
        if (Server.IsRunning) Server.Stop();
        Server.ClientDisconnected -= PlayerLeft;

        Client.Disconnect();
        Client.Connected -= DidConnect;
        Client.ConnectionFailed -= FailedToConnect;
        Client.ClientDisconnected -= PlayerLeft;
        Client.Disconnected -= DidDisconnect;

    }

    internal void StopServer()
    {
        Server.Stop();
        DestroyAllPlayers();
    }

    internal void DisconnectClient()
    {
        Client.Disconnect();
        DestroyAllPlayers();
        GameObject[] toBeDestroyed = GameObject.FindGameObjectsWithTag("Destroy");
        foreach (GameObject go in toBeDestroyed) Destroy(go);
    }

    private void DestroyAllPlayers()
    {
        foreach (KeyValuePair<ushort, Player> player in Player.list) Destroy(player.Value.gameObject);
    }

    private void DidConnect(object sender, EventArgs e)
    {
        GameManager.Singleton.networking = true;
        StartCoroutine(LobbyManager.Singleton.SendName());
    }

    private void FailedToConnect(object sender, EventArgs e)
    {
        SceneManager.LoadScene("Menu");
    }

    private void PlayerLeft(object sender, ClientDisconnectedEventArgs e)
    {
        Destroy(Player.list[e.Id].gameObject);
    }
    private void PlayerLeft(object sender, ServerDisconnectedEventArgs e)
    {
        Destroy(Player.list[e.Client.Id].gameObject);
    }

    private void DidDisconnect(object sender, EventArgs e)
    {
        LobbyManager.Singleton.LeaveLobby();
        SceneManager.LoadScene("Menu");
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void SceneChangeDebug(Scene loaded, LoadSceneMode i)
    {
        Debug.LogWarning($"Switched scene To {loaded.name}");
    }
}
