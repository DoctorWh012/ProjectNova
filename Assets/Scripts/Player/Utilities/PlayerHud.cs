using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Riptide;
using Steamworks;

public class PlayerHud : MonoBehaviour
{
    public static bool Focused { get; private set; } = true;

    [Header("Components")]
    [SerializeField] private ScriptablePlayer playerSettings;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Health Battery")]
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private Slider batteryLevel;

    [Header("Abilities Panel")]
    [SerializeField] private Image[] dashIcons;
    [SerializeField] public Slider dashSlider;
    [SerializeField] private Image groundSlamIcon;
    [SerializeField] public Slider groundSlamSlider;

    [Header("Weapons Panel")]
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private TextMeshProUGUI[] weaponsSlotsText;
    [SerializeField] private TextMeshProUGUI[] weaponsNamesText;
    [SerializeField] private Image[] weaponsImages;

    [SerializeField] private Color highlitedColor;
    [SerializeField] private Color fadedColor;

    [Header("UI Texts")]
    [SerializeField] private TextMeshProUGUI mediumBottomText;
    [SerializeField] private TextMeshProUGUI bigTopText;
    [SerializeField] private TextMeshProUGUI mediumTopText;
    [SerializeField] private TextMeshProUGUI pingTxt;

    [Header("Menus")]
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private Button respawnBtn;
    [SerializeField] private GameObject settingsMenu;
    [SerializeField] private GameObject gameHud;

    private void Start()
    {
        Focused = true;
        ResetWeaponsOnSlots();
        ResetAllUITexts();
        DeactivateAllMenus();
    }

    private void Update()
    {
        if (Input.GetKeyDown(SettingsManager.playerPreferences.pauseKey)) PauseUnpause();
        pingTxt.SetText($"Ping: {NetworkManager.Singleton.Client.RTT}");
    }

    #region Menus
    public void PauseUnpause()
    {
        Focused = !Focused;
        DeactivateAllMenus();
        pauseMenu.SetActive(!Focused);
        gameHud.SetActive(Focused);

        Cursor.lockState = Focused ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !Focused;
    }

    public void OpenSettingsMenu()
    {
        DeactivateAllMenus();
        settingsMenu.SetActive(true);
    }

    public void Respawn()
    {
        if (NetworkManager.Singleton.Server.IsRunning) playerHealth.InstaKill();
        else SendSuicideMessage();

        respawnBtn.interactable = false;
        Invoke("EnableRespawnBtn", 10f);
    }

    private void EnableRespawnBtn()
    {
        respawnBtn.interactable = true;
    }

    public void ExitMatch()
    {
        if (NetworkManager.Singleton.Server.IsRunning) NetworkManager.Singleton.Server.Stop();
        NetworkManager.Singleton.Client.Disconnect();
    }

    private void DeactivateAllMenus()
    {
        pauseMenu.SetActive(false);
        settingsMenu.SetActive(false);
    }
    #endregion

    #region GameUi
    public void UpdateHealthDisplay(float health)
    {
        healthText.SetText($"{health.ToString("#")}%");
        batteryLevel.value = health / playerSettings.maxHealth;
    }

    public void UpdateAmmoDisplay(int currentAmmo, int maxAmmo)
    {
        ammoText.SetText($"{currentAmmo}/{maxAmmo}");
    }

    public void UpdateMediumBottomText(string text)
    {
        mediumBottomText.SetText(text);
    }

    public void UpdateBigTopText(string text)
    {
        bigTopText.SetText(text);
    }

    public void UpdateMediumTopText(string text)
    {
        mediumTopText.SetText(text);
    }

    private void ResetAllUITexts()
    {
        mediumBottomText.SetText("");
        bigTopText.SetText("");
        mediumTopText.SetText("");
    }

    public void HighlightWeaponOnSlot(int slot)
    {
        FadeWeaponsSlots();

        weaponsSlotsText[slot].color = highlitedColor;
        weaponsNamesText[slot].color = highlitedColor;
        weaponsImages[slot].color = highlitedColor;
    }

    private void FadeWeaponsSlots()
    {
        for (int i = 0; i < weaponsSlotsText.Length; i++)
        {
            weaponsSlotsText[i].color = fadedColor;
            weaponsNamesText[i].color = fadedColor;
            weaponsImages[i].color = fadedColor;
        }
    }

    public void UpdateWeaponOnSlot(int slot, string weaponName, Sprite weaponImage, bool switching)
    {
        weaponsNamesText[slot].SetText(weaponName);
        weaponsImages[slot].enabled = true;
        weaponsImages[slot].sprite = weaponImage;
        if (switching) HighlightWeaponOnSlot(slot);
    }

    public void ResetWeaponsOnSlots()
    {
        for (int i = 0; i < weaponsSlotsText.Length; i++)
        {
            weaponsNamesText[i].SetText("");
            weaponsImages[i].enabled = false;
        }
    }

    public void UpdateGroundSlamIcon(bool state)
    {
        groundSlamIcon.color = state ? highlitedColor : fadedColor;
    }

    public void UpdateDashIcons(int availableDashes, bool state)
    {
        for (int i = dashIcons.Length; i-- > 0;)
        {
            dashIcons[i].color = state ? highlitedColor : fadedColor;
            if (availableDashes == i) return;
        }
    }
    #endregion

    #region ClientToServerSenders
    private void SendSuicideMessage()
    {
        Message message = Message.Create(MessageSendMode.Reliable, ClientToServerId.playerSuicide);
        NetworkManager.Singleton.Client.Send(message);
    }
    #endregion

    #region ClientToServerHandlers
    [MessageHandler((ushort)ClientToServerId.playerSuicide)]
    private static void ReceiveSuicide(ushort fromClientId, Message message)
    {
        if (Player.list.TryGetValue(fromClientId, out Player player))
        {
            player.playerHealth.InstaKill();
        }
    }
    #endregion
}
