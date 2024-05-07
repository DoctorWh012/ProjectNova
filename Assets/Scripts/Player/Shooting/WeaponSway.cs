// Copyright 2024, MirageDev, All rights reserved.

using UnityEngine;

public class WeaponSway : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private PlayerMovement playerMovement;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float mouseMultiplier = 3f;

    [Header("Position")]
    [SerializeField] private float velocityMultiplier = 0.01f;
    [SerializeField] private float velocityMouseMultiplier = 0.01f;
    [SerializeField] private float velocitySpeed = 8f;

    private Vector3 initialPosition;

    private void Start()
    {
        initialPosition = transform.localPosition;
    }

    private void Update()
    {
        // Get mouse input
        float mouseX = Input.GetAxisRaw("Mouse X") * mouseMultiplier;
        float mouseY = Input.GetAxisRaw("Mouse Y") * mouseMultiplier;

        if (!GameManager.Focused)
        {
            mouseX = 0;
            mouseY = 0;
        }

        // Get player vel
        Vector3 vel = transform.InverseTransformVector(playerMovement.rb.velocity);

        // Create a quaternion based on input
        Quaternion rotationX = Quaternion.AngleAxis(-mouseY, Vector3.right);
        Quaternion rotationY = Quaternion.AngleAxis(mouseX, Vector3.up);
        Quaternion targetRotation = rotationX * rotationY;

        // Create a position offset based on player velocity
        Vector3 targetPosition = initialPosition - vel * velocityMultiplier - Vector3.right * mouseX * velocityMouseMultiplier - Vector3.up * mouseY * velocityMouseMultiplier;

        // Interpolate between the orientation and position
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, rotationSpeed * Time.deltaTime);
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, velocitySpeed * Time.deltaTime);
    }
}