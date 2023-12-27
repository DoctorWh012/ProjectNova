using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Riptide;

/* WORK REMINDER

    Implement Crosshair Color Customization

*/

public class PlayerHud : SettingsMenu
{
    public static bool Focused { get; private set; } = true;

    [Header("Components")]
    [SerializeField] private ScriptablePlayer playerSettings;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerShooting playerShooting;

    [Header("Colors")]
    [SerializeField] private Color dashAvailableColor;
    [SerializeField] private Color highlitedColor;
    [SerializeField] private Color fadedColor;

    [Header("Health Battery")]
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private Slider batteryLevel;

    [Header("Abilities Panel")]
    [SerializeField] private Image[] dashIcons;
    [SerializeField] public Slider[] dashSliders;
    [SerializeField] private Image groundSlamIcon;
    [SerializeField] public Slider groundSlamSlider;

    [Header("Weapons Panel")]
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private TextMeshProUGUI[] weaponsSlotsText;
    [SerializeField] private TextMeshProUGUI[] weaponsNamesText;
    [SerializeField] private Image[] weaponsImages;

    [Header("Crosshairs")]
    [SerializeField] private GameObject[] crosshairs;
    [SerializeField] private Slider reloadSlider;

    [Header("UI Texts")]
    [SerializeField] private TextMeshProUGUI mediumBottomText;
    [SerializeField] private TextMeshProUGUI pingTxt;
    [SerializeField] private TextMeshProUGUI speedometerText;

    [Header("Menus")]
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private Button respawnBtn;
    [SerializeField] private GameObject settingsMenu;
    [SerializeField] private GameObject gameHud;
    [SerializeField] private GameObject matchSettingsMenu;

    [Header("Match Settings Menu")]
    [SerializeField] private Slider matchDurationSlider;
    [SerializeField] private TextMeshProUGUI matchDurationTxt;
    [SerializeField] private Slider matchRespawnTimeSlider;
    [SerializeField] private TextMeshProUGUI matchRespawnTimeTxt;
    [SerializeField] private GameObject startMatchBtn;
    [SerializeField] private GameObject cancelMatchBtn;

    private int currentCrosshairIndex;
    private Vector3 crosshairScale;
    private Vector3 crosshairShotScale;
    private float crosshairShrinkTime;
    private float crosshairShrinkTimer;

    private void Awake()
    {
        SettingsManager.updatedPlayerPrefs += UpdatePreferences;
    }

    private void OnApplicationQuit()
    {
        SettingsManager.updatedPlayerPrefs -= UpdatePreferences;
    }

    private void OnDestroy()
    {
        SettingsManager.updatedPlayerPrefs -= UpdatePreferences;
    }

    private void Start()
    {
        Focused = true;

        AddListenerToSettingsSliders();

        matchDurationSlider.onValueChanged.AddListener(delegate { UpdateSliderDisplayTxt(matchDurationTxt, matchDurationSlider); });
        matchRespawnTimeSlider.onValueChanged.AddListener(delegate { UpdateSliderDisplayTxt(matchRespawnTimeTxt, matchRespawnTimeSlider); });

        ResetWeaponsOnSlots();
        ResetAllUITexts();
        DisableAllMenus();

        SettingsManager.VerifyJson();
        UpdatePreferences();
    }

    private void Update()
    {
        if (Input.GetKeyDown(SettingsManager.playerPreferences.pauseKey)) PauseUnpause();
        ScaleDownCrosshair();
        pingTxt.SetText($"Ping: {NetworkManager.Singleton.Client.RTT}");
    }

    private void UpdatePreferences()
    {
        UpdateSettingsValues();
        if (SettingsManager.playerPreferences.crosshairType == 0 && playerShooting.activeGun) UpdateCrosshair((int)playerShooting.activeGun.crosshairType, playerShooting.activeGun.crosshairScale, playerShooting.activeGun.crosshairShotScale, playerShooting.activeGun.crosshairShrinkTime);
        else UpdateCrosshair((int)CrosshairType.dot, 1, 1, 0);
    }

