using UnityEngine;
using System.Collections.Generic;

public class Interactable : MonoBehaviour
{
    [TextArea]
    [SerializeField] private string popUpMessage;

    protected List<Player> players = new List<Player>();
    Player player;

    public void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        player = other.GetComponentInParent<Player>();
        if (player.IsLocal) player.localPlayerHud.UpdateMediumBottomText(string.Format(popUpMessage, SettingsManager.playerPreferences.interactKey));
        players.Add(player);
    }

    public void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        player = other.GetComponentInParent<Player>();
        if (player.IsLocal) player.localPlayerHud.UpdateMediumBottomText("");
        players.Remove(player);
    }
}
