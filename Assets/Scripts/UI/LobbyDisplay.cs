using Steamworks;
using UnityEngine;
using TMPro;

public class LobbyDisplay : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] public TextMeshProUGUI lobbyName;
    [SerializeField] public TextMeshProUGUI playerCount;
    [SerializeField] public TextMeshProUGUI matchStatus;

    [HideInInspector] public CSteamID lobbyId;

    public void JoinLobby()
    {
        MainMenu.Instance.JoinLobby((ulong)lobbyId);
    }
}
