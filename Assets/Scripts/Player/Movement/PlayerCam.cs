using UnityEngine;
using System.IO;

public class PlayerCam : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Transform orientation;
    private float yRotation, xRotation;
    private float sensitivity;

    // Start is called before the first frame update
    void Start()
    {
        GetSensitivity();
    }

    // Update is called once per frame
    void Update()
    {
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
}
