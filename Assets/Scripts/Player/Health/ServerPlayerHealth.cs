using System.Collections;
using System.Collections.Generic;
using Riptide;
using UnityEngine;

public class ServerPlayerHealth : MonoBehaviour
{
    public bool isDead { get; private set; }

    [Header("Components")]
    [SerializeField] Player player;
    [SerializeField] PlayerScore playerScore;
    [SerializeField] PlayerMovement playerMovement;
    [SerializeField] GunShoot gunShoot;
    [SerializeField] Collider col;

    [Header("Settings")]
    [SerializeField] PlayerHealthSettings playerHealthSettings;

    private float _currentHealth;
    private float currentHealth
    {
        get { return _currentHealth; }
        set
        {
            if (value > playerHealthSettings.maxHealth)
            {
                _currentHealth = playerHealthSettings.maxHealth;
                SendUpdatedHealth(_currentHealth.ToString());
                return;
            }
            _currentHealth = value;
            SendUpdatedHealth(_currentHealth.ToString());
        }
    }

    // Start is called before the first frame update
    void Awake()
    {
        if (!GameManager.Singleton.networking || !NetworkManager.Singleton.Server.IsRunning) { this.enabled = false; return; }
        currentHealth = playerHealthSettings.maxHealth;
    }

    public bool ReceiveDamage(int damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            Die();
            return true;
        }
        return false;
    }

    private void Die()
    {
        isDead = true;
        playerScore.deaths++;
        playerMovement.FreezePlayerMovement(true);

        gunShoot.FreezePlayerShooting(true);
        col.enabled = false;

        Invoke("Respawn", playerHealthSettings.respawnTime);
        SendStatusMessage(isDead);
    }

    private void Respawn()
    {
        isDead = false;
        col.enabled = true;
        player.Movement.rb.position = SpawnHandler.Instance.GetSpawnLocation();

        playerMovement.FreezePlayerMovement(false);

        gunShoot.FreezePlayerShooting(false);

        gunShoot.ReplenishAllAmmo();

        currentHealth = playerHealthSettings.maxHealth;
        SendStatusMessage(isDead);
    }

    #region Messages
    private void SendStatusMessage(bool isDead)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.playerDied);
        message.AddUShort(player.Id);
        message.AddBool(isDead);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    private void SendUpdatedHealth(string health)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.healthChanged);
        if (NetworkManager.Singleton.Server == null) Debug.LogError("WTF");
        message.AddString(health);
        NetworkManager.Singleton.Server.Send(message, player.Id);
    }
    #endregion
}
