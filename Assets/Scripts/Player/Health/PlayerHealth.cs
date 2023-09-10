using Riptide;
using UnityEngine;

public enum PlayerState
{
    Alive,
    Stunned,
    Dead
}

public class PlayerHealth : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Player player;
    [SerializeField] private ParticleSystem hurtEffect;
    [SerializeField] private PlayerHud playerHud;
    [SerializeField] private PlayerShooting playerShooting;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private GameObject[] playerModels;
    [SerializeField] private Collider[] colliders;

    [Header("Settings")]
    [SerializeField] ScriptablePlayer scriptablePlayer;

    [Header("Debugging Serialized")]
    [SerializeField] public PlayerState currentPlayerState = PlayerState.Alive;

    private ushort lastReceivedStatusTick;
    private ushort lastReceivedHealthTick;

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
                if (player.IsLocal) playerHud.UpdateHealthDisplay(_currentHealth);
                else SendUpdatedHealth();
                return;
            }
            _currentHealth = value;
            if (player.IsLocal) playerHud.UpdateHealthDisplay(_currentHealth);
            else SendUpdatedHealth();
        }
    }

    private void Start()
    {
        if (NetworkManager.Singleton.Server.IsRunning) currentHealth = scriptablePlayer.maxHealth;
    }

    private void Die()
    {
        if (currentPlayerState == PlayerState.Dead) return;
        currentPlayerState = PlayerState.Dead;

        playerMovement.FreezePlayerMovement();

        EnableDisablePlayerColliders(false);
        EnableDisableModels(false);

        if (NetworkManager.Singleton.Server.IsRunning)
        {
            SendStatusMessage();
            Invoke("Respawn", scriptablePlayer.respawnTime);
        }
    }

    private void Respawn()
    {
        if (currentPlayerState != PlayerState.Dead) return;
        currentPlayerState = PlayerState.Alive;

        playerMovement.rb.position = SpawnHandler.Instance.GetSpawnLocation();
        playerShooting.PickStartingWeapons();
        playerShooting.ReplenishAllAmmo();
        currentHealth = scriptablePlayer.maxHealth;

        EnableDisablePlayerColliders(true);
        EnableDisableModels(true);

        playerMovement.FreePlayerMovement();

        if (NetworkManager.Singleton.Server.IsRunning) SendStatusMessage();
    }

    private void HandleServerPlayerStatus(bool status, Vector3 position, ushort tick)
    {
        if (tick <= lastReceivedStatusTick) return;
        lastReceivedStatusTick = tick;

        if (status)
        {
            Respawn();
            playerMovement.transform.position = position;
        }

        else Die();
    }

    public bool ReceiveDamage(float damage)
    {
        if (currentPlayerState == PlayerState.Dead) return false;
        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            Die();
            return true;
        }
        return false;
    }

    public void InstaKill()
    {
        if (currentPlayerState == PlayerState.Dead) return;

        currentHealth -= currentHealth;
        Die();
    }

    private void RecoverHealth(float healAmount)
    {
        if (currentPlayerState == PlayerState.Dead) return;

        currentHealth += healAmount;
    }

    private void HandleServerHealth(float health, ushort tick)
    {
        if (tick <= lastReceivedHealthTick) return;
        lastReceivedHealthTick = tick;

        currentHealth = health;
    }

    private void EnableDisableModels(bool state)
    {
        if (player.IsLocal) playerShooting.EnableDisableHandsMeshes(state);

        for (int i = 0; i < playerModels.Length; i++)
        {
            playerModels[i].SetActive(state);
        }

        if (state)
        {
            playerShooting.EnableActiveWeapon(playerShooting.activeGun.weaponType);
        }

        else
        {
            playerShooting.DisableAllGuns();
            playerShooting.DisableAllMelees();
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

    #region  ServerSenders
    private void SendStatusMessage()
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.playerDied);
        message.AddUShort(player.Id);
        message.AddBool(currentPlayerState == PlayerState.Alive);
        message.AddVector3(playerMovement.rb.position);
        message.AddUShort(NetworkManager.Singleton.serverTick);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    private void SendUpdatedHealth()
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.healthChanged);
        message.AddUShort(player.Id);
        message.AddSByte((sbyte)currentHealth);
        message.AddUShort(NetworkManager.Singleton.serverTick);
        NetworkManager.Singleton.Server.Send(message, player.Id);
    }
    #endregion

    #region ServerToClientHandlers
    [MessageHandler((ushort)ServerToClientId.playerDied)]
    private static void PlayerDied(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            player.playerHealth.HandleServerPlayerStatus(message.GetBool(), message.GetVector3(), message.GetUShort());
        }
    }

    [MessageHandler((ushort)ServerToClientId.healthChanged)]
    private static void ChangeHealth(Message message)
    {
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            player.playerHealth.HandleServerHealth((float)message.GetSByte(), message.GetUShort());
        }
    }
    #endregion
}
