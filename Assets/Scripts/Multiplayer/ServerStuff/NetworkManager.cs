using Riptide;
using Riptide.Utils;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Riptide.Transports.Steam;
using System.Collections.Generic;

public enum ServerToClientId : ushort
{
    sync = 1,
    playerSpawned,
    playerMovement,
    playerShot,
    playerHit,
    playerDied,
    playerScore,
    gunChanged,
    gunReload,
    ammoChanged,
    healthChanged,
    gunSpawned,
    gunDespawned,
    meleeAtack,
    matchStart,
    matchOver,
    wallRun,
}

public enum ClientToServerId : ushort
{
    name = 1,
    input,
    gunInput,
    gunChange,
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
    public ushort CurrentTick { get; private set; } = 0;

    private ushort _serverTick;
    public ushort ServerTick
    {
        get => _serverTick;
        private set
        {
            _serverTick = value;
            InterpolationTick = (ushort)(value - TickBetweenPositionUpdates);
        }
    }
    public ushort InterpolationTick { get; private set; }
    private ushort _tickBetweenPositionUpdates = 2;
    public ushort TickBetweenPositionUpdates
    {
        get => _tickBetweenPositionUpdates;
        private set
        {
            _tickBetweenPositionUpdates = value;
            InterpolationTick = (ushort)(ServerTick - value);
        }
    }

    [SerializeField] public ushort maxClientCount;
    [Space(10)]
    [SerializeField] private ushort tickDivergenceTolerance = 1;


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

        ServerTick = 2;
    }

    private void FixedUpdate()
    {
        if (Server.IsRunning)
        {
            Server.Update();
            if (CurrentTick % 200 == 0) SendSync();
            CurrentTick++;
        }

        Client.Update();
        ServerTick++;
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
    }

    private void DestroyAllPlayers()
    {
        foreach (KeyValuePair<ushort, Player> player in Player.list) Destroy(player.Value.gameObject);
    }

    private void DidConnect(object sender, EventArgs e)
    {
        StartCoroutine(MainMenu.Instance.SendName());
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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
        SceneManager.LoadScene("Menu");
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void SetTick(ushort serverTick)
    {
        if (Mathf.Abs(ServerTick - serverTick) > tickDivergenceTolerance)
        {
            print($"Client Tick: {ServerTick} -> {serverTick}");
            ServerTick = serverTick;
        }
    }

    private void SendSync()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, (ushort)ServerToClientId.sync);
        message.Add(CurrentTick);

        Server.SendToAll(message);
    }

    private void SceneChangeDebug(Scene loaded, LoadSceneMode i)
    {
        Debug.LogWarning($"Switched scene To {loaded.name}");
    }

    [MessageHandler((ushort)ServerToClientId.sync)]
    public static void Sync(Message message)
    {
        Singleton.SetTick(message.GetUShort());
    }
}
