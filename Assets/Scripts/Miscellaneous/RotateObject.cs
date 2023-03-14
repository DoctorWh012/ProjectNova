using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateObject : MonoBehaviour
{

    [SerializeField] Transform objectPos;
    [SerializeField] float rotateSpeed;
    [SerializeField] bool rotate = false;
    [SerializeField] bool x;
    [SerializeField] bool y;
    [SerializeField] bool z;

    // Update is called once per frame
    void FixedUpdate()
    {
        if (!rotate) return;
        if (x) objectPos.Rotate(rotateSpeed, 0, 0);
        if (y) objectPos.Rotate(0, rotateSpeed, 0);
        if (z) objectPos.Rotate(0, 0, rotateSpeed);
    }
}
