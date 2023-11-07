using UnityEngine;

public static class Utilities
{
    public static float GetRandomPitch(float minShift = -0.1f, float maxShift = 0.02f)
    {
        return 1 + Random.Range(minShift, maxShift);
    }

    public static float VolumeToDB(float v)
    {
        v = Mathf.Clamp(v, 0.0001f, 1f);
        return Mathf.Log10(v) * 20f;
    }
}

public struct MyResolution
{
    public int width;
    public int height;
}