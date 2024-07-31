using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class SettingsMenu : MonoBehaviour
{
    [Header("SettingsMenu")]
    [SerializeField] private GameObject generalMenu;
    [SerializeField] private GameObject videoMenu;
    [SerializeField] private GameObject controlsMenu;

    [SerializeField] private Slider fovSlider;
    [SerializeField] private TextMeshProUGUI fovSliderTxt;

    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TextMeshProUGUI sensitivitySliderTxt;
    [SerializeField] private Slider zoomSensitivitySlider;
    [SerializeField] private TextMeshProUGUI zoomSensitivitySliderTxt;

    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private TextMeshProUGUI masterVolumeSliderTxt;

    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private TextMeshProUGUI musicVolumeSliderTxt;

    [SerializeField] private Toggle vSyncToggle;
    [SerializeField] private Toggle fullScreenToggle;
    [SerializeField] private Toggle renderArmsToggle;

    [SerializeField] private TMP_Dropdown framerateDropdown;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown graphicsDropdown;

    [SerializeField] private TMP_Dropdown crosshairTypeDropdown;
    [SerializeField] private TMP_Dropdown crosshairColorDropdown;

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

    internal void AddListenerToSettingsSliders()
    {
        fovSlider.onValueChanged.AddListener(delegate { UpdateSliderDisplayTxt(fovSliderTxt, fovSlider); });
        sensitivitySlider.onValueChanged.AddListener(delegate { UpdateSliderDisplayTxt(sensitivitySliderTxt, sensitivitySlider); });
        zoomSensitivitySlider.onValueChanged.AddListener(delegate { UpdateSliderDisplayTxt(zoomSensitivitySliderTxt, zoomSensitivitySlider); });
        masterVolumeSlider.onValueChanged.AddListener(delegate { UpdateSliderDisplayTxt(masterVolumeSliderTxt, masterVolumeSlider, 100); });
        musicVolumeSlider.onValueChanged.AddListener(delegate { UpdateSliderDisplayTxt(musicVolumeSliderTxt, musicVolumeSlider, 100); });
    }

    private void OnGUI()
    {
        if (!(Event.current.isKey || Event.current.isMouse) || !buttonWaitingForRebind) return;

        KeyCode key = Event.current.isKey ? Event.current.keyCode : Event.current.button == 0 ? KeyCode.Mouse0 : Event.current.button == 1 ? KeyCode.Mouse1 : Event.current.button == 2 ? KeyCode.Mouse2 : Event.current.button == 3 ? KeyCode.Mouse3 : Event.current.button == 4 ? KeyCode.Mouse4 : Event.current.button == 5 ? KeyCode.Mouse5 : Event.current.button == 6 ? KeyCode.Mouse6 : KeyCode.Mouse0;
        buttonWaitingForRebind.SetKey(key);
        buttonWaitingForRebind = null;
        rebindPopUp.SetActive(false);
    }

    internal void UpdateSliderDisplayTxt(TextMeshProUGUI display, Slider slider, int multiplier = 1)
    {
        float i = slider.value * multiplier;
        display.SetText(i.ToString("#.##"));
    }

    #region SettingsMenu
    internal void UpdateSettingsValues()
    {
        fovSlider.value = SettingsManager.playerPreferences.cameraFov;
        sensitivitySlider.value = SettingsManager.playerPreferences.sensitivity;
        zoomSensitivitySlider.value = SettingsManager.playerPreferences.zoomSensitivity;
        masterVolumeSlider.value = SettingsManager.playerPreferences.masterVolume;
        musicVolumeSlider.value = SettingsManager.playerPreferences.musicVolume;

        vSyncToggle.isOn = SettingsManager.playerPreferences.vSync;
        fullScreenToggle.isOn = SettingsManager.playerPreferences.fullScreen;
        renderArmsToggle.isOn = SettingsManager.playerPreferences.renderArms;
        crosshairTypeDropdown.value = SettingsManager.playerPreferences.crosshairType;
        crosshairColorDropdown.value = SettingsManager.playerPreferences.crosshairColor;

        framerateDropdown.value = SettingsManager.playerPreferences.maxFrameRateIndex;
        graphicsDropdown.value = SettingsManager.playerPreferences.graphics;

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
        PlayerPreferences prefs = new PlayerPreferences();
        prefs.resWidth = filteredResolutions[resolutionDropdown.value].width;
        prefs.resHeight = filteredResolutions[resolutionDropdown.value].height;
        prefs.maxFrameRateIndex = framerateDropdown.value;
        prefs.graphics = graphicsDropdown.value;
        prefs.vSync = vSyncToggle.isOn;
        prefs.fullScreen = fullScreenToggle.isOn;
        prefs.sensitivity = (int)sensitivitySlider.value;
        prefs.zoomSensitivity = (int)zoomSensitivitySlider.value;
        prefs.cameraFov = (int)fovSlider.value;
        prefs.masterVolume = masterVolumeSlider.value;
        prefs.musicVolume = musicVolumeSlider.value;
        prefs.renderArms = renderArmsToggle.isOn;
        prefs.crosshairType = crosshairTypeDropdown.value;
        prefs.crosshairColor = crosshairColorDropdown.value;

        prefs.forwardKey = forward.key;
        prefs.backwardKey = backward.key;
        prefs.rightKey = right.key;
        prefs.leftKey = left.key;
        prefs.jumpKey = jump.key;
        prefs.dashKey = dash.key;
        prefs.crouchKey = crouch.key;
        prefs.interactKey = interact.key;
        prefs.fireBtn = fire.key;
        prefs.altFireBtn = altFire.key;
        prefs.reloadKey = reload.key;
        prefs.primarySlotKey = primarySlot.key;
        prefs.secondarySlotKey = secondarySlot.key;
        prefs.tertiarySlotKey = tertiarySlot.key;

        SettingsManager.UpdateJson(prefs);
    }

    internal void GetAvailableResolutions()
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

    internal void DisableAllSettingsMenus()
    {
        generalMenu.SetActive(false);
        videoMenu.SetActive(false);
        controlsMenu.SetActive(false);
        rebindPopUp.SetActive(false);
        buttonWaitingForRebind = null;
    }

    #endregion
}
