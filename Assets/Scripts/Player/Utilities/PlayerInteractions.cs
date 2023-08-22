using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInteractions : MonoBehaviour
{
    public bool interacting { get; private set; }

    private void Update()
    {
        interacting = Input.GetKey(Keybinds.interactKey);
    }
}
