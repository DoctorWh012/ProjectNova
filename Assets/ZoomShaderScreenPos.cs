using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ZoomShaderScreenPos : MonoBehaviour
{
    [SerializeField] private Material mat;

    void Update()
    {
        Vector2 screenPixels = Camera.main.WorldToScreenPoint(transform.position);
        screenPixels = new Vector2(screenPixels.x / Screen.width, screenPixels.y / Screen.height);

        mat.SetVector("_ObjectScreenPos", screenPixels);
    }
}
