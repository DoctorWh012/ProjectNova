using Riptide;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public bool isDead { get; private set; }

    [Header("Components")]
    [SerializeField] private Player player;
    [SerializeField] private ParticleSystem hurtEffect;
    [SerializeField] private MultiplayerGunShoot multiplayerGunShoot;
    [SerializeField] private GunShoot gunShoot;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerScore playerScore;
    [SerializeField] private HeadBobController headBob;
    [SerializeField] private GameObject[] playerModels;
    [SerializeField] private Collider[] colliders;

    [Header("Settings")]
    [SerializeField] ScriptablePlayer scriptablePlayer;

    private float _currentHealth;
    private float currentHealth
    {
        get { return _currentHealth; }
        set
        {
            if (value < currentHealth && player.IsLocal) hurtEffect.Play();

            if (value > scriptablePlayer.maxHealth)
            {
                _currentHealth = scriptablePlayer.maxHealth;
                SendUpdatedHealth((sbyte)_currentHealth);
                return;
            }
            _currentHealth = value;
            SendUpdatedHealth((sbyte)_currentHealth);

            GameCanvas.Instance.UpdateHealthAmmount(currentHealth.ToString("0"));
        }
    }

    private void Awake()
    {
        if (NetworkManager.Singleton.Server.IsRunning) currentHealth = scriptablePlayer.maxHealth;
    }


    // True = Die | False = Respawn
    private void DieRespawnServer(bool state)
    {
        isDead = state;
        playerMovement.FreezePlayerMovement(state);
        EnableDisablePlayerColliders(!state);
        DisableEnableModels(!state);
        if (state)
        {
            playerScore.deaths++;
            Invoke("ServerRespawn", scriptablePlayer.respawnTime);
        }
        else
        {
            playerMovement.rb.position = SpawnHandler.Instance.GetSpawnLocation();
            gunShoot.ReplenishAllAmmo();
            currentHealth = scriptablePlayer.maxHealth;
        }

        SendStatusMessage(isDead);
    }

    private void ServerRespawn()
    {
        DieRespawnServer(false);
    }

    // True = Die | False = Respawn
    private void DieRespawnClient(bool state)
    {
        DisableEnableModels(!state);
        if (!player.IsLocal) return;
        playerMovement.FreezePlayerMovement(state);
        multiplayerGunShoot.enabled = !state;
        headBob.enabled = !state;
    }

    private void DisableEnableModels(bool state)
    {
        for (int i = 0; i < playerModels.Length; i++)
        {
            playerModels[i].SetActive(state);
        }

        if (!state)
        {
            gunShoot.DisableAllGunMeshes();
            gunShoot.DisableAllMeleeMesh();
            return;
        }
    }

    private void EnableDisablePlayerColliders(bool state)
    {
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = state;
        }
    }

    public bool ReceiveDamage(float damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            DieRespawnServer(true);
            return true;
        }
        return false;
    }

    private void SendStatusMessage(bool isDead)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.playerDied);
        message.AddUShort(player.Id);
        message.AddBool(isDead);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    private void SendUpdatedHealth(sbyte health)
    {
        if (player.IsLocal || !NetworkManager.Singleton.Server.IsRunning) return;
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.healthChanged);
        message.AddUShort(player.Id);
        message.AddSByte(health);
        NetworkManager.Singleton.Server.Send(message, player.Id);
    }

    [MessageHandler((ushort)ServerToClientId.playerDied)]
    private static void PlayerDied(Message message)
    {
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            player.playerHealth.DieRespawnClient(message.GetBool());
        }
    }

    [MessageHandler((ushort)ServerToClientId.healthChanged)]
    private static void ChangeHealth(Message message)
    {
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            player.playerHealth.currentHealth = (float)message.GetSByte();
        }
    }
}
