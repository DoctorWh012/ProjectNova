using System.Collections.Generic;
using UnityEngine;

public class SpectateCameraManager : MonoBehaviour
{
    public static SpectateCameraManager Instance;

    public static Transform activeCameraTransform;
    public static List<GameObject> availableCameras = new List<GameObject>();

    [Header("Components")]
    [SerializeField] private GameObject mapCamera;
    public Transform playerCamera;
    public int atCamerasIndex = 0;
    public bool spectating;

    private void Awake()
    {
        Instance = this;
        mapCamera.SetActive(true);
    }

    private void Update()
    {
        if (!spectating) return;

        if (Input.GetKeyDown(SettingsManager.playerPreferences.fireBtn)) SpectateNext();
        if (Input.GetKeyDown(SettingsManager.playerPreferences.altFireBtn)) SpectatePrevious();
    }

    public void EnableSpectateMode()
    {
        atCamerasIndex = 0;
        spectating = true;
        mapCamera.SetActive(true);
        if (availableCameras.Count > atCamerasIndex) availableCameras[atCamerasIndex].SetActive(true);
        activeCameraTransform = mapCamera.transform;
    }

    public void DisableSpectateMode()
    {
        spectating = false;
        mapCamera.SetActive(false);
        activeCameraTransform = playerCamera;
        if (availableCameras.Count > atCamerasIndex) availableCameras[atCamerasIndex].SetActive(false);
    }

    private void SpectateNext()
    {
        if (availableCameras.Count == 0) return;
        availableCameras[atCamerasIndex].SetActive(false);
        atCamerasIndex = (atCamerasIndex + 1) % (availableCameras.Count);
        availableCameras[atCamerasIndex].SetActive(true);
    }

    private void SpectatePrevious()
    {
        if (availableCameras.Count == 0) return;
        availableCameras[atCamerasIndex].SetActive(false);
        atCamerasIndex = (atCamerasIndex - 1 + availableCameras.Count) % (availableCameras.Count);
        availableCameras[atCamerasIndex].SetActive(true);
    }
}
