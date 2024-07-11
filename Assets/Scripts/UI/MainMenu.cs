using UnityEngine;
using Steamworks;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/* WORK REMINDER

    menu music
    New menu map

*/

public class MainMenu : SettingsMenu
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
    [SerializeField] private LobbyDisplay lobbyCapsulePrefab;
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
        AddListenerToSettingsSliders();

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

        LobbyDisplay lobbyCapsule = Instantiate(lobbyCapsulePrefab);
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
        for (int i = 0; i < lobbiesCapsules.Count; i++) Destroy(lobbiesCapsules[i].gameObject);
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

    private void DisableAllMultiplayerMenus()
    {
        findMatchMenu.SetActive(false);
        hostMenu.SetActive(false);
        refreshBtn.SetActive(false);
    }
}
