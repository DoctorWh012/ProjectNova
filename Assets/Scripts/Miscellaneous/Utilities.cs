using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public static class Utilities
{
    private static System.Random rng = new System.Random();

    public static float GetRandomPitch(float minShift = -0.1f, float maxShift = 0.02f)
    {
        return 1 + Random.Range(minShift, maxShift);
    }

    public static void GetRandomPitch(this AudioSource source, float minShift = -0.1f, float maxShift = 0.02f)
    {
        source.pitch = 1 + Random.Range(minShift, maxShift);
    }

    public static float VolumeToDB(float v)
    {
        v = Mathf.Clamp(v, 0.0001f, 1f);
        return Mathf.Log10(v) * 20f;
    }

    public static void Shuffle<T>(this IList<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }
}

public struct MyResolution
{
    public int width;
    public int height;
}

