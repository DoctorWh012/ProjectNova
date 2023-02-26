using TMPro;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;

public class MainMenu : MonoBehaviour
{
    public static MainMenu Instance;

    [Header("Components")]
    [SerializeField] private GameObject mainMenu;
    [SerializeField] private GameObject[] otherMenus;

    [Space(10)]
    [Header("Networking Components")]
    [SerializeField] private Transform display;
    [SerializeField] private GameObject lobbyDisplay;

    private List<GameObject> lobbyDisplays = new List<GameObject>();

    private void Awake()
    {
        Instance = this;
        GuaranteeStartAtMainMenu();
    }

    public void HostClicked()
    {
        LobbyManager.Singleton.CreateLobby();
    }

    public void SearchClicked()
    {
        LobbyManager.Singleton.GetLobbiesList();
    }

    public void CreateLobbyList(List<CSteamID> lobbyIDS, LobbyDataUpdate_t result)
    {
        for (int i = 0; i < lobbyIDS.Count; i++)
        {
            if (lobbyIDS[i].m_SteamID != result.m_ulSteamIDLobby) continue;
            Debug.LogWarning("Created A Lobby");

            LobbyDisplay lobbyDis = Instantiate(lobbyDisplay).GetComponent<LobbyDisplay>();
            lobbyDisplays.Add(lobbyDis.gameObject);
            lobbyDis.transform.SetParent(display);

            lobbyDis.lobbyName.SetText(SteamMatchmaking.GetLobbyData((CSteamID)lobbyIDS[i].m_SteamID, "name"));
            lobbyDis.playerCount.SetText($"{SteamMatchmaking.GetNumLobbyMembers((CSteamID)lobbyIDS[i].m_SteamID)}/{SteamMatchmaking.GetLobbyMemberLimit((CSteamID)lobbyIDS[i].m_SteamID)}");
            lobbyDis.matchStatus.SetText($"{SteamMatchmaking.GetLobbyData((CSteamID)lobbyIDS[i].m_SteamID, "status")}");
            lobbyDis.lobbyId = (CSteamID)lobbyIDS[i].m_SteamID;
        }
    }

    public void DestroyOldLobbiesDiplays()
    {
        foreach (GameObject display in lobbyDisplays)
        {
            Destroy(display);
            lobbyDisplays.Remove(display);
        }
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    private void GuaranteeStartAtMainMenu()
    {
        mainMenu.SetActive(true);
        foreach (GameObject menu in otherMenus) { menu.SetActive(false); }
    }
}
