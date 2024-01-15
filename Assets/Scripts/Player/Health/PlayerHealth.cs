using System.Collections;
using UnityEngine;
using Riptide;

public enum PlayerState
{
    Alive,
    Stunned,
    Invincible,
    Dead
}

public class PlayerHealth : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Player player;
    [SerializeField] private GameObject playerCamera;
    [SerializeField] private AudioSource playerAudioSource;
    [SerializeField] private PlayerHud playerHud;
    [SerializeField] private PlayerShooting playerShooting;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private GameObject[] playerModels;
    [SerializeField] private Collider[] colliders;

    [Header("Particles/Effects")]
    [SerializeField] private ParticleSystem hurtEffect;
    [SerializeField] private ParticleSystem hurtParticles;
    [SerializeField] private ParticleSystem deathParticles;
    [SerializeField] private GameObject shieldsHolder;
    [SerializeField] private Animator shieldAnimator;

    [Header("Settings")]
    [SerializeField] ScriptablePlayer scriptablePlayer;

    [Header("Debugging Serialized")]
    [SerializeField] public PlayerState currentPlayerState = PlayerState.Alive;

    private uint lastReceivedDiedTick;
    private uint lastReceivedRespawnTick;
    private uint lastReceivedHealthTick;

    [SerializeField] private float _currentHealth;
    private float currentHealth
    {
        get { return _currentHealth; }
        set
        {
            // Taking Damage
            if (value < _currentHealth)
            {
                if (player.IsLocal) hurtEffect.Play();
                else hurtParticles.Play();

                playerAudioSource.pitch = Utilities.GetRandomPitch();
                playerAudioSource.PlayOneShot(scriptablePlayer.playerHurtAudio, scriptablePlayer.playerHurtAudioVolume);
            }

            // Healing
            if (value > scriptablePlayer.maxHealth)
            {
                _currentHealth = scriptablePlayer.maxHealth;

                if (player.IsLocal) playerHud.UpdateHealthDisplay(_currentHealth);
                if (NetworkManager.Singleton.Server.IsRunning) SendUpdatedHealth();
                return;
            }

            _currentHealth = value > 0 ? value : 0;

            if (player.IsLocal) playerHud.UpdateHealthDisplay(_currentHealth);
            if (NetworkManager.Singleton.Server.IsRunning) SendUpdatedHealth();
        }
    }

    private void Start()
    {
        currentHealth = scriptablePlayer.maxHealth;
    }

    private void Die()
    {
        if (currentPlayerState == PlayerState.Dead) return;
        currentPlayerState = PlayerState.Dead;

        shieldsHolder.SetActive(false);
        StopAllCoroutines();

        playerAudioSource.pitch = Utilities.GetRandomPitch();
        playerAudioSource.PlayOneShot(scriptablePlayer.playerDieAudio, scriptablePlayer.playerDieAudioVolume);

        if (player.IsLocal)
        {
            playerCamera.SetActive(false);
            SpectateCameraManager.Instance.EnableSpectateMode();
            playerMovement.FreezePlayerMovement();
        }
        else playerMovement.FreezeNetPlayerMovement();

        EnableDisablePlayerColliders(false);
        EnableDisableModels(false);

        deathParticles.Play();

        if (NetworkManager.Singleton.Server.IsRunning)
        {
            Invoke("StartRespawn", MatchManager.respawnTime);
            MatchManager.Singleton.AddDeathToPlayerScore(player.Id);
            SendPlayerDied();
        }
    }

    private void StartRespawn()
    {
        StartCoroutine(Respawn());
    }

    private void HandleServerPlayerDied(uint tick)
    {
        if (tick <= lastReceivedDiedTick) return;
        lastReceivedDiedTick = tick;

        Die();
    }

    private void HandleServerPlayerRespawned(Vector3 pos, uint tick)
    {
        if (tick <= lastReceivedRespawnTick) return;
        lastReceivedRespawnTick = tick;

        transform.position = pos;
        StartRespawn();
    }

    public bool ReceiveDamage(float damage)
    {
        if (currentPlayerState == PlayerState.Dead || currentPlayerState == PlayerState.Invincible) return false;
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

    private void HandleServerHealth(float health, uint tick)
    {
        if (tick <= lastReceivedHealthTick) return;
        lastReceivedHealthTick = tick;

        currentHealth = health;
    }

    private void EnableDisableModels(bool state)
    {
        if (player.IsLocal) playerShooting.EnableDisableHandsMeshes(state, state);

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
    private void SendPlayerDied()
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.playerDied);
        message.AddUShort(player.Id);
        message.AddUInt(NetworkManager.Singleton.serverTick);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    private void SendPlayerRespawned()
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.playerRespawned);
        message.AddUShort(player.Id);
        message.AddVector3(transform.position);
        message.AddUInt(NetworkManager.Singleton.serverTick);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    private void SendUpdatedHealth()
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.healthChanged);
        message.AddUShort(player.Id);
        message.AddSByte((sbyte)currentHealth);
        message.AddUInt(NetworkManager.Singleton.serverTick);
        NetworkManager.Singleton.Server.SendToAll(message);
    }
    #endregion

    #region ServerToClientHandlers
    [MessageHandler((ushort)ServerToClientId.playerDied)]
    private static void PlayerDied(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            player.playerHealth.HandleServerPlayerDied(message.GetUInt());
        }
    }

    [MessageHandler((ushort)ServerToClientId.playerRespawned)]
    private static void PlayerRespawned(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            player.playerHealth.HandleServerPlayerRespawned(message.GetVector3(), message.GetUInt());
        }
    }

    [MessageHandler((ushort)ServerToClientId.healthChanged)]
    private static void ChangeHealth(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            player.playerHealth.HandleServerHealth((float)message.GetSByte(), message.GetUInt());
        }
    }
    #endregion

    private IEnumerator Respawn()
    {
        if (currentPlayerState != PlayerState.Dead) yield break;
        currentPlayerState = PlayerState.Invincible;

        shieldsHolder.SetActive(true);
        shieldAnimator.Play("ShieldRaise");

        if (NetworkManager.Singleton.Server.IsRunning)
        {
            transform.position = SpawnHandler.Instance.GetSpawnLocation();
            currentHealth = scriptablePlayer.maxHealth;
            SendPlayerRespawned();
        }

        playerShooting.PickStartingWeapons();
        playerShooting.ReplenishAllAmmo();

        EnableDisablePlayerColliders(true);
        EnableDisableModels(true);

        if (player.IsLocal)
        {
            playerCamera.SetActive(true);
            SpectateCameraManager.Instance.DisableSpectateMode();
            playerMovement.FreePlayerMovement();
        }
        else playerMovement.FreeNetPlayerMovement();

        playerMovement.GetSpecials();

        yield return new WaitForSeconds(scriptablePlayer.invincibilityTime);
        shieldsHolder.SetActive(false);
        currentPlayerState = PlayerState.Alive;
    }
}
