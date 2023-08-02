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
    public static KeyCode fireBtn = KeyCode.Mouse0;

    public static KeyCode reloadKey = KeyCode.R;
    public static KeyCode primarySlotKey = KeyCode.Alpha1;
    public static KeyCode secondarySlotKey = KeyCode.Alpha2;
    public static KeyCode tertiarySlotKey = KeyCode.Alpha3;
}
