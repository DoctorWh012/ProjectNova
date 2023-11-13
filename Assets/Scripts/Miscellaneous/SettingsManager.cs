using UnityEngine;
using UnityEngine.UI;
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
    public float sensitivity;
    public float cameraFov;

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
}

public static class SettingsManager
{
    public delegate void UpdatedPlayerPrefs();
    public static event UpdatedPlayerPrefs updatedPlayerPrefs;

    public static PlayerPreferences playerPreferences;

    public static void VerifyJson()
    {
        if (!System.IO.File.Exists($"{Application.dataPath}/PlayerPrefs.json")) UpdateJson();
        else LoadFromJson();
    }

    public static void UpdateJson(int resWidth = 0, int resHeight = 0, int maxFrameRateIndex = 0, int graphics = 1, bool vsync = true, bool fullScreen = true, float sensitivity = 20f, int cameraFov = 90
    , float mastervolume = 1f, float musicVolume = 1, bool renderArms = true, int crosshairType = 0, int crosshairColor = 0, KeyCode forward = KeyCode.W, KeyCode backward = KeyCode.S, KeyCode left = KeyCode.A, KeyCode right = KeyCode.D, KeyCode jump = KeyCode.Space, KeyCode dash = KeyCode.LeftShift, KeyCode crouch = KeyCode.LeftControl, KeyCode interact = KeyCode.E, KeyCode fire = KeyCode.Mouse0, KeyCode altFire = KeyCode.Mouse1, KeyCode reload = KeyCode.R, KeyCode primarySlot = KeyCode.Alpha1, KeyCode secondarySlot = KeyCode.Alpha2, KeyCode tertiarySlot = KeyCode.Alpha3)
    {
        PlayerPreferences playerPrefs = new PlayerPreferences();

        playerPrefs.resWidth = resWidth == 0 ? Screen.width : resWidth;
        playerPrefs.resHeight = resHeight == 0 ? Screen.height : resHeight;

        playerPrefs.maxFrameRateIndex = maxFrameRateIndex;

        playerPrefs.graphics = graphics;
        playerPrefs.vSync = vsync;
        playerPrefs.fullScreen = fullScreen;

        playerPrefs.sensitivity = sensitivity;
        playerPrefs.cameraFov = cameraFov;

        playerPrefs.masterVolume = mastervolume;
        playerPrefs.musicVolume = musicVolume;

        playerPrefs.renderArms = renderArms;

        playerPrefs.crosshairType = crosshairType;
        playerPrefs.crosshairColor = crosshairColor;

        playerPrefs.forwardKey = forward;
        playerPrefs.backwardKey = backward;
        playerPrefs.leftKey = left;
        playerPrefs.rightKey = right;
        playerPrefs.jumpKey = jump;
        playerPrefs.dashKey = dash;
        playerPrefs.crouchKey = crouch;
        playerPrefs.interactKey = interact;
        playerPrefs.fireBtn = fire;
        playerPrefs.altFireBtn = altFire;
        playerPrefs.reloadKey = reload;
        playerPrefs.primarySlotKey = primarySlot;
        playerPrefs.secondarySlotKey = secondarySlot;
        playerPrefs.tertiarySlotKey = tertiarySlot;

        string json = JsonUtility.ToJson(playerPrefs, true);
        File.WriteAllText($"{Application.dataPath}/PlayerPrefs.json", json);

        LoadFromJson();
    }

    public static void LoadFromJson()
    {
        string json = File.ReadAllText($"{Application.dataPath}/PlayerPrefs.json");
        PlayerPreferences playerPrefs = JsonUtility.FromJson<PlayerPreferences>(json);

        playerPreferences = playerPrefs;

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
