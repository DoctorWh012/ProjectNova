using System.Collections.Generic;
using Riptide;
using UnityEngine;

public class Player : MonoBehaviour
{
    public static Dictionary<ushort, Player> list = new Dictionary<ushort, Player>();

    public ushort Id { get; private set; }
    public bool IsLocal { get; private set; }
    public string username { get; private set; }

    public delegate void PlayerJoinedServer(ushort id);
    public static event PlayerJoinedServer playerJoinedServer;

    [SerializeField] public PlayerHealth playerHealth;
    [SerializeField] public PlayerEffects playerEffects;
    [SerializeField] public PlayerMovement playerMovement;
    [SerializeField] public PlayerShooting playerShooting;
    [SerializeField] public Rigidbody rb;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    // --------CLIENT--------
    private void OnDestroy()
    {
        if (ScoreBoard.Instance != null) ScoreBoard.Instance.RemoveScoreBoardItem(list[Id]);
        list.Remove(Id);
    }

    public static void Spawn(ushort id, string username, Vector3 position)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;

        Player player;
        if (id == NetworkManager.Singleton.Client.Id)
        {
            player = Instantiate(NetworkManager.Singleton.localPlayerPrefab, position, Quaternion.identity).GetComponent<Player>();
            player.IsLocal = true;
        }
        else
        {
            player = Instantiate(NetworkManager.Singleton.netPlayerPrefab, position, Quaternion.identity).GetComponent<Player>();
            player.IsLocal = false;

            player.rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            player.rb.interpolation = RigidbodyInterpolation.None;
            player.rb.isKinematic = true;
        }
        player.name = $"Player {id} {username}";
        player.Id = id;
        player.username = username;
        list.Add(id, player);

        ScoreBoard.Instance.AddScoreBoarditem(list[id]);
    }

    //--------SERVER Only Runs On The Host!--------
    public static void Spawn(ushort id, string username)
    {
        foreach (Player otherPlayer in list.Values) otherPlayer.SendSpawned(id);

        //Spawns LocalPlayer if im the Host
        Player player;
        if (id == NetworkManager.Singleton.Client.Id)
        {
            player = Instantiate(NetworkManager.Singleton.localPlayerPrefab, SpawnHandler.Instance.GetSpawnLocation(), Quaternion.identity).GetComponent<Player>();
            player.IsLocal = true;
        }

        //Spawns NetPlayer if im the Host
        else
        {
            player = Instantiate(NetworkManager.Singleton.netPlayerPrefab, SpawnHandler.Instance.GetSpawnLocation(), Quaternion.identity).GetComponent<Player>();
            player.IsLocal = false;

            player.rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            player.rb.interpolation = RigidbodyInterpolation.None;
            player.rb.isKinematic = true;
            playerJoinedServer(id);
        }

        player.name = $"Player {id} ({(string.IsNullOrEmpty(username) ? "Guest" : username)}";
        player.Id = id;
        player.username = string.IsNullOrEmpty(username) ? $"Guest {id}" : username;
        list.Add(id, player);
        ScoreBoard.Instance.AddScoreBoarditem(list[id]);
        player.SendSpawned();
    }

    //Sends LocalPlayer
    private void SendSpawned()
    {
        NetworkManager.Singleton.Server.SendToAll(AddSpawnData(Message.Create(MessageSendMode.Reliable, ServerToClientId.playerSpawned)));
    }

    //Sends NetPlayer
    private void SendSpawned(ushort toClientId)
    {
        NetworkManager.Singleton.Server.Send(AddSpawnData(Message.Create(MessageSendMode.Reliable, ServerToClientId.playerSpawned)), toClientId);
    }

    private Message AddSpawnData(Message message)
    {
        message.AddUShort(Id);
        message.AddString(username);
        message.AddVector3(transform.position);
        return message;
    }

    // Client Message Handler
    [MessageHandler((ushort)ServerToClientId.playerSpawned)]
    private static void SpawnPlayer(Message message)
    {
        Spawn(message.GetUShort(), message.GetString(), message.GetVector3());
    }

    // Server Message Handler
    #region  Messages
    [MessageHandler((ushort)ClientToServerId.name)]
    private static void Name(ushort fromClientId, Message message)
    {
        Spawn(fromClientId, message.GetString());
    }
    #endregion
}
