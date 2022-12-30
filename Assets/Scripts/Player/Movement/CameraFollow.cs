using UnityEngine;

public class CameraFollow : MonoBehaviour

{
    [SerializeField] public Transform cameraPos;

    // Update is called once per frame
    private void Update()
    {
        transform.position = cameraPos.position;
    }
}

