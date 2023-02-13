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
    [SerializeField] private TextMeshProUGUI versionTXT;
    [SerializeField] private GameObject mainMenu;
    [SerializeField] private GameObject[] otherMenus;

    private void Awake()
    {
        Instance = this;
        GuaranteeStartAtMainMenu();
        versionTXT.SetText(Application.version);
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

    private void GuaranteeStartAtMainMenu()
    {
        mainMenu.SetActive(true);
        foreach (GameObject menu in otherMenus) { menu.SetActive(false); }
    }
}
