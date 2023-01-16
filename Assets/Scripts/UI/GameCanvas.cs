using TMPro;
using UnityEngine;
using Riptide;

public class GameCanvas : MonoBehaviour
{
    public static GameCanvas Instance;

    [SerializeField] public TextMeshProUGUI uiPopUpText;
    [SerializeField] public TextMeshProUGUI bigPopUpText;
    [SerializeField] public TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI ammoDisplayText;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI pingText;

    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        if (NetworkManager.Singleton.Client.IsConnected)
        {
            pingText.SetText($"Ping = {NetworkManager.Singleton.Client.RTT}Ms");
        }
    }

    public void SetUiPopUpText(string text)
    {
        uiPopUpText.SetText(text);
    }

    private void UpdateAmmunition(int magazine, int ammo)
    {
        ammoDisplayText.SetText(magazine + "/" + ammo);
    }

    private void UpdateHealthAmmount(string health)
    {
        healthText.SetText($"{health}%");
    }

    [MessageHandler((ushort)ServerToClientId.ammoChanged)]
    private static void ChangeAmmo(Message message)
    {
        GameCanvas.Instance.UpdateAmmunition(message.GetInt(), message.GetInt());
    }

    [MessageHandler((ushort)ServerToClientId.healthChanged)]
    private static void ChangeHealth(Message message)
    {
        GameCanvas.Instance.UpdateHealthAmmount(message.GetString());
    }
}
