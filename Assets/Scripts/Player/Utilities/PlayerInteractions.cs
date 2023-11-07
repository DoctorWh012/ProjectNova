using UnityEngine;
using Riptide;

public class PlayerInteractions : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Player player;
    [SerializeField] private ScriptablePlayer scriptablePlayer;

    [SerializeField] public float interactTimeCounter;
    private ushort lastReceivedInteractTick;

    private void Update()
    {
        interactTimeCounter = interactTimeCounter > 0 ? interactTimeCounter - Time.deltaTime : 0;

        if (!player.IsLocal) return;
        GetInput();
    }

    private void GetInput()
    {
        // Interacting
        if (Input.GetKeyDown(SettingsManager.playerPreferences.interactKey))
        {
            interactTimeCounter = scriptablePlayer.interactBufferTime;
            SendClientInteract();
        }
    }

    private void HandleClientInteract(ushort tick)
    {
        if (tick <= lastReceivedInteractTick) return;
        lastReceivedInteractTick = tick;

        interactTimeCounter = scriptablePlayer.interactBufferTime;
    }

    private void SendClientInteract()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ClientToServerId.playerInteract);
        message.AddUShort(NetworkManager.Singleton.serverTick);
        NetworkManager.Singleton.Client.Send(message);
    }

    [MessageHandler((ushort)ClientToServerId.playerInteract)]
    private static void GetClientInteract(ushort fromClientId, Message message)
    {
        if (Player.list.TryGetValue(fromClientId, out Player player))
        {
            player.playerInteractions.HandleClientInteract(message.GetUShort());
        }
    }
}
