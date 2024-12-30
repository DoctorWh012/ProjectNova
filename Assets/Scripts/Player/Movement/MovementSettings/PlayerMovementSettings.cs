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
    [SerializeField] public float landCameraOffset;
    [SerializeField] public float landCameraOffsetTime;
    [SerializeField] public float speedFovOffset;
    [SerializeField] public float speedFovOffsetSpeed;
    [SerializeField] public float speedLinesRate;
    [SerializeField] public float speedLinesRateSpeed;
    [SerializeField] public float consideredFastMagnitude;
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
    [SerializeField, Range(0, 1)] public float jumpSoundVolume = 0.4f;
    [SerializeField] public float jumpForce = 15;
    [SerializeField] public float groundCheckHeight = 0.2f;
    [SerializeField] public float jumpCooldown = 0.3f;
    [SerializeField] public float coyoteTime = 0.2f;
    [SerializeField] public float jumpBufferTime = 0.1f;

    [Header("Wallruning")]
    [Space(5)]
    [SerializeField] public AudioClip wallrunJumpSound;
    [SerializeField, Range(0, 1)] public float wallJumpSoundVolume = 0.4f;
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
    [SerializeField] public float slideWallBlockDistance;
    [SerializeField] public AudioClip slideSound;
    [SerializeField, Range(0, 1)] public float slideSoundVolume = 0.4f;
    [SerializeField] public float slidingMovementMultiplier = 1.5f;
    [SerializeField] public float slidingCounterMovement = 0.2f;

    [Header("Actions")]
    [Space(5)]
    [SerializeField] public float maxStamina = 3;
    [SerializeField] public float staminaRefillTime = 4;

    [Header("Ground Slam")]
    [Space(5)]
    [SerializeField] public float groundSlamStaminaCost = 1;
    [SerializeField] public AudioClip groundSlamFallingSound;
    [SerializeField, Range(0, 1)] public float groundSlamFallingSoundVolume = 0.4f;
    [SerializeField] public AudioClip groundSlamSound;
    [SerializeField, Range(0, 1)] public float groundSlamSoundVolume = 0.4f;
    [SerializeField] public float groundSlamImpulse = 65;
    [SerializeField] public float groundSlamGravity = 50f;

    [Header("Dashing")]
    [Space(5)]
    [SerializeField] public float slideStaminaCost = 1.5f;
    [SerializeField] public AudioClip dashSound;
    [SerializeField, Range(0, 1)] public float dashSoundVolume = 0.4f;
    [SerializeField] public float dashDuration = 1f;
    [SerializeField] public float dashForce = 30f;
    [SerializeField] public float dashRefillTime = 2f;

    [Header("Animations")]
    [SerializeField] public float runAnimationStartSpeed;

    [HideInInspector] public float staminaRefillRate;

    private void Awake()
    {
        staminaRefillRate = maxStamina / staminaRefillTime;
    }
}