using UnityEngine;
using Steamworks;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/* WORK REMINDER

    Button Remaping
    Working CrosshairSystem
    Menu beep sounds
    menu music
    New menu map

*/

public class MainMenu : MonoBehaviour
{
    public static MainMenu Instance;

    [Header("Audio")]
    [SerializeField] private AudioSource menuAudioSource;
    [Range(0, 1)]
    [SerializeField] public float buttonClickSoundsVolume = 1;
    [SerializeField] private AudioClip[] buttonClickSounds;

    [Header("MainMenu")]
    [SerializeField] private GameObject noticeMenu;
    [SerializeField] private GameObject startMenu;
    [SerializeField] private GameObject multiplayerMenu;
    [SerializeField] private GameObject settingsMenu;

    [Header("SettingsMenu")]
    [SerializeField] private GameObject generalMenu;
    [SerializeField] private GameObject videoMenu;
    [SerializeField] private GameObject controlsMenu;
    [SerializeField] private Slider fovSlider;
    [SerializeField] private TextMeshProUGUI fovSliderTxt;
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TextMeshProUGUI sensitivitySliderTxt;
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private TextMeshProUGUI masterVolumeSliderTxt;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private TextMeshProUGUI musicVolumeSliderTxt;
    [SerializeField] private Toggle vSyncToggle;
    [SerializeField] private Toggle fullScreenToggle;
    [SerializeField] private Toggle renderArmsToggle;
    [SerializeField] private TMP_Dropdown framerateDropdown;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private GameObject rebindPopUp;

    [Header("KeyRemappingButtons")]
    [SerializeField] private KeybindButton forward;
    [SerializeField] private KeybindButton backward;
    [SerializeField] private KeybindButton left;
    [SerializeField] private KeybindButton right;
    [SerializeField] private KeybindButton jump;
    [SerializeField] private KeybindButton dash;
    [SerializeField] private KeybindButton crouch;
    [SerializeField] private KeybindButton interact;
    [SerializeField] private KeybindButton fire;
    [SerializeField] private KeybindButton altFire;
    [SerializeField] private KeybindButton reload;
    [SerializeField] private KeybindButton primarySlot;
    [SerializeField] private KeybindButton secondarySlot;
    [SerializeField] private KeybindButton tertiarySlot;

    private List<MyResolution> filteredResolutions = new List<MyResolution>();
    private KeybindButton buttonWaitingForRebind;

    [Header("MultiplayerMenu")]
    [SerializeField] private GameObject findMatchMenu;
    [SerializeField] private GameObject refreshBtn;
    [SerializeField] private GameObject hostMenu;
    [SerializeField] private GameObject hostPopUp;
    [SerializeField] private GameObject joiningPopUp;
    [SerializeField] private TMP_Dropdown lobbyTypeDropdown;
    [SerializeField] private TMP_InputField lobbyNameInputField;
    [SerializeField] private Slider maxPlayersSlider;
    [SerializeField] private TextMeshProUGUI maxPlayerSliderTxt;
    [SerializeField] private GameObject lobbyCapsulePrefab;
    [SerializeField] private Transform lobbyCapsuleParent;

    private List<LobbyDisplay> lobbiesCapsules = new List<LobbyDisplay>();

    protected Callback<LobbyMatchList_t> lobbiesList;
    protected Callback<LobbyDataUpdate_t> lobbyDataUpdate;

    private void Awake()
    {
        Instance = this;
        SettingsManager.updatedPlayerPrefs += UpdateSettingsValues;
    }

    private void OnApplicationQuit()
    {
        SettingsManager.updatedPlayerPrefs -= UpdateSettingsValues;
    }

    private void OnDestroy()
    {
        SettingsManager.updatedPlayerPrefs -= UpdateSettingsValues;
    }

