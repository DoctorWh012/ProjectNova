using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestingManager : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Camera cam;
    [SerializeField] private AudioListener audioListener;
    [SerializeField] private GameObject networkManager;
    [SerializeField] private GameObject steamManager;

    [Header("Settings")]
    [SerializeField] private KeyCode playKey;

    private void Awake()
    {
        if (NetworkManager.Singleton != null) { ActivateManagers(false); this.gameObject.SetActive(false); return; }
        ActivateManagers(true);
    }

    private void Update()
    {
        if (Input.GetKeyDown(playKey))
        {
            DisableTestingStuff();
            // LobbyManager.Singleton.CreateLobby();
        }
    }

    private void ActivateManagers(bool state)
    {
        networkManager.SetActive(state);
        steamManager.SetActive(state);
    }

    private void DisableTestingStuff()
    {
        cam.enabled = false;
        audioListener.enabled = false;
        gameObject.SetActive(false);
    }

}
