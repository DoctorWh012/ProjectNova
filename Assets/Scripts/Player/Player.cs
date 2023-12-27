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
    public static event PlayerJoinedServer clientSpawned;

    [SerializeField] public PlayerHealth playerHealth;
    [SerializeField] public PlayerInteractions playerInteractions;
    [SerializeField] public PlayerHud playerHud;
    [SerializeField] public PlayerMovement playerMovement;
    [SerializeField] public PlayerShooting playerShooting;
    [SerializeField] public Rigidbody rb;

    private void OnDestroy()
    {
        list.Remove(Id);
    }

    public static void SpawnPlayer(ushort id, string username, Vector3 position)
    {
        Player player;
        Vector3 spawnPos = NetworkManager.Singleton.Server.IsRunning ? SpawnHandler.Instance.GetSpawnLocation() : position;
        if (id == NetworkManager.Singleton.Client.Id)
        {
            player = Instantiate(NetworkManager.Singleton.localPlayerPrefab, spawnPos, Quaternion.identity).GetComponent<Player>();
            player.IsLocal = true;
        }

        else
        {
            player = Instantiate(NetworkManager.Singleton.netPlayerPrefab, spawnPos, Quaternion.identity).GetComponent<Player>();
            player.IsLocal = false;

            player.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            player.rb.interpolation = RigidbodyInterpolation.None;
            player.rb.isKinematic = true;
        }

        player.name = $"Player {id} ({username})";
        player.Id = id;
        player.username = username;
        list.Add(id, player);

        if (NetworkManager.Singleton.Server.IsRunning)
        {
            foreach (Player otherPlayer in list.Values) otherPlayer.SendPlayersToPlayer(id);
            if (!player.IsLocal) clientSpawned(id);
        }
    }

    private void SendPlayersToPlayer(ushort toClientId)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.playerSpawned);
        message.AddUShort(Id);
        message.AddString(username);
        message.AddVector3(transform.position);
        NetworkManager.Singleton.Server.Send(message, toClientId);
    }

    [MessageHandler((ushort)ServerToClientId.playerSpawned)]
    private static void SpawnPlayer(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        SpawnPlayer(message.GetUShort(), message.GetString(), message.GetVector3());
    }
}
