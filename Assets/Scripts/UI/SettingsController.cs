using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.IO;

public class SettingsController : MonoBehaviour
{
    public static SettingsController Instance;
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TextMeshProUGUI sensitivitySliderText;

    [SerializeField] private Slider mainVolumeSlider;
    [SerializeField] private TextMeshProUGUI mainVolumeSliderText;

    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private TextMeshProUGUI musicVolumeSliderText;

    [SerializeField] private ButtonHover goodGraphicsBtn;
    [SerializeField] private TextMeshProUGUI goodGraphicsBtnText;

    [SerializeField] private ButtonHover shitGraphicsBtn;
    [SerializeField] private TextMeshProUGUI shitGraphicsBtnText;

    [SerializeField] private Toggle fullScreenToggle;
    [SerializeField] private Toggle vSyncToggle;
    private int desiredGraphics;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        LoadFromJson();
    }

    public void SetDesiredGraphics(int i)
    {
        desiredGraphics = i;
        HighlightGraphicsBtn();
    }

    private void HighlightGraphicsBtn()
    {
        switch (desiredGraphics)
        {

            case 0:
                DoTheHighlighting(shitGraphicsBtnText, shitGraphicsBtn, goodGraphicsBtnText, goodGraphicsBtn);
                break;
            case 1:
                DoTheHighlighting(goodGraphicsBtnText, goodGraphicsBtn, shitGraphicsBtnText, shitGraphicsBtn);
                break;
        }
    }

    private void DoTheHighlighting(TextMeshProUGUI toHighlightText, ButtonHover toHighlightBTN, TextMeshProUGUI toFadeText, ButtonHover toFadeBTN)
    {
        Color currentColor = toHighlightText.color;
        currentColor.a = 1;
        toHighlightText.fontSize = 45;
        toHighlightText.color = currentColor;
        toHighlightBTN.enabled = false;

        currentColor.a = 0.3f;
        toFadeText.color = currentColor;
        toFadeText.fontSize = 40;
        toFadeBTN.enabled = true;
    }

    public void UpdateSliderValue(TextMeshProUGUI sliderText)
    {

        if (sliderText == sensitivitySliderText)
        {
            sliderText.text = sensitivitySlider.value.ToString("#.00");
            return;
        }
        if (sliderText == mainVolumeSliderText)
        {
            sliderText.text = mainVolumeSlider.value.ToString("#.00");
            return;
        }
        if (sliderText == musicVolumeSliderText)
        {
            sliderText.text = musicVolumeSlider.value.ToString("#.00");
            return;
        }
    }

    public void SaveAndApply()
    {
        SaveToJson();
        LoadFromJson();
        if (UIManager.Instance == null) return;
        PlayerCam.Instance.GetSensitivity();
    }

    public void CreateJson()
    {
        PlayerPreferences playerPrefs = new PlayerPreferences();

        playerPrefs.resHeight = Screen.currentResolution.height;
        playerPrefs.resWidth = Screen.currentResolution.width;
        playerPrefs.graphics = 1;
        playerPrefs.sensitivity = 20;
        playerPrefs.mainVolume = 1;
        playerPrefs.musicVolume = 1;
        playerPrefs.fullScreen = fullScreenToggle.isOn;
        playerPrefs.vSync = vSyncToggle.isOn;

        string json = JsonUtility.ToJson(playerPrefs, true);
        File.WriteAllText($"{Application.dataPath}/PlayerPrefs.json", json);
    }

    public void SaveToJson()
    {
        PlayerPreferences playerPrefs = new PlayerPreferences();

        Resolution savedRes = ResolutionControl.Instance.GetAndSetResolution(fullScreenToggle.isOn);

        playerPrefs.resHeight = savedRes.height;
        playerPrefs.resWidth = savedRes.width;
        playerPrefs.graphics = desiredGraphics;
        playerPrefs.sensitivity = sensitivitySlider.value;
        playerPrefs.mainVolume = mainVolumeSlider.value;
        playerPrefs.musicVolume = musicVolumeSlider.value;
        playerPrefs.fullScreen = fullScreenToggle.isOn;
        playerPrefs.vSync = vSyncToggle.isOn;

        string json = JsonUtility.ToJson(playerPrefs, true);
        File.WriteAllText($"{Application.dataPath}/PlayerPrefs.json", json);
    }

    public void LoadFromJson()
    {
        if (!System.IO.File.Exists($"{Application.dataPath}/PlayerPrefs.json")) CreateJson();

        string json = File.ReadAllText($"{Application.dataPath}/PlayerPrefs.json");
        PlayerPreferences playerPrefs = JsonUtility.FromJson<PlayerPreferences>(json);

        desiredGraphics = playerPrefs.graphics;
        HighlightGraphicsBtn();
        QualitySettings.SetQualityLevel(desiredGraphics);

        sensitivitySlider.value = playerPrefs.sensitivity;
        UpdateSliderValue(sensitivitySliderText);

        mainVolumeSlider.value = playerPrefs.mainVolume;
        AudioListener.volume = playerPrefs.mainVolume;
        UpdateSliderValue(mainVolumeSliderText);

        musicVolumeSlider.value = playerPrefs.musicVolume;
        SoundManager.Instance.musicSource.volume = playerPrefs.musicVolume;
        UpdateSliderValue(musicVolumeSliderText);

        Screen.SetResolution(playerPrefs.resWidth, playerPrefs.resHeight, playerPrefs.fullScreen);
        fullScreenToggle.isOn = playerPrefs.fullScreen;

        vSyncToggle.isOn = playerPrefs.vSync;
        if (playerPrefs.vSync) QualitySettings.vSyncCount = 1;
        else QualitySettings.vSyncCount = 0;
    }
}


[System.Serializable]
public class PlayerPreferences
{
    public int resHeight;
    public int resWidth;
    public float sensitivity;
    public float mainVolume;
    public float musicVolume;
    public int graphics;
    public bool vSync;
    public bool fullScreen;
}