    #region Menus
    public void PauseUnpause()
    {
        Focused = !Focused;
        DisableAllMenus();
        pauseMenu.SetActive(!Focused);
        gameHud.SetActive(Focused);

        Cursor.lockState = Focused ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !Focused;
    }

    public void OpenSettingsMenu()
    {
        DisableAllMenus();
        settingsMenu.SetActive(true);

        GetAvailableResolutions();

        UpdateSettingsValues();
        DisableAllSettingsMenus();
        EnterGeneralMenu();
    }

    public void ReturnToPauseMenu()
    {
        DisableAllMenus();
        pauseMenu.SetActive(true);
    }

    public void OpenCloseMatchSettingsMenu()
    {
        Focused = !Focused;

        startMatchBtn.SetActive(false);
        cancelMatchBtn.SetActive(false);

        if (MatchManager.currentMatchState == MatchState.Waiting) startMatchBtn.SetActive(true);
        else cancelMatchBtn.SetActive(true);

        matchSettingsMenu.SetActive(!Focused);

        Cursor.lockState = Focused ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !Focused;
    }

    public void Respawn()
    {
        if (playerHealth.currentPlayerState == PlayerState.Dead) return;

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

    public void StartMatch()
    {
        MatchManager.Singleton.StartMatch(GameMode.FreeForAll, Scenes.MapFacility, (int)matchRespawnTimeSlider.value, (int)matchDurationSlider.value);
        OpenCloseMatchSettingsMenu();
    }

    public void CancelMatch()
    {
        MatchManager.Singleton.EndMatch();
        OpenCloseMatchSettingsMenu();
    }

    private void DisableAllMenus()
    {
        pauseMenu.SetActive(false);
        settingsMenu.SetActive(false);
        matchSettingsMenu.SetActive(false);
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

    public void UpdateSpeedometerText(string text)
    {
        speedometerText.SetText(text);
    }

    private void ResetAllUITexts()
    {
        mediumBottomText.SetText("");
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

    public void UpdateDashIcons(int availableDashes)
    {
        for (int i = 0; i < dashIcons.Length; i++)
        {
            dashIcons[i].color = i < availableDashes ? dashAvailableColor : fadedColor;
            dashSliders[i].value = i < availableDashes ? 1 : 0;
        }
    }

    public void UpdateCrosshair(int crosshairIndex, float weaponCrosshairScale, float weaponcrosshairShotScale, float weaponcrosshairShrinkTime)
    {
        DisableAllCrosshairs();

        currentCrosshairIndex = crosshairIndex;
        crosshairs[currentCrosshairIndex].SetActive(true);

        crosshairShrinkTimer = 0;
        crosshairScale = new Vector3(weaponCrosshairScale, weaponCrosshairScale, weaponCrosshairScale);
        crosshairShotScale = new Vector3(weaponcrosshairShotScale, weaponcrosshairShotScale, weaponcrosshairShotScale);
        crosshairShrinkTime = weaponcrosshairShrinkTime;

        crosshairs[currentCrosshairIndex].transform.localScale = crosshairScale;
    }

    public void UpdateReloadSlider(float val)
    {
        if (val >= 1)
        {
            reloadSlider.value = 0;
            return;
        }

        reloadSlider.value = val;
    }

    public void ScaleCrosshairShot()
    {
        crosshairs[currentCrosshairIndex].transform.localScale = crosshairShotScale;
        crosshairShrinkTimer = 0;
    }

    private void ScaleDownCrosshair()
    {
        if (crosshairShrinkTimer >= crosshairShrinkTime) return;

        crosshairShrinkTimer += Time.deltaTime;
        crosshairs[currentCrosshairIndex].transform.localScale = Vector3.Lerp(crosshairShotScale, crosshairScale, crosshairShrinkTimer / crosshairShrinkTime);
    }

    private void DisableAllCrosshairs()
    {
        for (int i = 0; i < crosshairs.Length; i++) crosshairs[i].SetActive(false);
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
