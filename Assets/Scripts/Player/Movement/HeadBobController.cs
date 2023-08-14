using UnityEngine;

public class HeadBobController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private Transform mainCamera;
    [SerializeField] private Transform gunCamera;

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
        startPos = mainCamera.localPosition;
        gunStartPos = gunCamera.localPosition;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        CheckMotion();
        ResetPosition();
        ResetGunPosition();
        mainCamera.LookAt(FocusTarget());
    }

    private void PlayMotion(Vector3 motion)
    {
        mainCamera.localPosition += motion;
    }

    private void PlayGunMotion(Vector3 motion)
    {
        gunCamera.localPosition += motion;
    }

    private void CheckMotion()
    {
        if (!UIManager.Instance.focused) return;
        if (Input.GetAxisRaw("Vertical") == 0 && Input.GetAxisRaw("Horizontal") == 0) return;
        // if (!playerMovement.grounded || playerMovement.isCrouching) return;

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
        if (mainCamera.localPosition == startPos) return;
        mainCamera.localPosition = Vector3.Lerp(mainCamera.localPosition, startPos, 1 * Time.deltaTime);
    }

    private void ResetGunPosition()
    {
        if (gunCamera.localPosition == gunStartPos) return;
        gunCamera.localPosition = Vector3.Lerp(gunCamera.localPosition, gunStartPos, 1 * Time.deltaTime);
    }

    public void InstantlyResetGunPos()
    {
        if (gunCamera.localPosition == gunStartPos) return;
        gunCamera.localPosition = gunStartPos;
    }


    private Vector3 FocusTarget()
    {
        Vector3 pos = new Vector3(transform.position.x, transform.position.y + cameraHolder.localPosition.y, transform.position.z);
        pos += cameraHolder.forward * 15;
        return pos;
    }
}
