using UnityEngine;
using DG.Tweening;

public class PlayerCam : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Transform cameraPos;
    [SerializeField] private Camera cam;
    [SerializeField] private Transform cameraTilt;
    [SerializeField] private Transform orientation;

    private float yRotation, xRotation;
    private float sensitivity;

    private float desiredRotation;
    private int rotatedDirection;

    private void Awake()
    {
        SettingsManager.updatedPlayerPrefs += GetPreferences;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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
        cam.fieldOfView = SettingsManager.playerPreferences.cameraFov;
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

    public void TiltCamera(int direction)
    {
        if (direction == rotatedDirection) return;
        cameraTilt.DOLocalRotate(new Vector3(0, 0, -direction * 3), 0.5f);
        rotatedDirection = direction;
    }
}

