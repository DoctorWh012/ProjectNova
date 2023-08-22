using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Utilities
{
    public static float GetRandomPitch(float minShift, float maxShift)
    {
        return 1 + Random.Range(minShift, maxShift);
    }
}

public static class Keybinds
{
    // Interactions
    public static KeyCode pauseKey = KeyCode.Escape;
    public static KeyCode interactKey = KeyCode.E;

    // Movement
    public static KeyCode forwardKey = KeyCode.W;
    public static KeyCode backwardsKey = KeyCode.S;
    public static KeyCode rightKey = KeyCode.D;
    public static KeyCode leftKey = KeyCode.A;
    public static KeyCode jumpKey = KeyCode.Space;
    public static KeyCode crouchKey = KeyCode.LeftControl;
    public static KeyCode dashKey = KeyCode.LeftShift;

    // Shooting
    public static KeyCode fireBtn = KeyCode.Mouse0;
    public static KeyCode altFireBtn = KeyCode.Mouse1;
    public static KeyCode reloadKey = KeyCode.R;
    public static KeyCode primarySlotKey = KeyCode.Alpha1;
    public static KeyCode secondarySlotKey = KeyCode.Alpha2;
    public static KeyCode tertiarySlotKey = KeyCode.Alpha3;
}
