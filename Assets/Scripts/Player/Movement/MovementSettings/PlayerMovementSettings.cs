using System.Runtime.Remoting.Messaging;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerMovement", menuName = "RUSP/PlayerMovement", order = 0)]
public class PlayerMovementSettings : ScriptableObject
{
    //----MOVEMENT SETTINGS----
    [Header("Physics")]
    [Space(5)]
    [SerializeField] public float mass = 5f;
    [SerializeField] public float gravity = 25;

    [Header("Camera")]
    [SerializeField] public float cameraHeight;
    [SerializeField] public float crouchedCameraHeight;
    [SerializeField] public float cameraSideMovementTilt;
    [SerializeField] public float cameraWallRunTilt;

    [Header("Movement")]
    [Space(5)]
    [SerializeField] public float movementMultiplier = 10;
    [SerializeField] public float counterMovement = 0.2f;
    [SerializeField] public float excessSpeedCounterMovement = 0.05f;
    [SerializeField] public float moveSpeed = 13;
    [SerializeField] public float airMoveSpeed = 4;
    [SerializeField] public float maxSlopeAngle = 45;

    [Header("Jumping")]
    [Space(5)]
    [SerializeField] public AudioClip jumpSound;
    [Range(0, 1)]
    [SerializeField] public float jumpSoundVolume = 0.4f;
    [SerializeField] public float jumpForce = 15;
    [SerializeField] public float groundCheckHeight = 0.2f;
    [SerializeField] public float jumpCooldown = 0.3f;
    [SerializeField] public float coyoteTime = 0.2f;
    [SerializeField] public float jumpBufferTime = 0.1f;

    [Header("Wallruning")]
    [Space(5)]
    [SerializeField] public AudioClip wallrunJumpSound;
    [Range(0, 1)]
    [SerializeField] public float wallJumpSoundVolume = 0.4f;
    [SerializeField] public float wallRunMoveMultiplier = 1.5f;
    [SerializeField] public float wallDistance = 1;
    [SerializeField] public float wallStickForce = 50f;
    [SerializeField] public float wallStickUpMultiplier = 0.5f;
    [SerializeField] public float wallJumpUpForce = 10;
    [SerializeField] public float wallJumpSideForce = 10;
    [SerializeField] public float wallClimbSpeed = 3;
    [SerializeField] public float wallRunGravity = 5;

    [Header("Sliding")]
    [Space(5)]
    [SerializeField] public AudioClip slideSound;
    [Range(0, 1)]
    [SerializeField] public float slideSoundVolume = 0.4f;
    [SerializeField] public Vector3 crouchedGroundCheckPos;
    [SerializeField] public float slidingMovementMultiplier = 1.5f;
    [SerializeField] public float slidingCounterMovement = 0.2f;
    [SerializeField] public float crouchForce = 20f;
    [SerializeField] public float slideParticlesThreshold = 5f;

    [Header("Actions")]
    [Space(5)]
    [SerializeField] public float maxStamina = 3;
    [SerializeField] public float staminaRefillTime = 4;

    [Header("Ground Slam")]
    [Space(5)]
    [SerializeField] public float groundSlamStaminaCost = 1;
    [SerializeField] public AudioClip groundSlamSound;
    [Range(0, 1)]
    [SerializeField] public float groundSlamSoundVolume = 0.4f;
    [SerializeField] public float groundSlamImpulse = 65;
    [SerializeField] public float groundSlamGravity = 50f;

    [Header("Dashing")]
    [Space(5)]
    [SerializeField] public float slideStaminaCost = 1.5f;
    [SerializeField] public AudioClip dashSound;
    [Range(0, 1)]
    [SerializeField] public float dashSoundVolume = 0.4f;
    [SerializeField] public float dashDuration = 1f;
    [SerializeField] public float dashForce = 30f;
    [SerializeField] public float dashRefillTime = 2f;

    [HideInInspector] public float staminaRefillRate;

    private void Awake()
    {
        staminaRefillRate = maxStamina / staminaRefillTime;
    }
}