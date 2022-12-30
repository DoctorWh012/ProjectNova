using TMPro;
using Riptide;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        NetworkManager.Singleton.Connect(ipField.text);
    }

    public void ConnectLocal()
    {
        NetworkManager.Singleton.ConnectHost();
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public IEnumerator SendName()
    {
        Message message = Message.Create(MessageSendMode.Reliable, ClientToServerId.name);
        message.AddString(usernameField.text);

        if (SceneManager.GetActiveScene().name != "RiptideMultiplayer") SceneManager.LoadScene("RiptideMultiplayer");
        while (SceneManager.GetActiveScene().name != "RiptideMultiplayer") yield return null;

        NetworkManager.Singleton.Client.Send(message);
    }

    private void GuaranteeStartAtMainMenu()
    {
        mainMenu.SetActive(true);
        foreach (GameObject menu in otherMenus) { menu.SetActive(false); }
    }
}
