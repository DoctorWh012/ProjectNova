using UnityEngine;
using System.IO;

[System.Serializable]
public class PlayerPreferences
{
    // Resolutions and graphics
    public int resHeight;
    public int resWidth;

    public int maxFrameRateIndex;

    public int graphics;

    public bool vSync;
    public bool fullScreen;

    // Camera
    public int sensitivity;
    public int zoomSensitivity;
    public int cameraFov;

    // Audio
    public float masterVolume;
    public float musicVolume;

    // Miscellaneous
    public bool renderArms;
    public int crosshairType;
    public int crosshairColor;

    // Keybinds
    // Interactions
    public KeyCode pauseKey = KeyCode.Escape;
    public KeyCode scoreboardKey = KeyCode.Tab;
    public KeyCode interactKey;

    // Movement
    public KeyCode forwardKey;
    public KeyCode backwardKey;
    public KeyCode rightKey;
    public KeyCode leftKey;
    public KeyCode jumpKey;
    public KeyCode crouchKey;
    public KeyCode dashKey;

    // Shooting
    public KeyCode fireBtn;
    public KeyCode altFireBtn;
    public KeyCode reloadKey;
    public KeyCode primarySlotKey;
    public KeyCode secondarySlotKey;
    public KeyCode tertiarySlotKey;

    public PlayerPreferences()
    {
        // Resolutions and graphics
        resHeight = 1080;
        resWidth = 1920;

        maxFrameRateIndex = 0;

        graphics = 1;

        vSync = false;
        fullScreen = true;

        // Camera
        sensitivity = 20;
        zoomSensitivity = 10;
        cameraFov = 90;

        // Audio
        masterVolume = 1;
        musicVolume = 1;

        // Miscellaneous
        renderArms = true;
        crosshairType = 0;
        crosshairColor = 0;

        // Keybinds
        // Interactions
        pauseKey = KeyCode.Escape;
        scoreboardKey = KeyCode.Tab;
        interactKey = KeyCode.E;

        // Movement
        forwardKey = KeyCode.W;
        backwardKey = KeyCode.S;
        rightKey = KeyCode.D;
        leftKey = KeyCode.A;
        jumpKey = KeyCode.Space;
        crouchKey = KeyCode.LeftControl;
        dashKey = KeyCode.LeftShift;

        // Shooting
        fireBtn = KeyCode.Mouse0;
        altFireBtn = KeyCode.Mouse1;
        reloadKey = KeyCode.R;
        primarySlotKey = KeyCode.Alpha1;
        secondarySlotKey = KeyCode.Alpha2;
        tertiarySlotKey = KeyCode.Alpha3;
    }
}

public static class SettingsManager
{
    public delegate void UpdatedPlayerPrefs();
    public static event UpdatedPlayerPrefs updatedPlayerPrefs;

    public static PlayerPreferences playerPreferences;

    public static void VerifyJson()
    {
        if (!System.IO.File.Exists($"{Application.dataPath}/PlayerPrefs.json")) UpdateJson(null);
        else LoadFromJson();
    }

    public static void UpdateJson(PlayerPreferences playerPrefs)
    {
        if (playerPrefs == null) playerPrefs = new PlayerPreferences();

        string json = JsonUtility.ToJson(playerPrefs, true);
        File.WriteAllText($"{Application.dataPath}/PlayerPrefs.json", json);

        LoadFromJson();
    }

    public static void LoadFromJson()
    {
        string json = File.ReadAllText($"{Application.dataPath}/PlayerPrefs.json");
        PlayerPreferences playerPrefs = JsonUtility.FromJson<PlayerPreferences>(json);

        playerPreferences = playerPrefs;

        QualitySettings.SetQualityLevel(playerPreferences.graphics);
        QualitySettings.vSyncCount = playerPreferences.vSync ? 1 : 0;
        Application.targetFrameRate = GetDesiredRefreshRate(playerPreferences.maxFrameRateIndex);
        Screen.SetResolution(playerPreferences.resWidth, playerPreferences.resHeight, playerPreferences.fullScreen);

        updatedPlayerPrefs();
    }

    private static int GetDesiredRefreshRate(int index)
    {
        switch (index)
        {
            case 0:
                return 0;
            case 1:
                return Screen.currentResolution.refreshRate;
            case 2:
                return 30;
            case 3:
                return 60;
            case 4:
                return 144;
            case 5:
                return 244;
            case 6:
                return 512;
            default:
                return 0;
        }
    }
}
