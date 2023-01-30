using UnityEngine;

[CreateAssetMenu(fileName = "PlayerMovement", menuName = "RUSP/PlayerMovement", order = 0)]
public class PlayerMovementSettings : ScriptableObject
{
    //----MOVEMENT SETTINGS----
    [Space]
    [Header("Movement Settings")]
    [SerializeField] public float moveSpeed = 13;
    [SerializeField] public float crouchedSpeedMultiplier = 1.2f;
    [SerializeField] public float groundDrag = 5;
    [SerializeField] public float airDrag = 0;
    [SerializeField] public float jumpForce = 15;
    [SerializeField] public float wallJumpUpForce = 10;
    [SerializeField] public float wallJumpSideForce = 10;
    [SerializeField] public float wallClimbSpeed = 3;
    [SerializeField] public float maxSlopeAngle = 45;
    [SerializeField] public float gravity = 10;
    [SerializeField] public float wallRunGravity = 5;

    //----OTHER SETTINGS----
    [Space]
    [Header("Other Settings")]
    [SerializeField] public float groundCheckHeight = 0.2f;
    [SerializeField] public float jumpCooldown = 0.3f;
    [SerializeField] public float airMultiplier = 4;
    [SerializeField] public float wallDistance = 1;

}