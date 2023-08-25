using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInteractions : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Player player;
    
    public bool interacting { get; private set; }

    private void Update()
    {
        if (!player.IsLocal) return;
        interacting = Input.GetKey(Keybinds.interactKey);
    }
}
