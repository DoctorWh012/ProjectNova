using System;
using Riptide;
using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public bool focused { get; private set; } = true;
    public static UIManager Instance;
    [SerializeField] private Canvas gameUICanvas;
    [SerializeField] private GameObject settingsUI;

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
        settingsUI.SetActive(focused);
        focused = !focused;
        return;
    }
}
