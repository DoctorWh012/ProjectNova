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

    //Client Script
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private Transform orientation;
    [SerializeField] private Transform cam;
    [SerializeField] private Interpolation interpolation;
    [SerializeField] public MultiplayerGunShoot multiplayerGunShoot;
    [SerializeField] public MultiplayerController multiplayerController;
    [SerializeField] public PlayerShooting playerShooting;

    //Server Script
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private GunShoot gunShoot;
    [SerializeField] private Rigidbody rb;

    private int[] inputs;

    // --------CLIENT--------
    private void OnDestroy()
    {
        ScoreBoard.Instance.RemoveScoreBoardItem(list[Id]);
        list.Remove(Id);
    }

    private void Move(ushort tick, Vector3 newPosition, Vector3 forward, Quaternion camRot)
    {
        interpolation.NewUpdate(tick, newPosition);

        if (!IsLocal && !NetworkManager.Singleton.Server.IsRunning)
        {
            orientation.forward = forward;
            cam.rotation = camRot;
        }
    }

    public static void Spawn(ushort id, string username, Vector3 position)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        Player player;
        if (id == NetworkManager.Singleton.Client.Id)
        {
            player = Instantiate(GameLogic.Singleton.LocalPlayerPrefab, position, Quaternion.identity).GetComponent<Player>();
            player.interpolation.enabled = true;
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

    private void PlayerAnimator(bool isSliding)
    {
        if (IsLocal) return;
        if (isSliding) { playerAnimator.Play("Slide"); return; }
        switch (inputs[0])
        {
            case 1:
                playerAnimator.Play("Run");
                return;
            case -1:
                playerAnimator.Play("RunBackwards");
                return;
        }
        switch (inputs[1])
        {
            case 1:
                playerAnimator.Play("RunRight");
                return;
            case -1:
                playerAnimator.Play("RunLeft");
                return;
        }
        playerAnimator.Play("Idle");
    }

    private void PlayAnimation(string animation)
    {
        if (!playerAnimator.GetCurrentAnimatorStateInfo(0).IsName(animation))
        {
            playerAnimator.Play(animation);
        }
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

    [MessageHandler((ushort)ServerToClientId.playerMovement)]
    private static void PlayerMovement(Message message)
    {
        if (list.TryGetValue(message.GetUShort(), out Player player))
        {
            player.Move(message.GetUShort(), message.GetVector3(), message.GetVector3(), message.GetQuaternion());
            player.inputs = message.GetInts();
            player.PlayerAnimator(message.GetBool());
        }
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
