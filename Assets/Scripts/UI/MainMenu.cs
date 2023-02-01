using TMPro;
using Riptide;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Steamworks;

public class MainMenu : MonoBehaviour
{
    public static MainMenu Instance;
    [SerializeField] private TMP_InputField usernameField;
    [SerializeField] private TMP_InputField ipField;
    [SerializeField] private GameObject mainMenu;
    [SerializeField] private GameObject[] otherMenus;

    private void Awake()
    {
        Instance = this;
        GuaranteeStartAtMainMenu();
    }

    public void ConnectClicked()
    {
        LobbyManager.Singleton.JoinLobby(ulong.Parse(ipField.text));
    }

    public void ConnectLocal()
    {
        LobbyManager.Singleton.CreateLobby();
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public IEnumerator SendName()
    {
        string playerName;
        if (SteamManager.Initialized) playerName = SteamFriends.GetPersonaName();
        else playerName = "";

        Message message = Message.Create(MessageSendMode.Reliable, ClientToServerId.name);
        message.AddString(playerName);

        if (SceneManager.GetActiveScene().name != "RiptideLobby") SceneManager.LoadScene("RiptideLobby");
        while (SceneManager.GetActiveScene().name != "RiptideLobby") yield return null;

        NetworkManager.Singleton.Client.Send(message);
    }

    private void GuaranteeStartAtMainMenu()
    {
        mainMenu.SetActive(true);
        foreach (GameObject menu in otherMenus) { menu.SetActive(false); }
    }
}
