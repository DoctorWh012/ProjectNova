using System.Collections;
using System.Collections.Generic;
using Riptide;
using UnityEngine;

public class PlayerScore : MonoBehaviour
{
    [SerializeField] private Player player;
    private int _deaths;
    public int deaths
    {
        get { return _deaths; }

        set
        {
            _deaths = value;
            SendScore();
        }
    }

    private int _kills;
    public int kills
    {
        get { return _kills; }
        set
        {
            _kills = value;
            SendScore();
        }
    }

    #region Messages
    private void SendScore()
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.playerScore);
        message.AddUShort(player.Id);
        message.AddInt(deaths);
        message.AddInt(kills);

        NetworkManager.Singleton.Server.SendToAll(message);
    }

    #endregion
}
