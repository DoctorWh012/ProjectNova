using UnityEngine;
using DG.Tweening;

public class PlayerCam : MonoBehaviour
{
    [Header("Components")]
    [Header("Cameras")]
    [Space(5)]
    [SerializeField] private Camera mainCam;
    [SerializeField] private Camera weaponCam;

    [Header("Transforms")]
    [Space(5)]
    [SerializeField] private Transform cameraPos;
    [SerializeField] private Transform orientation;

    private float yRotation;
    private float xRotation;
    private float sensitivity;

    private void Awake()
    {
        SettingsManager.updatedPlayerPrefs += GetPreferences;
    }

    private void OnDestroy()
    {
        SettingsManager.updatedPlayerPrefs -= GetPreferences;
    }

    private void OnApplicationQuit()
    {
        SettingsManager.updatedPlayerPrefs -= GetPreferences;
    }

    void Start()
    {
        GetPreferences();
    }

    private void Update()
    {
        transform.position = cameraPos.position;

        if (!GameManager.Focused) return;
        GetInput();
        MoveCam();
    }

    public void GetPreferences()
    {
        sensitivity = SettingsManager.playerPreferences.sensitivity / 10;
        mainCam.fieldOfView = SettingsManager.playerPreferences.cameraFov;
    }

    private void GetInput()
    {
        float mouseX = Input.GetAxisRaw("Mouse X") * sensitivity;
        float mouseY = Input.GetAxisRaw("Mouse Y") * sensitivity;

        yRotation += mouseX;
        xRotation -= mouseY;

        xRotation = Mathf.Clamp(xRotation, -89, 89);
    }

    private void MoveCam()
    {
        transform.rotation = Quaternion.Euler(xRotation, yRotation, 0);
        orientation.rotation = Quaternion.Euler(0, yRotation, 0);
    }

    #region Effects
    public void AlterZoomMode(bool state, int fov = 0)
    {
        mainCam.fieldOfView = state ? fov : SettingsManager.playerPreferences.cameraFov;
        weaponCam.enabled = !state;
        sensitivity = state ? SettingsManager.playerPreferences.zoomSensitivity : SettingsManager.playerPreferences.sensitivity;
        sensitivity /= 10;
    }
    #endregion
}

