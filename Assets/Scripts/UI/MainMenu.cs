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

    [HideInInspector] public List<GameObject> lobbyDisplays = new List<GameObject>();

    private bool playerRequestedLobbies = false;

    private void Awake()
    {
        Instance = this;
        GuaranteeStartAtMainMenu();
    }

    public void SearchClicked()
    {
        LobbyManager.Singleton.GetLobbiesList();
        playerRequestedLobbies = true;
    }

    public void CreateLobbyList(List<CSteamID> lobbyIDS, LobbyDataUpdate_t result)
    {
        // Creates a display for a lobby if player asked for lobby list Or updates the lobby data if lobby data changed
        if (playerRequestedLobbies)
        {
            for (int i = 0; i < lobbyIDS.Count; i++)
            {
                if (lobbyIDS[i].m_SteamID != result.m_ulSteamIDLobby) continue;

                LobbyDisplay lobbyDis;

                if (playerRequestedLobbies)
                {
                    lobbyDis = Instantiate(lobbyDisplay).GetComponent<LobbyDisplay>();
                    lobbyDisplays.Add(lobbyDis.gameObject);
                    lobbyDis.transform.SetParent(display);
                    lobbyDis.transform.localScale = Vector3.one;
                }

                else lobbyDis = lobbyDisplays[i].GetComponent<LobbyDisplay>();

                lobbyDis.lobbyName.SetText(SteamMatchmaking.GetLobbyData((CSteamID)lobbyIDS[i].m_SteamID, "name"));
                lobbyDis.playerCount.SetText($"{SteamMatchmaking.GetNumLobbyMembers((CSteamID)lobbyIDS[i].m_SteamID)}/{SteamMatchmaking.GetLobbyMemberLimit((CSteamID)lobbyIDS[i].m_SteamID)}");
                lobbyDis.matchStatus.SetText($"{SteamMatchmaking.GetLobbyData((CSteamID)lobbyIDS[i].m_SteamID, "status")}");
                lobbyDis.lobbyId = (CSteamID)lobbyIDS[i].m_SteamID;
            }
        }

        playerRequestedLobbies = false;
    }

    public void DestroyOldLobbiesDiplays()
    {
        foreach (GameObject display in lobbyDisplays)
        {
            GameObject displayToDestroy = display;
            Destroy(displayToDestroy);
        }
        lobbyDisplays.Clear();
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
