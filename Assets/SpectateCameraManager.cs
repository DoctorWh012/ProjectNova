using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using TMPro;

public class SpectateCameraManager : MonoBehaviour
{
    private static SpectateCameraManager _singleton;
    public static SpectateCameraManager Singleton
    {
        get { return _singleton; }
        set
        {
            if (_singleton == null)
            {
                _singleton = value;
            }

            else if (_singleton != value)
            {
                Debug.Log($"{nameof(SpectateCameraManager)} instance already exists, destroying duplicate");
                Destroy(value);
            }
        }
    }

    public static Transform activeCameraTransform;
    public static List<GameObject> availableCameras = new List<GameObject>();

    [Header("Components")]
    [SerializeField] public GameObject mapCamera;

    [Header("Menus")]
    [SerializeField] private GameObject deathScreen;
    [SerializeField] private TextMeshProUGUI deathRespawnTxt;

    public Transform playerCamera;
    public int atCamerasIndex = 0;
    public bool spectating;

    private void Awake()
    {
        Singleton = this;
    }

    private void Update()
    {
        if (!spectating) return;

        if (Input.GetKeyDown(SettingsManager.playerPreferences.fireBtn)) SpectateNext();
        if (Input.GetKeyDown(SettingsManager.playerPreferences.altFireBtn)) SpectatePrevious();
    }

    public void EnableDeathSpectateMode(ushort? id, float respawnTime)
    {
        deathScreen.SetActive(true);
        if (id != null) atCamerasIndex = GetCameraOfPlayer((ushort)id);
        StartCoroutine(RespawnTimer(respawnTime));

        spectating = true;
        mapCamera.SetActive(true);
        if (availableCameras.Count > atCamerasIndex) availableCameras[atCamerasIndex].SetActive(true);
        activeCameraTransform = mapCamera.transform;
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
        deathScreen.SetActive(false);
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

    private int GetCameraOfPlayer(ushort id)
    {
        for (int i = 0; i < availableCameras.Count; i++) if (availableCameras[i] == Player.list[id].netPlayerSpectatorCamBrain) return i;
        return 0;
    }

    private IEnumerator RespawnTimer(float time)
    {
        float timer = time;
        while (timer > 0)
        {
            timer -= Time.deltaTime;
            deathRespawnTxt.SetText(timer > 1 ? $"Respawning in [{timer.ToString("#")}] seconds" : "Respawning in [1] second");
            yield return null;
        }
    }
}
