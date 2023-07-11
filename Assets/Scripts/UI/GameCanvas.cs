using TMPro;
using UnityEngine;
using UnityEngine.UI;
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

    [Header("Slots")]
    [SerializeField] private Image slot1Image;
    [SerializeField] private TextMeshProUGUI slot1Text;
    [SerializeField] private TextMeshProUGUI slot1WeaponName;
    [SerializeField] private Image slot2Image;
    [SerializeField] private TextMeshProUGUI slot2Text;
    [SerializeField] private TextMeshProUGUI slot2WeaponName;
    [SerializeField] private Image slot3Image;
    [SerializeField] private TextMeshProUGUI slot3Text;
    [SerializeField] private TextMeshProUGUI slot3WeaponName;

    private void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (!GameManager.Singleton.networking) return;
        else if (NetworkManager.Singleton.Client.IsConnected)
            pingText.SetText($"Ping = {NetworkManager.Singleton.Client.RTT}Ms");
    }

    public void SetUiPopUpText(string text)
    {
        uiPopUpText.SetText(text);
    }

    public void UpdateAmmunition(int magazine, int ammo)
    {
        ammoDisplayText.SetText(magazine + "/" + ammo);
    }

    public void UpdateHealthAmmount(string health)
    {
        healthText.SetText($"{health}%");
    }

    public void ChangeGunSlotIcon(int index, Sprite image, string name)
    {
        switch (index)
        {
            case 0:
                slot1Image.sprite = image;
                slot1WeaponName.SetText(name);
                break;
            case 1:
                slot2Image.sprite = image;
                slot2WeaponName.SetText(name);
                break;
            case 2:
                slot3Image.sprite = image;
                slot3WeaponName.SetText(name);
                break;
        }
        ChangeSlotOpacity(index);
    }

    public void ChangeSlotOpacity(int index)
    {
        if (slot1Image.sprite == null) slot1Image.color = new Color(1f, 1f, 1f, 0f);
        else slot1Image.color = new Color(1f, 1f, 1f, 0.3f);
        slot1Text.color = new Color(1f, 1f, 1f, 0.3f);
        slot1WeaponName.color = new Color(1f, 1f, 1f, 0.3f);

        if (slot2Image.sprite == null) slot2Image.color = new Color(1f, 1f, 1f, 0f);
        else slot2Image.color = new Color(1f, 1f, 1f, 0.3f);
        slot2Text.color = new Color(1f, 1f, 1f, 0.3f);
        slot2WeaponName.color = new Color(1f, 1f, 1f, 0.3f);

        if (slot3Image.sprite == null) slot3Image.color = new Color(1f, 1f, 1f, 0f);
        else slot3Image.color = new Color(1f, 1f, 1f, 0.3f);
        slot3Text.color = new Color(1f, 1f, 1f, 0.3f);
        slot3WeaponName.color = new Color(1f, 1f, 1f, 0.3f);

        switch (index)
        {
            case 0:
                slot1Image.color = new Color(1f, 1f, 1f, 0.9f);
                slot1Text.color = new Color(1f, 1f, 1f, 1f);
                slot1WeaponName.color = new Color(1f, 1f, 1f, 1f);
                break;
            case 1:
                slot2Image.color = new Color(1f, 1f, 1f, 0.9f);
                slot2Text.color = new Color(1f, 1f, 1f, 1f);
                slot2WeaponName.color = new Color(1f, 1f, 1f, 1f);
                break;
            case 2:
                slot3Image.color = new Color(1f, 1f, 1f, 0.9f);
                slot3Text.color = new Color(1f, 1f, 1f, 1f);
                slot3WeaponName.color = new Color(1f, 1f, 1f, 1f);
                break;
        }
    }
}
