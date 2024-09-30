using UnityEngine;

public class RotateObject : MonoBehaviour
{

    [SerializeField] Transform objectPos;
    [SerializeField] float rotateSpeed;
    [SerializeField] bool rotate = true;
    [SerializeField] bool x;
    [SerializeField] bool y;
    [SerializeField] bool z;

    private void Start()
    {
        if (!objectPos) objectPos = transform;
    }

    void Update()
    {
        if (!rotate) return;
        if (x) objectPos.Rotate(rotateSpeed * Time.deltaTime, 0, 0);
        if (y) objectPos.Rotate(0, rotateSpeed * Time.deltaTime, 0);
        if (z) objectPos.Rotate(0, 0, rotateSpeed * Time.deltaTime);
    }
}
