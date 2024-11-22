using System.Collections.Generic;
using Riptide;
using UnityEngine;

public class Player : MonoBehaviour
{
    public static Dictionary<ushort, Player> list = new Dictionary<ushort, Player>();

    public ushort Id { get; private set; }
    public bool IsLocal { get; private set; }
    public string username { get; private set; }

    [Header("Components")]
    [Space(5)]
    [SerializeField] public PlayerHealth playerHealth;
    [SerializeField] public PlayerInteractions playerInteractions;
    [SerializeField] public PlayerHud localPlayerHud;
    [SerializeField] public PlayerMovement playerMovement;
    [SerializeField] public PlayerShooting playerShooting;
    [SerializeField] public Rigidbody rb;
    [SerializeField] public GameObject netPlayerSpectatorCamBrain;
    [SerializeField] public Transform localPlayerCamera;

    [Header("Team")]
    [Space(5)]
    [SerializeField] private Outliner netOutliner;
    [SerializeField] public Color allyTeamColor;
    [SerializeField] public Color enemyTeamColor;

    private void OnDestroy()
    {
        list.Remove(Id);
        SpectateCameraManager.availableCameras.Remove(netPlayerSpectatorCamBrain);
    }

    private void Start()
    {
        if (IsLocal) return;

        if (GameManager.currentGamemode == GameMode.FreeForAll) OutlinePlayer(false);
        else OutlinePlayer(GameManager.allyTeam.ContainsKey(Id));
    }

    private void OutlinePlayer(bool ally)
    {
        netOutliner.OutlineMode = ally ? Outliner.Mode.OutlineAll : Outliner.Mode.OutlineVisible;
        netOutliner.OutlineColor = ally ? allyTeamColor : enemyTeamColor;
    }

    public static void SpawnPlayer(ushort id, string username, Vector3 position)
    {
        if (Player.list.ContainsKey(id)) { print($"Returning because {username} has already been spawned"); return; }
        print($"Spawning {username}");

        Player player;
        Vector3 spawnPos = NetworkManager.Singleton.Server.IsRunning ? SpawnHandler.Instance.GetSpawnLocation() : position;
        if (id == NetworkManager.Singleton.Client.Id)
        {
            player = Instantiate(NetworkManager.Singleton.localPlayerPrefab, spawnPos, Quaternion.identity).GetComponent<Player>();
            player.IsLocal = true;
            SpectateCameraManager.Singleton.playerCamera = player.localPlayerCamera;
        }

        else
        {
            player = Instantiate(NetworkManager.Singleton.netPlayerPrefab, spawnPos, Quaternion.identity).GetComponent<Player>();
            player.IsLocal = false;

            player.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            player.rb.interpolation = RigidbodyInterpolation.None;
            player.rb.isKinematic = true;
            SpectateCameraManager.availableCameras.Add(player.netPlayerSpectatorCamBrain);
        }

        player.name = $"Player {id} ({username})";
        player.Id = id;
        player.username = username;
        list.Add(id, player);

        if (list.Count == 1 && id != NetworkManager.Singleton.Client.Id) SpectateCameraManager.Singleton.EnableSpectateMode();

        if (id == NetworkManager.Singleton.Client.Id) SpectateCameraManager.Singleton.DisableSpectateMode();
        if (NetworkManager.Singleton.Server.IsRunning) player.SendPlayerToPlayers();
        print("Player was Spawned");
    }

    #region ServerSenders
    public void SendPlayersToPlayer(ushort toClientId)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.playerSpawned);
        message.AddUShort(Id);
        message.AddString(username);
        message.AddVector3(transform.position);
        NetworkManager.Singleton.Server.Send(message, toClientId);
    }

    private void SendPlayerToPlayers()
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.playerSpawned);
        message.AddUShort(Id);
        message.AddString(username);
        message.AddVector3(transform.position);
        NetworkManager.Singleton.Server.SendToAll(message);
    }
    #endregion

    #region ServerToClientHandlers
    [MessageHandler((ushort)ServerToClientId.playerSpawned)]
    private static void SpawnPlayer(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        SpawnPlayer(message.GetUShort(), message.GetString(), message.GetVector3());
    }
    #endregion
}
