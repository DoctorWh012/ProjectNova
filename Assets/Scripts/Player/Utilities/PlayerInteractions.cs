using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInteractions : MonoBehaviour
{
    public bool Focused { get; private set; }
    public bool Interacting { get; private set; }

    [Header("Components")]
    [SerializeField] private Player player;


    private void Update()
    {
        if (!player.IsLocal) return;
        GetInput();
    }

    private void GetInput()
    {
        Interacting = Input.GetKey(Keybinds.interactKey);
        if (Input.GetKeyDown(Keybinds.pauseKey)) PauseUnpause();
    }

    private void PauseUnpause()
    {
        if (Focused)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Focused = false;
        }

        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Focused = true;
        }
    }
}
