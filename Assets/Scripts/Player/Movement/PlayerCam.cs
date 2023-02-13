using UnityEngine;
using System.Collections;
using System.IO;
using Riptide;

public class PlayerCam : MonoBehaviour
{
    public static PlayerCam Instance;

    public bool isTilted;//{ get; private set; }

    [Header("Components")]
    [SerializeField] private Transform orientation;
    [SerializeField] private Transform mainCamera;

    private float yRotation, xRotation;
    private float sensitivity;
    private float desiredRotation;
    private int rotatedDirection;

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        GetSensitivity();
    }

    void Update()
    {

    }

    private void FixedUpdate()
    {
        if (!UIManager.Instance.focused) return;
        GetInput();
        MoveCam();
    }

    public void GetSensitivity()
    {
        string json = File.ReadAllText($"{Application.dataPath}/PlayerPrefs.json");
        PlayerPreferences playerPrefs = JsonUtility.FromJson<PlayerPreferences>(json);
        sensitivity = playerPrefs.sensitivity * 10;
    }

    private void GetInput()
    {
        float mouseX = Input.GetAxisRaw("Mouse X") * Time.deltaTime * sensitivity;
        float mouseY = Input.GetAxisRaw("Mouse Y") * Time.deltaTime * sensitivity;

        yRotation += mouseX;
        xRotation -= mouseY;

        xRotation = Mathf.Clamp(xRotation, -89, 89);
    }

    private void MoveCam()
    {
        transform.rotation = Quaternion.Euler(xRotation, yRotation, 0);
        orientation.rotation = Quaternion.Euler(0, yRotation, 0);
    }

    public void TiltCamera(bool shouldTilt, int rotateDirection, float rotationAmount, float duration)
    {
        if (rotateDirection == 0) desiredRotation = -rotationAmount;
        else if (rotateDirection == 1) desiredRotation = rotationAmount;


        if (!isTilted && shouldTilt || rotatedDirection != rotateDirection)
        {
            StopAllCoroutines();
            StartCoroutine(LerpCameraRotation(desiredRotation, duration, true));
            rotatedDirection = rotateDirection;
        }
        if (!shouldTilt && isTilted)
        {
            StopAllCoroutines();
            StartCoroutine(LerpCameraRotation(0, duration, false));
        }
    }

    private IEnumerator LerpCameraRotation(float tiltAngle, float duration, bool untiltBeforeTilt)
    {
        Quaternion startingAngle = mainCamera.localRotation;
        Quaternion toAngle = Quaternion.Euler(new Vector3(0, 0, tiltAngle));
        float rotationDuration;

        if (untiltBeforeTilt)
        {
            isTilted = true;
            Quaternion zeroAngle = Quaternion.Euler(new Vector3(0, 0, 0));
            rotationDuration = 0;
            while (mainCamera.localRotation != zeroAngle)
            {
                mainCamera.localRotation = Quaternion.Lerp(startingAngle, zeroAngle, rotationDuration / 0.2f);
                rotationDuration += Time.deltaTime;
                yield return null;
            }
        }
        else { isTilted = !isTilted; }
        rotationDuration = 0;
        startingAngle = mainCamera.localRotation;

        while (mainCamera.localRotation != toAngle)
        {
            mainCamera.localRotation = Quaternion.Lerp(startingAngle, toAngle, rotationDuration / duration);
            rotationDuration += Time.deltaTime;
            yield return null;
        }
    }
}