    private void Start()
    {
        fovSlider.onValueChanged.AddListener(delegate { UpdateSliderDisplayTxt(fovSliderTxt, fovSlider); });
        sensitivitySlider.onValueChanged.AddListener(delegate { UpdateSliderDisplayTxt(sensitivitySliderTxt, sensitivitySlider); });
        masterVolumeSlider.onValueChanged.AddListener(delegate { UpdateSliderDisplayTxt(masterVolumeSliderTxt, masterVolumeSlider, 100); });
        musicVolumeSlider.onValueChanged.AddListener(delegate { UpdateSliderDisplayTxt(musicVolumeSliderTxt, musicVolumeSlider, 100); });

        maxPlayersSlider.onValueChanged.AddListener(delegate { UpdateSliderDisplayTxt(maxPlayerSliderTxt, maxPlayersSlider); });

        lobbiesList = Callback<LobbyMatchList_t>.Create(OnGetLobbiesList);
        lobbyDataUpdate = Callback<LobbyDataUpdate_t>.Create(OnGetLobbyInfo);

        ReturnToMainMenu();
        noticeMenu.SetActive(true);
        SettingsManager.VerifyJson();
    }

    private void Update()
    {
        if (Input.GetKeyDown(SettingsManager.playerPreferences.pauseKey)) ReturnToMainMenu();
    }

    private void OnGUI()
    {
        if (!(Event.current.isKey || Event.current.isMouse) || !buttonWaitingForRebind) return;

        KeyCode key = Event.current.isKey ? Event.current.keyCode : Event.current.button == 0 ? KeyCode.Mouse0 : Event.current.button == 1 ? KeyCode.Mouse1 : Event.current.button == 2 ? KeyCode.Mouse2 : Event.current.button == 3 ? KeyCode.Mouse3 : Event.current.button == 4 ? KeyCode.Mouse4 : Event.current.button == 5 ? KeyCode.Mouse5 : Event.current.button == 6 ? KeyCode.Mouse6 : KeyCode.Mouse0;
        buttonWaitingForRebind.SetKey(key);
        buttonWaitingForRebind = null;
        rebindPopUp.SetActive(false);
    }

    public void PlayButtonClickSound()
    {
        menuAudioSource.pitch = Utilities.GetRandomPitch();
        menuAudioSource.PlayOneShot(buttonClickSounds[Random.Range(0, buttonClickSounds.Length)], buttonClickSoundsVolume);
    }

    #region MainMenu
    public void ReturnToMainMenu()
    {
        DisableAllMenus();
        startMenu.SetActive(true);
    }

    public void EnterSettingsMenu()
    {
        startMenu.SetActive(false);
        settingsMenu.SetActive(true);

        GetAvailableResolutions();

        UpdateSettingsValues();
        DisableAllSettingsMenus();
        EnterGeneralMenu();
    }

    public void EnterMultiplayerMenu()
    {
        startMenu.SetActive(false);
        hostPopUp.SetActive(false);
        joiningPopUp.SetActive(false);
        multiplayerMenu.SetActive(true);
        EnterFindMatchMenu();
    }

    public void EnterCreditsMenu()
    {

    }

    public void CloseNotice()
    {
        noticeMenu.SetActive(false);
    }

    public void QuitToDesktop()
    {
        Application.Quit();
    }
    #endregion

    #region MultiplayerMenu
    public void EnterFindMatchMenu()
    {
        RequestLobiesList();
        DisableAllMultiplayerMenus();
        findMatchMenu.SetActive(true);
        refreshBtn.SetActive(true);
    }

    public void EnterHostMenu()
    {
        DisableAllMultiplayerMenus();
        hostMenu.SetActive(true);
    }

