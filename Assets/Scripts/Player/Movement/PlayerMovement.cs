using Riptide;
using UnityEngine;

public class SimulationState
{
    public Vector3 position;
    public Vector3 rotation;
    public ushort currentTick;
}

public class PlayerMovement : MonoBehaviour
{
    private enum MovementStates { Idle, Moving }

    [Header("Components")]
    [Space(5)]
    [SerializeField] private Player player;
    [SerializeField] private PlayerMovementSettings movementSettings;
    [SerializeField] public Rigidbody rb;
    [SerializeField] private Transform orientation;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;


    [Header("Debugging Serialized")]
    [SerializeField] private MovementStates currentMovementState = MovementStates.Idle;

    private float timer;

    // Movement Variables
    [SerializeField] private float verticalInput;
    [SerializeField] private float horizontalInput;
    [SerializeField] private float verticalMultiplier;
    [SerializeField] private float horizontalMultiplier;
    [SerializeField] private float verticalTimer;
    [SerializeField] private float horizontalTimer;
    [SerializeField] private float decelerationTimer;

    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private bool readyToJump = true;
    private bool grounded;
    private Vector3 moveDir;
    private Vector3 trueForward;
    private RaycastHit slopeHit;

    private void Awake()
    {
        rb.drag = movementSettings.drag;
    }

    private void Update()
    {
        CheckIfGrounded();
        if (player.IsLocal) GetInput();

        timer += Time.deltaTime;
        while (timer >= NetworkManager.Singleton.minTimeBetweenTicks)
        {
            timer -= NetworkManager.Singleton.minTimeBetweenTicks;
            MovementTick();
        }
    }

    private void MovementTick()
    {
        switch (currentMovementState)
        {
            case MovementStates.Moving:
                ApplyMovement(orientation.forward);
                break;
            case MovementStates.Idle:
                ApplyCounterMovement();
                break;

        }

        if (jumpBufferCounter > 0 && coyoteTimeCounter > 0 && readyToJump) Jump();

        SpeedCap();
        IncreaseFallGravity(movementSettings.gravity);
    }

    private void GetInput()
    {
        // Movement
        verticalInput = Input.GetKey(Keybinds.forwardKey) ? 1 : (Input.GetKey(Keybinds.backwardsKey) ? -1 : 0);
        horizontalInput = Input.GetKey(Keybinds.rightKey) ? 1 : (Input.GetKey(Keybinds.leftKey) ? -1 : 0);

        // Counter movement keys
        if (Input.GetKey(Keybinds.forwardKey) && Input.GetKey(Keybinds.backwardsKey)) verticalInput = 0;
        if (Input.GetKey(Keybinds.rightKey) && Input.GetKey(Keybinds.leftKey)) horizontalInput = 0;

        // Timer
        if (Input.GetKeyDown(Keybinds.forwardKey) || Input.GetKeyDown(Keybinds.backwardsKey)) verticalTimer = 0;
        else verticalTimer += Time.deltaTime;

        if (Input.GetKeyDown(Keybinds.rightKey) || Input.GetKeyDown(Keybinds.leftKey)) horizontalTimer = 0;
        else horizontalTimer += Time.deltaTime;

        // Multiplier
        if (grounded) verticalMultiplier = GetMovementMultiplier(verticalMultiplier, verticalInput, verticalTimer);
        else verticalMultiplier = GetMovementMultiplier(verticalMultiplier, verticalInput, verticalTimer * movementSettings.airAccelerationMultiplier);

        if (grounded) horizontalMultiplier = GetMovementMultiplier(horizontalMultiplier, horizontalInput, horizontalTimer);
        else horizontalMultiplier = GetMovementMultiplier(horizontalMultiplier, horizontalInput, horizontalTimer * movementSettings.airAccelerationMultiplier);

        // State
        if (verticalInput != 0 || horizontalInput != 0)
        {
            decelerationTimer = 0;
            currentMovementState = MovementStates.Moving;
        }
        else
        {
            decelerationTimer += Time.deltaTime;
            currentMovementState = MovementStates.Idle;
        }

        // Jumping
        jumpBufferCounter = Input.GetKey(Keybinds.jumpKey) ? movementSettings.jumpBufferTime : jumpBufferCounter > 0 ? jumpBufferCounter - Time.deltaTime : 0;
    }

    private float GetMovementMultiplier(float multiplier, float input, float timer)
    {
        return Mathf.Lerp(multiplier, input, movementSettings.accelerationCurve.Evaluate(timer));
    }

    #region Moving
    private void ApplyMovement(Vector3 trueForward)
    {
        moveDir = trueForward * verticalMultiplier + orientation.right * horizontalMultiplier;
        moveDir *= movementSettings.moveSpeed;
        rb.velocity = new Vector3(moveDir.x, rb.velocity.y, moveDir.z);
    }

    private void ApplyCounterMovement()
    {
        
        if (grounded) moveDir = rb.velocity * movementSettings.decelerationCurve.Evaluate(decelerationTimer);
        else moveDir = rb.velocity * movementSettings.decelerationCurve.Evaluate(decelerationTimer * movementSettings.airDecelerationMultiplier);

        rb.velocity = new Vector3(moveDir.x, rb.velocity.y, moveDir.z);
    }

    private void Jump()
    {
        readyToJump = false;
        Invoke("RestoreJump", movementSettings.jumpCooldown);
        rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        rb.AddForce(transform.up * movementSettings.jumpForce, ForceMode.Impulse);
    }

    private void SpeedCap()
    {
        Vector3 flatVel = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        // if (currentMovementState != MovementStates.Sliding)
        // {
        if (flatVel.magnitude > movementSettings.moveSpeed)
        {
            Vector3 limitedVel = flatVel.normalized * movementSettings.moveSpeed;
            rb.velocity = new Vector3(limitedVel.x, rb.velocity.y, limitedVel.z);
        }
        // }
        // else
        // {
        // if (flatVel.magnitude > movementSettings.moveSpeed)
        // {
        //     Vector3 limitedVel = flatVel.normalized * (movementSettings.moveSpeed * movementSettings.crouchedSpeedMultiplier);
        //     rb.velocity = new Vector3(limitedVel.x, rb.velocity.y, limitedVel.z);
        // }
        // }
    }

    private void IncreaseFallGravity(float force)
    {
        rb.AddForce(Vector3.down * force);
    }

    private void RestoreJump()
    {
        readyToJump = true;
    }

    #endregion

    #region Checks
    private void CheckIfGrounded()
    {
        grounded = Physics.Raycast(groundCheck.position, Vector3.down, movementSettings.groundCheckHeight, groundLayer);
        coyoteTimeCounter = grounded ? movementSettings.coyoteTime : coyoteTimeCounter > 0 ? coyoteTimeCounter - Time.deltaTime : 0;

    }

    private bool OnSlope()
    {
        if (Physics.Raycast(groundCheck.position, Vector3.down, out slopeHit, movementSettings.groundCheckHeight, groundLayer))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle < movementSettings.maxSlopeAngle && angle != 0;
        }
        return false;
    }

    private Vector3 GetSlopeMoveDirection()
    {
        return Vector3.ProjectOnPlane(orientation.forward, slopeHit.normal).normalized;
    }
    #endregion
}
