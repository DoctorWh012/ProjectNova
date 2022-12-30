using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateObject : MonoBehaviour
{
    [SerializeField] Transform objectPos;
    [SerializeField] float rotateSpeed;
    [SerializeField] bool rotate = false;

    // Update is called once per frame
    void FixedUpdate()
    {
        if (!rotate) return;
        objectPos.Rotate(0, rotateSpeed, 0);
    }
}
