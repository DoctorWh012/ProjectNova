using System.Collections;
using UnityEngine;
using Riptide;
using DG.Tweening;

public enum PlayerState
{
    Alive,
    Invincible,
    Dead
}

public class PlayerHealth : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Player player;
    [SerializeField] private GameObject playerCharacter;
    [SerializeField] private GameObject localPlayerCameraHolder;
    [SerializeField] private AudioSource playerAudioSource;
    [SerializeField] private PlayerHud localPlayerHud;
    [SerializeField] private PlayerShooting playerShooting;
    [SerializeField] private PlayerMovement playerMovement;

    [Header("Particles/Effects")]
    [SerializeField] private DamageIndicator netPlayerDamageIndicator;
    [SerializeField] private Sprite suicideIcon;
    [SerializeField] private ParticleSystem hurtParticles;
    [SerializeField] private ParticleSystem deathParticles;
    [SerializeField] private GameObject shieldsHolder;
    [SerializeField] private Animator shieldAnimator;

    [Header("Settings")]
    [SerializeField] private ScriptablePlayer scriptablePlayer;

    [Header("Debugging Serialized")]
    [SerializeField] public PlayerState currentPlayerState = PlayerState.Alive;

    private uint lastReceivedDiedTick;
    private uint lastReceivedRespawnTick;
    private uint lastReceivedHealthTick;

    [SerializeField]
    private float _currentHealth;
    private float currentHealth
    {
        get { return _currentHealth; }
        set
        {
            // Taking Damage
            if (value < _currentHealth)
            {
                if (player.IsLocal) localPlayerHud.FadeHurtOverlay();
                else hurtParticles.Play();

                playerAudioSource.pitch = Utilities.GetRandomPitch();
                playerAudioSource.PlayOneShot(scriptablePlayer.playerHurtAudio, scriptablePlayer.playerHurtAudioVolume);
            }

            // Healing
            if (value >= _currentHealth)
            {
                if (player.IsLocal) localPlayerHud.FadeHealOverlay();

                playerAudioSource.pitch = Utilities.GetRandomPitch();
                playerAudioSource.PlayOneShot(scriptablePlayer.playerHealAudio, scriptablePlayer.playerHealAudioVolume);
            }

            _currentHealth = value > scriptablePlayer.maxHealth ? scriptablePlayer.maxHealth : value < 0 ? 0 : value;

            if (player.IsLocal) localPlayerHud.UpdateHealthDisplay(_currentHealth);
            if (NetworkManager.Singleton.Server.IsRunning) SendUpdatedHealth();
        }
    }

    private void Start()
    {
        currentHealth = scriptablePlayer.maxHealth;
    }

    #region Dying
    private void HandleServerPlayerDied(bool wasKilled, ushort killerId, uint tick)
    {
        if (tick <= lastReceivedDiedTick) return;
        lastReceivedDiedTick = tick;

        Die(wasKilled ? (ushort?)killerId : null);
    }

    private void Die(ushort? id)
    {
        if (currentPlayerState == PlayerState.Dead) return;
        currentPlayerState = PlayerState.Dead;

        // Killfeed Capsule
        if (id != null) GameManager.Singleton.SpawnKillFeedCapsule(Player.list[(ushort)id].username, Player.list[(ushort)id].playerShooting.currentWeapon.weaponIcon, player.username);
        else GameManager.Singleton.SpawnKillFeedCapsule("", suicideIcon, player.username);

        // Spectator
        if (player.IsLocal)
        {
            localPlayerCameraHolder.SetActive(false);
            SpectateCameraManager.Singleton.EnableDeathSpectateMode(id, GameManager.respawnTime);
        }

        //  Particles
        playerAudioSource.pitch = Utilities.GetRandomPitch();
        playerAudioSource.PlayOneShot(scriptablePlayer.playerDieAudio, scriptablePlayer.playerDieAudioVolume);
        deathParticles.Play();

        playerCharacter.SetActive(false);
        playerShooting.PlayerDied();
        playerMovement.PlayerDied();

        if (NetworkManager.Singleton.Server.IsRunning)
        {
            Invoke(nameof(StartRespawn), GameManager.respawnTime);
            GameManager.Singleton.AddDeathToPlayerScore(player.Id);
            SendPlayerDied(id);
        }
    }

    public void InstaKill()
    {
        if (currentPlayerState == PlayerState.Dead) return;

        currentHealth -= currentHealth;
        Die(null);
    }
    #endregion

    #region Respawning
    private void HandleServerPlayerRespawned(Vector3 pos, uint tick)
    {
        if (tick <= lastReceivedRespawnTick) return;
        lastReceivedRespawnTick = tick;

        transform.position = pos;
        StartRespawn();
    }

    private void StartRespawn()
    {
        StartCoroutine(Respawn());
    }
    #endregion

    #region Health
    private void HandleServerHealth(float health, uint tick)
    {
        if (tick <= lastReceivedHealthTick) return;
        lastReceivedHealthTick = tick;

        currentHealth = health;
    }

    public bool ReceiveDamage(float damage, bool critical, ushort? id)
    {
        if (currentPlayerState != PlayerState.Alive) return false;

        currentHealth -= damage;

        if (!player.IsLocal) PlayerHitEffects((int)damage, critical);

        if (currentHealth <= 0)
        {
            Die(id);
            return true;
        }
        return false;
    }

    public void RecoverHealth(float healAmount)
    {
        if (currentPlayerState == PlayerState.Dead) return;

        currentHealth += healAmount;
    }
    #endregion

    #region Effects
    public void PlayerHitEffects(int damage, bool critical)
    {
        DamageIndicator indicator = Instantiate(netPlayerDamageIndicator);
        indicator.transform.position = transform.position;
        indicator.transform.DOMove(indicator.transform.position + new Vector3(Random.Range(-2f, 2f), Random.Range(2f, 3f), Random.Range(-2f, 2f)), 0.4f).SetEase(Ease.OutSine);
        indicator.DisplayDamage((int)damage, critical);
    }
    #endregion

    #region ServerSenders
    private void SendPlayerDied(ushort? id)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.playerDied);
        message.AddUShort(player.Id);

        bool wasKilled = id != null;
        message.AddBool(wasKilled);
        message.AddUShort(wasKilled ? (ushort)id : (ushort)0);

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
        message.AddFloat(currentHealth);
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
            player.playerHealth.HandleServerPlayerDied(message.GetBool(), message.GetUShort(), message.GetUInt());
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
            player.playerHealth.HandleServerHealth(message.GetFloat(), message.GetUInt());
        }
    }
    #endregion

    private IEnumerator Respawn()
    {
        if (currentPlayerState != PlayerState.Dead) yield break;
        currentPlayerState = PlayerState.Invincible;

        // Spawn
        if (NetworkManager.Singleton.Server.IsRunning)
        {
            transform.position = SpawnHandler.Instance.GetSpawnLocation();
            currentHealth = scriptablePlayer.maxHealth;
            SendPlayerRespawned();
        }

        // Spectator
        if (player.IsLocal)
        {
            localPlayerCameraHolder.SetActive(true);
            SpectateCameraManager.Singleton.DisableSpectateMode();
        }

        shieldsHolder.SetActive(true);
        shieldAnimator.Play("ShieldRaise");

        playerCharacter.SetActive(true);
        playerShooting.PlayerRespawned();
        playerMovement.PlayerRespawned();

        // Finish Invincibility
        yield return new WaitForSeconds(scriptablePlayer.invincibilityTime);
        shieldsHolder.SetActive(false);
        currentPlayerState = PlayerState.Alive;
    }
}