    public void RequestLobiesList()
    {
        if (lobbiesCapsules.Count != 0) DestroyLobbiesCapsules();
        SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);
        SteamMatchmaking.RequestLobbyList();
    }

    private void OnGetLobbiesList(LobbyMatchList_t result)
    {
        for (int i = 0; i < result.m_nLobbiesMatching; i++)
        {
            CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(i);
            SteamMatchmaking.RequestLobbyData(lobbyId);
        }
    }

    private void OnGetLobbyInfo(LobbyDataUpdate_t result)
    {
        for (int i = 0; i < lobbiesCapsules.Count; i++)
        {
            if ((ulong)lobbiesCapsules[i].lobbyId == result.m_ulSteamIDLobby)
            {
                lobbiesCapsules[i].lobbyName.SetText(SteamMatchmaking.GetLobbyData(lobbiesCapsules[i].lobbyId, "name"));
                lobbiesCapsules[i].playerCount.SetText($"{SteamMatchmaking.GetNumLobbyMembers(lobbiesCapsules[i].lobbyId)}/{SteamMatchmaking.GetLobbyMemberLimit(lobbiesCapsules[i].lobbyId)}");
                lobbiesCapsules[i].matchStatus.SetText($"{SteamMatchmaking.GetLobbyData(lobbiesCapsules[i].lobbyId, "status")}");
                return;
            }
        }

        LobbyDisplay lobbyCapsule = Instantiate(lobbyCapsulePrefab).GetComponent<LobbyDisplay>();
        lobbiesCapsules.Add(lobbyCapsule);
        lobbyCapsule.transform.SetParent(lobbyCapsuleParent);
        lobbyCapsule.transform.localScale = Vector3.one;

        lobbyCapsule.lobbyName.SetText(SteamMatchmaking.GetLobbyData((CSteamID)result.m_ulSteamIDLobby, "name"));
        lobbyCapsule.playerCount.SetText($"{SteamMatchmaking.GetNumLobbyMembers((CSteamID)result.m_ulSteamIDLobby)}/{SteamMatchmaking.GetLobbyMemberLimit((CSteamID)result.m_ulSteamIDLobby)}");
        lobbyCapsule.matchStatus.SetText($"{SteamMatchmaking.GetLobbyData((CSteamID)result.m_ulSteamIDLobby, "status")}");
        lobbyCapsule.lobbyId = (CSteamID)result.m_ulSteamIDLobby;
    }

    private void DestroyLobbiesCapsules()
    {
        for (int i = 0; i < lobbiesCapsules.Count; i++)
        {
            Destroy(lobbiesCapsules[i].gameObject);
        }
        lobbiesCapsules.Clear();
    }

    public void HostALobby()
    {
        hostPopUp.SetActive(true);
        ELobbyType lobbyType = lobbyTypeDropdown.value == 0 ? lobbyType = ELobbyType.k_ELobbyTypePublic : ELobbyType.k_ELobbyTypeFriendsOnly;
        NetworkManager.Singleton.lobbyName = !string.IsNullOrEmpty(lobbyNameInputField.text) ? lobbyNameInputField.text.Trim() : $"{SteamFriends.GetPersonaName()}'s Lobby";
        NetworkManager.Singleton.maxPlayers = (int)maxPlayersSlider.value;
        SteamMatchmaking.CreateLobby(lobbyType, (int)maxPlayersSlider.value);
    }

    public void JoinLobby(ulong lobbyId)
    {
        joiningPopUp.SetActive(true);
        NetworkManager.Singleton.JoinLobby(lobbyId);
    }
    #endregion

    #region SettingsMenu
    private void UpdateSettingsValues()
    {
        fovSlider.value = SettingsManager.playerPreferences.cameraFov;
        sensitivitySlider.value = SettingsManager.playerPreferences.sensitivity;
        masterVolumeSlider.value = SettingsManager.playerPreferences.masterVolume;
        musicVolumeSlider.value = SettingsManager.playerPreferences.musicVolume;

        vSyncToggle.isOn = SettingsManager.playerPreferences.vSync;
        fullScreenToggle.isOn = SettingsManager.playerPreferences.fullScreen;
        renderArmsToggle.isOn = SettingsManager.playerPreferences.renderArms;

        framerateDropdown.value = SettingsManager.playerPreferences.maxFrameRateIndex;

        forward.SetKey(SettingsManager.playerPreferences.forwardKey);
        backward.SetKey(SettingsManager.playerPreferences.backwardKey);
        left.SetKey(SettingsManager.playerPreferences.leftKey);
        right.SetKey(SettingsManager.playerPreferences.rightKey);
        jump.SetKey(SettingsManager.playerPreferences.jumpKey);
        dash.SetKey(SettingsManager.playerPreferences.dashKey);
        crouch.SetKey(SettingsManager.playerPreferences.crouchKey);
        interact.SetKey(SettingsManager.playerPreferences.interactKey);
        fire.SetKey(SettingsManager.playerPreferences.fireBtn);
        altFire.SetKey(SettingsManager.playerPreferences.altFireBtn);
        reload.SetKey(SettingsManager.playerPreferences.reloadKey);
        primarySlot.SetKey(SettingsManager.playerPreferences.primarySlotKey);
        secondarySlot.SetKey(SettingsManager.playerPreferences.secondarySlotKey);
        tertiarySlot.SetKey(SettingsManager.playerPreferences.tertiarySlotKey);
    }

    public void EnterGeneralMenu()
    {
        DisableAllSettingsMenus();
        generalMenu.SetActive(true);
    }

    public void EnterVideoMenu()
    {
        DisableAllSettingsMenus();
        videoMenu.SetActive(true);
    }

    public void EnterControlsMenu()
    {
        DisableAllSettingsMenus();
        controlsMenu.SetActive(true);
    }

    public void SaveAndApply()
    {
        SettingsManager.UpdateJson(filteredResolutions[resolutionDropdown.value].width, filteredResolutions[resolutionDropdown.value].height, framerateDropdown.value, 0, vSyncToggle.isOn
        , fullScreenToggle.isOn, sensitivitySlider.value, (int)fovSlider.value, masterVolumeSlider.value, musicVolumeSlider.value, renderArmsToggle.isOn, forward.key, backward.key, left.key, right.key, jump.key, dash.key, crouch.key, interact.key, fire.key, altFire.key, reload.key, primarySlot.key, secondarySlot.key, tertiarySlot.key);
    }

    private void GetAvailableResolutions()
    {
        resolutionDropdown.ClearOptions();
        filteredResolutions.Clear();
        MyResolution tempRes;

        List<string> options = new List<string>();
        int currentResolutionIndex = 0;

        for (int i = 0; i < Screen.resolutions.Length; i++)
        {
            tempRes.width = Screen.resolutions[i].width;
            tempRes.height = Screen.resolutions[i].height;

            if (filteredResolutions.Contains(tempRes)) continue;

            options.Add($"{Screen.resolutions[i].width}x{Screen.resolutions[i].height}");
            filteredResolutions.Add(tempRes);

            if (Screen.resolutions[i].width == Screen.width && Screen.resolutions[i].height == Screen.height) currentResolutionIndex = i;
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();
    }

    public void WaitForRebind(KeybindButton button)
    {
        buttonWaitingForRebind = button;
        rebindPopUp.SetActive(true);
    }

    #endregion
    #region VideoMenu
    #endregion
    #region ControlsMenu
    #endregion

    private void DisableAllMenus()
    {
        settingsMenu.SetActive(false);
        startMenu.SetActive(false);
        multiplayerMenu.SetActive(false);
    }

    private void DisableAllSettingsMenus()
    {
        generalMenu.SetActive(false);
        videoMenu.SetActive(false);
        controlsMenu.SetActive(false);
        rebindPopUp.SetActive(false);
        buttonWaitingForRebind = null;
    }

    private void DisableAllMultiplayerMenus()
    {
        findMatchMenu.SetActive(false);
        hostMenu.SetActive(false);
        refreshBtn.SetActive(false);
    }

    private void UpdateSliderDisplayTxt(TextMeshProUGUI display, Slider slider, int multiplier = 1)
    {
        float i = slider.value * multiplier;
        display.SetText(i.ToString("#.##"));
    }
}
