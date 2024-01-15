using UnityEngine;

public class Billboard : MonoBehaviour
{
    private void Update()
    {
        transform.LookAt(SpectateCameraManager.activeCameraTransform);
    }
}
