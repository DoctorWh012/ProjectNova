using System;
using Riptide;
using TMPro;
using UnityEngine;
using Steamworks;

public class UIManager : MonoBehaviour
{
    public bool focused { get; private set; } = true;
    public static UIManager Instance;
    [SerializeField] private Canvas gameUICanvas;
    [SerializeField] private GameObject menuUI;
    [SerializeField] private GameObject settingsUI;

    protected Callback<GameOverlayActivated_t> overlayActivated;

    private void OnEnable()
    {
        if (SteamManager.Initialized) overlayActivated = Callback<GameOverlayActivated_t>.Create(OnGameOverlayActivated);
    }

    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void InGameFocusUnfocus()
    {
        if (focused) Cursor.lockState = CursorLockMode.None;
        else Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = focused;
        gameUICanvas.enabled = !focused;
        menuUI.SetActive(focused);
        if (!focused) { settingsUI.SetActive(false); print("POTATO"); }
        focused = !focused;
        return;
    }

    private void OnGameOverlayActivated(GameOverlayActivated_t pCallback)
    {
        InGameFocusUnfocus();
    }

    public void ExitMatch()
    {
        MatchManager.Singleton.ExitMatch();
    }
}
