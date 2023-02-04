using System.Collections;
using System.Collections.Generic;
using Riptide;
using UnityEngine;

public class Player : MonoBehaviour
{
    public static Dictionary<ushort, Player> list = new Dictionary<ushort, Player>();

    public ushort Id { get; private set; }
    public bool IsLocal { get; private set; }
    public string username { get; private set; }
    public bool isAlive { get; private set; } = true;
    public PlayerMovement Movement => movement;
    public GunShoot GunShoot => gunShoot;

    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Transform orientation;
    [SerializeField] private Transform cam;
    [SerializeField] public Interpolation interpolation;
    [SerializeField] public MultiplayerGunShoot multiplayerGunShoot;
    [SerializeField] public MultiplayerController multiplayerController;
    [SerializeField] public PlayerEffects playerEffects;
    [SerializeField] public PlayerShooting playerShooting;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private GunShoot gunShoot;
    [SerializeField] private Rigidbody rb;


    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
    // --------CLIENT--------
    private void OnDestroy()
    {
        ScoreBoard.Instance.RemoveScoreBoardItem(list[Id]);
        list.Remove(Id);
    }

    public static void Spawn(ushort id, string username, Vector3 position)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        Player player;
        if (id == NetworkManager.Singleton.Client.Id)
        {
            player = Instantiate(GameLogic.Singleton.LocalPlayerPrefab, position, Quaternion.identity).GetComponent<Player>();
            player.interpolation.enabled = false;
            player.IsLocal = true;
        }
        else
        {
            player = Instantiate(GameLogic.Singleton.PlayerPrefab, position, Quaternion.identity).GetComponent<Player>();
            player.interpolation.enabled = true;
            player.IsLocal = false;
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
        foreach (Player otherPlayer in list.Values)
        {
            otherPlayer.SendSpawned(id);
        }
        //Spawns LocalPlayer if im the Host
        Player player;
        if (id == NetworkManager.Singleton.Client.Id)
        {
            player = Instantiate(GameLogic.Singleton.LocalPlayerPrefab, SpawnHandler.Instance.GetSpawnLocation(), Quaternion.identity).GetComponent<Player>();
            player.IsLocal = true;
        }
        //Spawns NetPlayer if im the Host
        else
        {
            player = Instantiate(GameLogic.Singleton.PlayerPrefab, SpawnHandler.Instance.GetSpawnLocation(), Quaternion.identity).GetComponent<Player>();
            player.IsLocal = false;
        }
        player.name = $"Player {id} ({(string.IsNullOrEmpty(username) ? "Guest" : username)}";
        player.Id = id;
        player.username = string.IsNullOrEmpty(username) ? $"Guest {id}" : username;
        list.Add(id, player);
        player.rb.isKinematic = false;
        ScoreBoard.Instance.AddScoreBoarditem(list[id]);
        player.SendSpawned();
    }

    //Sends LocalPlayer?
    private void SendSpawned()
    {
        NetworkManager.Singleton.Server.SendToAll(AddSpawnData(Message.Create(MessageSendMode.Reliable, ServerToClientId.playerSpawned)));
    }

    //Sends NetPlayer?
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
    #region  Messages
    [MessageHandler((ushort)ServerToClientId.playerSpawned)]
    private static void SpawnPlayer(Message message)
    {
        Spawn(message.GetUShort(), message.GetString(), message.GetVector3());
    }

    [MessageHandler((ushort)ServerToClientId.playerDied)]
    private static void PlayerDied(Message message)
    {
        if (list.TryGetValue(message.GetUShort(), out Player player))
        {
            if (message.GetBool()) player.playerHealth.Die();
            else player.playerHealth.Respawn();
        }
    }

    #endregion

    // Server Message Handler
    #region  Messages
    [MessageHandler((ushort)ClientToServerId.name)]
    private static void Name(ushort fromClientId, Message message)
    {
        Spawn(fromClientId, message.GetString());
    }
    #endregion
}
