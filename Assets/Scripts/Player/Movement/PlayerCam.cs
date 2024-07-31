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
    [SerializeField] private Transform cameraTilt;
    [SerializeField] private Transform orientation;

    private float yRotation;
    private float xRotation;
    private float sensitivity;

    private float desiredRotation;
    private int rotatedDirection;

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
        sensitivity = (state ? SettingsManager.playerPreferences.zoomSensitivity : SettingsManager.playerPreferences.sensitivity) / 10;
    }

    public void TiltCamera(int direction)
    {
        if (direction == rotatedDirection) return;
        cameraTilt.DOLocalRotate(new Vector3(0, 0, -direction * 3), 0.5f);
        rotatedDirection = direction;
    }
    #endregion
}

