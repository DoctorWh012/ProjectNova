using System.Runtime.Remoting.Messaging;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerMovement", menuName = "RUSP/PlayerMovement", order = 0)]
public class PlayerMovementSettings : ScriptableObject
{
    //----MOVEMENT SETTINGS----
    [Header("Physics")]
    [Space(5)]
    [SerializeField] public float mass = 5f;
    [SerializeField] public float groundedDrag = 5;
    [SerializeField] public float airDrag = 0;
    [SerializeField] public float gravity = 10;

    [Header("Movement")]
    [Space(5)]
    [SerializeField] public AudioClip footStepSounds;
    [Range(0, 1)]
    [SerializeField] public float footStepSoundVolume = 0.3f;
    [SerializeField] public float footStepStartVelocity;
    [SerializeField] public Vector3 groundCheckPos;
    [SerializeField] public float footStepRate = 2.5f;
    [SerializeField] public float movementMultiplier = 10;
    [SerializeField] public float moveSpeed = 13;
    [SerializeField] public float airMoveSpeed = 4;
    [SerializeField] public float maxSlopeAngle = 45;

    [Header("Jumping")]
    [Space(5)]
    [SerializeField] public AudioClip jumpAudioClip;
    [Range(0, 1)]
    [SerializeField] public float jumpSoundVolume = 0.4f;
    [SerializeField] public float jumpForce = 15;
    [SerializeField] public float groundCheckHeight = 0.2f;
    [SerializeField] public float jumpCooldown = 0.3f;
    [SerializeField] public float coyoteTime = 0.2f;
    [SerializeField] public float jumpBufferTime = 0.1f;

    [Header("Wallruning")]
    [Space(5)]
    [SerializeField] public AudioClip wallrunJumpAudioClip;
    [Range(0, 1)]
    [SerializeField] public float wallJumpSoundVolume = 0.4f;
    [SerializeField] public float wallDistance = 1;
    [SerializeField] public float wallStickForce = 50f;
    [SerializeField] public float wallStickUpMultiplier = 0.5f;
    [SerializeField] public float wallJumpUpForce = 10;
    [SerializeField] public float wallJumpSideForce = 10;
    [SerializeField] public float wallClimbSpeed = 3;
    [SerializeField] public float wallRunGravity = 5;

    [Header("Sliding")]
    [Space(5)]
    [SerializeField] public AudioClip slideAudioClip;
    [Range(0, 1)]
    [SerializeField] public float slideSoundVolume = 0.4f;
    [SerializeField] public Vector3 crouchedGroundCheckPos;
    [SerializeField] public float slidingMovementMultiplier = 1.5f;
    [SerializeField] public float crouchForce = 20f;
    [SerializeField] public float slideParticlesThreshold = 5f;

    [Header("Ground Slam")]
    [Space(5)]
    [SerializeField] public AudioClip groundSlamAudioClip;
    [Range(0, 1)]
    [SerializeField] public float groundSlamAudioVolume = 0.4f;
    [SerializeField] public AudioClip groundSlamRefilAudioClip;
    [Range(0, 1)]
    [SerializeField] public float groundSlamRefilAudioVolume = 0.4f;
    [SerializeField] public int groundSlamQuantity = 1;
    [SerializeField] public float groundSlamRefilTime = 3f;
    [SerializeField] public float groundSlamImpulse = 65;
    [SerializeField] public float groundSlamGravity = 50f;

    [Header("Dashing")]
    [Space(5)]
    [SerializeField] public AudioClip dashAudioClip;
    [Range(0, 1)]
    [SerializeField] public float dashAudioVolume = 0.4f;
    [SerializeField] public AudioClip dashRefilAudioClip;
    [Range(0, 1)]
    [SerializeField] public float dashRefilAudioVolume = 0.4f;
    [SerializeField] public int dashQuantity = 2;
    [SerializeField] public float dashDuration = 1f;
    [SerializeField] public float dashForce = 30f;
    [SerializeField] public float dashRefilTime = 2f;
}