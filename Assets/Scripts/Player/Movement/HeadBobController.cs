using UnityEngine;

public class HeadBobController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Transform cam;
    [SerializeField] private Transform gunCam;
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private MultiplayerController multiplayerController;

    [Header("Settings")]
    [SerializeField, Range(0, 0.1f)] private float stepAmplitude = 0.015f;
    [SerializeField, Range(0, 0.1f)] private float gunAmplitude = 0.005f;
    [SerializeField, Range(0, 30)] private float frequency = 10;

    [Header("Toggle")]
    [SerializeField] public bool envCamBob = true;
    [SerializeField] public bool gunCambob = true;

    private Vector3 startPos;
    private Vector3 gunStartPos;

    // Start is called before the first frame update
    void Start()
    {
        startPos = cam.localPosition;
        gunStartPos = gunCam.localPosition;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        CheckMotion();
        ResetPosition();
        ResetGunPosition();
        cam.LookAt(FocusTarget());
    }

    private void PlayMotion(Vector3 motion)
    {
        cam.localPosition += motion;
    }

    private void PlayGunMotion(Vector3 motion)
    {
        gunCam.localPosition += motion;
    }

    private void CheckMotion()
    {
        if (!UIManager.Instance.focused) return;
        if (!(Input.GetAxisRaw("Vertical") != 0 || Input.GetAxisRaw("Horizontal") != 0)) return;
        if (!multiplayerController.isGrounded) return;

        if (envCamBob)
            PlayMotion(FootStepMotion(stepAmplitude));
        if (gunCambob)
            PlayGunMotion(FootStepMotion(gunAmplitude));

    }

    private Vector3 FootStepMotion(float amplitude)
    {
        Vector3 pos = Vector3.zero;
        pos.y += Mathf.Sin(Time.time * frequency) * amplitude;
        pos.x += Mathf.Cos(Time.time * frequency / 2) * amplitude * 2;
        return pos;
    }

    private void ResetPosition()
    {
        if (cam.localPosition == startPos) return;
        cam.localPosition = Vector3.Lerp(cam.localPosition, startPos, 1 * Time.deltaTime);
    }

    private void ResetGunPosition()
    {
        if (gunCam.localPosition == gunStartPos) return;
        gunCam.localPosition = Vector3.Lerp(gunCam.localPosition, gunStartPos, 1 * Time.deltaTime);
    }

    public void InstantlyResetGunPos()
    {
        if (gunCam.localPosition == gunStartPos) return;
        gunCam.localPosition = gunStartPos;
    }


    private Vector3 FocusTarget()
    {
        Vector3 pos = new Vector3(transform.position.x, transform.position.y + cameraHolder.localPosition.y, transform.position.z);
        pos += cameraHolder.forward * 15;
        return pos;
    }
}
