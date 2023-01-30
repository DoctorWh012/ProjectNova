using System.Collections;
using System.Collections.Generic;
using Riptide;
using UnityEngine;

public class ServerPlayerHealth : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] Player player;
    [SerializeField] PlayerScore playerScore;
    [SerializeField] PlayerMovement playerMovement;
    [SerializeField] GunShoot gunShoot;
    [SerializeField] Collider col;

    [Header("Settings")]
    [SerializeField] private float maxHealth;
    [SerializeField] private float respawnTime;

    private float _currentHealth;
    private float currentHealth
    {
        get { return _currentHealth; }
        set
        {
            if (value > maxHealth)
            {
                _currentHealth = maxHealth;
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
        if (!NetworkManager.Singleton.Server.IsRunning) { this.enabled = false; return; }
        currentHealth = maxHealth;
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

        playerScore.deaths++;
        playerMovement.FreezePlayerMovement(true);
        playerMovement.enabled = false;

        gunShoot.FreezePlayerShooting(true);
        col.enabled = false;
        Invoke("Respawn", respawnTime);
        SendStatusMessage(true);
    }

    private void Respawn()
    {
        transform.position = SpawnHandler.Instance.GetSpawnLocation();
        playerMovement.enabled = true;
        playerMovement.FreezePlayerMovement(false);
        gunShoot.FreezePlayerShooting(false);
        gunShoot.ReplenishAllAmmo();
        col.enabled = true;
        currentHealth = maxHealth;
        SendStatusMessage(false);
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
