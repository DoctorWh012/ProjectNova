using Riptide;
using UnityEditor;
using UnityEngine;


/* WORK REMINDER

    

*/

public class SimulationState
{
    public Vector3 position;
    public Vector3 rotation;
    public ushort currentTick;
}

public class PlayerMovement : MonoBehaviour
{
    public enum MovementStates { Active, Inactive, Crouched, Wallrunning, GroundSlamming, Dashing }

    [Header("Components")]
    [Space(5)]
    [SerializeField] private Player player;
    [SerializeField] private CapsuleCollider col;
    [SerializeField] public Rigidbody rb;
    [SerializeField] private Transform playerCharacter;
    [SerializeField] private Transform orientation;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask wallLayer;

    [Header("Scriptables")]
    [SerializeField] private PlayerMovementSettings movementSettings;
    [SerializeField] private ScriptablePlayer scriptablePlayer;

    [Header("Particles")]
    [SerializeField] private ParticleSystem slideParticles;
    [SerializeField] private ParticleSystem jumpParticles;
    [SerializeField] private ParticleSystem groundSlamParticles;

    [Header("Audio")]
    [SerializeField] private AudioSource playerAudioSource;
    [SerializeField] private AudioSource slideAudioSouce;

    [Header("Debugging Serialized")]
    [SerializeField] public MovementStates currentMovementState = MovementStates.Active;
    [SerializeField] private float coyoteTimeCounter;

    private float timer;

    // Movement Variables
    private float verticalInput;
    private float horizontalInput;

    private float groundedMovementMultiplier;
    private float airMovementMultiplier;

    private float jumpBufferCounter;
    private bool readyToJump = true;
    private Vector3 moveDir;
    private RaycastHit slopeHit;

    private float footStepTimer;

    private bool onWallLeft;
    private bool onWallRight;

    private RaycastHit leftWallHit;
    private RaycastHit rightWallHit;

    Vector3 flatVel;
    private Vector3 wallNormal;
    private Vector3 wallForward;

    // Lag Compensation
    public SimulationState[] playerSimulationState = new SimulationState[NetworkManager.lagCompensationCacheSize];
    private Vector3 savedPlayerPos;

    private void Awake()
    {
        ApplyMass();
        GetMultipliers();
        SetupSlideAudioSource();
    }

    private void Update()
    {
        CheckIfGrounded();
        PlayerAnimator();
        if (!player.IsLocal) return;

        GetInput();
    }

    private void FixedUpdate()
    {
        CheckWallRun();
        ApplyMovement(GetTrueForward());
        if (CanJump()) Jump();
        IncreaseGravity();
        ApplyDrag();
    }


    private void GetInput()
    {
        // Desired Input
        verticalInput = Input.GetKey(Keybinds.forwardKey) ? 1 : (Input.GetKey(Keybinds.backwardsKey) ? -1 : 0);
        horizontalInput = Input.GetKey(Keybinds.rightKey) ? 1 : (Input.GetKey(Keybinds.leftKey) ? -1 : 0);

        // Counter movement keys
        if (Input.GetKey(Keybinds.forwardKey) && Input.GetKey(Keybinds.backwardsKey)) verticalInput = 0;
        if (Input.GetKey(Keybinds.rightKey) && Input.GetKey(Keybinds.leftKey)) horizontalInput = 0;

        // Jumping
        jumpBufferCounter = Input.GetKey(Keybinds.jumpKey) ? movementSettings.jumpBufferTime : jumpBufferCounter > 0 ? jumpBufferCounter - Time.deltaTime : 0;

        // Crouching / GroundSlam
        if (Input.GetKey(Keybinds.crouchKey) && (coyoteTimeCounter > 0)) StartCrouch();
        if (Input.GetKeyDown(Keybinds.crouchKey) && coyoteTimeCounter == 0) GroundSlam();
        if (Input.GetKeyUp(Keybinds.crouchKey)) EndCrouch();

        // Dashing
        if (Input.GetKeyDown(Keybinds.dashKey)) Dash();
    }

    private void SwitchMovementState(MovementStates state)
    {
        currentMovementState = state;
    }

    private void ApplyMass()
    {
        rb.mass = movementSettings.mass;
    }

    private void GetMultipliers()
    {
        groundedMovementMultiplier = movementSettings.moveSpeed * movementSettings.movementMultiplier * movementSettings.mass;
        airMovementMultiplier = movementSettings.airMoveSpeed * movementSettings.movementMultiplier * movementSettings.mass;
    }

    #region Moving
    private void ApplyMovement(Vector3 trueForward)
    {
        if (currentMovementState == MovementStates.GroundSlamming || currentMovementState == MovementStates.Dashing) return;
        moveDir = trueForward * verticalInput + orientation.right * horizontalInput;

        if (coyoteTimeCounter > 0) rb.AddForce(moveDir * groundedMovementMultiplier, ForceMode.Force);
        else rb.AddForce(moveDir * airMovementMultiplier, ForceMode.Force);

        // Sticks The Player To The Wall When Wallrunning
        if (currentMovementState == MovementStates.Wallrunning && !(onWallLeft && horizontalInput > 0) && !(onWallRight && horizontalInput < 0) && readyToJump)
            rb.AddForce(-wallNormal * movementSettings.wallStickForce * movementSettings.mass, ForceMode.Force);

        // Speed Cap
        flatVel = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        float multiplier = currentMovementState != MovementStates.Crouched ? 1 : movementSettings.slidingMovementMultiplier;

        if (flatVel.magnitude > movementSettings.moveSpeed)
        {
            Vector3 limitedVel = flatVel.normalized * movementSettings.moveSpeed * multiplier;
            rb.velocity = new Vector3(limitedVel.x, rb.velocity.y, limitedVel.z);
        }
    }

    private void ApplyDrag()
    {
        if (coyoteTimeCounter > 0) rb.drag = movementSettings.groundedDrag;
        else rb.drag = movementSettings.airDrag;
    }

    private void Jump()
    {
        readyToJump = false;
        Invoke("RestoreJump", movementSettings.jumpCooldown);

        rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);

        if (currentMovementState == MovementStates.Wallrunning)
        {
            rb.AddForce(transform.up * movementSettings.wallJumpUpForce * movementSettings.mass + wallNormal * movementSettings.wallJumpSideForce * movementSettings.mass, ForceMode.Impulse);
            playerAudioSource.pitch = Utilities.GetRandomPitch(-0.1f, 0.02f);
            playerAudioSource.PlayOneShot(movementSettings.wallrunJumpAudioClip, movementSettings.jumpSoundVolume);
        }
        else
        {
            rb.AddForce(transform.up * movementSettings.jumpForce * movementSettings.mass, ForceMode.Impulse);
            playerAudioSource.pitch = Utilities.GetRandomPitch(-0.1f, 0.2f);
            playerAudioSource.PlayOneShot(movementSettings.jumpAudioClip, movementSettings.wallJumpSoundVolume);
        }
    }

    private void IncreaseGravity()
    {
        if (OnSlope())
        {
            rb.useGravity = false;
            return;
        }

        rb.useGravity = currentMovementState != MovementStates.Wallrunning;
        if (currentMovementState == MovementStates.Wallrunning) rb.AddForce(Vector3.down * movementSettings.wallRunGravity * movementSettings.mass);
        else if (currentMovementState == MovementStates.GroundSlamming) rb.AddForce(Vector3.down * movementSettings.groundSlamGravity * movementSettings.mass);
        else rb.AddForce(Vector3.down * movementSettings.gravity * movementSettings.mass);
    }

    private void RestoreJump()
    {
        readyToJump = true;
    }

    private Vector3 GetTrueForward()
    {
        if (OnSlope()) return GetSlopeMoveDirection();

        if (currentMovementState == MovementStates.Wallrunning)
        {
            wallNormal = onWallRight ? rightWallHit.normal : leftWallHit.normal;
            wallForward = Vector3.Cross(wallNormal, transform.up);
            if ((orientation.forward - wallForward).magnitude > (orientation.forward - -wallForward).magnitude) wallForward = -wallForward;

            return wallForward;
        }

        return orientation.forward;
    }
    #endregion

    #region LagCompensation
    public SimulationState CurrentSimulationState()
    {
        return new SimulationState
        {
            position = rb.position,
            rotation = orientation.forward,
            currentTick = NetworkManager.Singleton.serverTick
        };
    }

    public void SetPlayerPositionToTick(ushort tick)
    {
        savedPlayerPos = playerCharacter.position;
        int cacheIndex = tick % NetworkManager.lagCompensationCacheSize;

        if (playerSimulationState[cacheIndex].currentTick != tick) return;

        playerCharacter.position = playerSimulationState[cacheIndex].position;
    }

    public void ResetPlayerPosition()
    {
        playerCharacter.position = savedPlayerPos;
    }
    #endregion

    #region Actions
    private void StartCrouch()
    {
        if (currentMovementState == MovementStates.Crouched || currentMovementState == MovementStates.Dashing) return;

        SwitchMovementState(MovementStates.Crouched);
        col.height = scriptablePlayer.crouchedHeight;
        groundCheck.localPosition = movementSettings.crouchedGroundCheckPos;
        rb.AddForce(Vector3.down * movementSettings.crouchForce * movementSettings.mass, ForceMode.Impulse);
    }

    private void EndCrouch()
    {
        if (currentMovementState != MovementStates.Crouched) return;

        SwitchMovementState(MovementStates.Active);
        rb.position = new Vector3(rb.position.x, rb.position.y + 0.5f, rb.position.z);
        col.height = scriptablePlayer.playerHeight;
        groundCheck.localPosition = movementSettings.groundCheckPos;
    }

    private void GroundSlam()
    {
        if (currentMovementState != MovementStates.Active) return;

        SwitchMovementState(MovementStates.GroundSlamming);
        rb.velocity = Vector3.zero;
        rb.AddForce(Vector3.down * movementSettings.groundSlamImpulse * movementSettings.mass, ForceMode.Impulse);
    }

    private void Dash()
    {
        if (currentMovementState == MovementStates.Inactive || currentMovementState == MovementStates.GroundSlamming) return;
        if (currentMovementState == MovementStates.Crouched) return;
        SwitchMovementState(MovementStates.Dashing);
        Invoke("FinishDashing", movementSettings.dashDuration);

        rb.velocity = new Vector3(0, rb.velocity.y, 0);
        rb.AddForce(orientation.forward * movementSettings.dashForce * movementSettings.mass, ForceMode.Impulse);

        if (movementSettings.dashAudioClip)
        {
            playerAudioSource.pitch = Utilities.GetRandomPitch();
            playerAudioSource.PlayOneShot(movementSettings.dashAudioClip, movementSettings.dashAudioVolume);
        }
    }

    private void FinishDashing()
    {
        SwitchMovementState(MovementStates.Active);
    }
    #endregion

    #region Checks
    private void CheckIfGrounded()
    {
        coyoteTimeCounter = Physics.Raycast(groundCheck.position, Vector3.down, movementSettings.groundCheckHeight, groundLayer)
        ? movementSettings.coyoteTime : coyoteTimeCounter > 0 ? coyoteTimeCounter - Time.deltaTime : 0;

        if (coyoteTimeCounter == 0 && !readyToJump && currentMovementState == MovementStates.Crouched) EndCrouch();
        if (coyoteTimeCounter > 0 && currentMovementState == MovementStates.GroundSlamming)
        {
            SwitchMovementState(MovementStates.Active);
            if (movementSettings.groundSlamAudioClip)
            {
                playerAudioSource.pitch = Utilities.GetRandomPitch(-0.1f, 0.05f);
                playerAudioSource.PlayOneShot(movementSettings.groundSlamAudioClip, movementSettings.groundSlamAudioVolume);
            }
            groundSlamParticles.Play();
        }
    }

    private bool CanJump()
    {
        if (currentMovementState == MovementStates.Wallrunning) return jumpBufferCounter > 0 && readyToJump;
        return jumpBufferCounter > 0 && coyoteTimeCounter > 0 && readyToJump;
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

    private void CheckWallRun()
    {
        onWallLeft = Physics.Raycast(orientation.position, -orientation.right, out leftWallHit, movementSettings.wallDistance, wallLayer);
        onWallRight = Physics.Raycast(orientation.position, orientation.right, out rightWallHit, movementSettings.wallDistance, wallLayer);

        if ((onWallLeft || onWallRight) && coyoteTimeCounter == 0 && currentMovementState == MovementStates.Active)
        {
            SwitchMovementState(MovementStates.Wallrunning);
            rb.velocity = new Vector3(rb.velocity.x, rb.velocity.y * movementSettings.wallStickUpMultiplier, rb.velocity.z);
        }

        else if (currentMovementState == MovementStates.Wallrunning && !(onWallLeft || onWallRight)) SwitchMovementState(MovementStates.Active);
    }
    #endregion

    #region Effects
    private void PlayerAnimator()
    {
        PlayFootStepSound();
        CheckCameraTilt();
        SlideEffects();
    }

    private void SlideEffects()
    {
        if (currentMovementState == MovementStates.Crouched && rb.velocity.magnitude > movementSettings.slideParticlesThreshold && coyoteTimeCounter > 0f)
        {
            if (!slideParticles.isEmitting)
            {
                slideParticles.Play();
                slideAudioSouce.pitch = Utilities.GetRandomPitch(-0.1f, 0.02f);
                slideAudioSouce.Play();
            }
        }
        else if (slideParticles.isEmitting)
        {
            slideParticles.Stop();
            slideAudioSouce.Stop();
        }
    }

    private void SetupSlideAudioSource()
    {
        slideAudioSouce.loop = true;
        slideAudioSouce.clip = movementSettings.slideAudioClip;
        slideAudioSouce.volume = movementSettings.slideSoundVolume;
    }

    public void PlayFootStepSound()
    {
        footStepTimer -= Time.deltaTime;
        if (currentMovementState != MovementStates.Active && currentMovementState != MovementStates.Wallrunning) return;
        if (currentMovementState == MovementStates.Active && coyoteTimeCounter == 0) return;
        if (rb.velocity.magnitude < 5 || footStepTimer > 0) return;


        playerAudioSource.pitch = Utilities.GetRandomPitch(-0.15f, 0.15f);
        playerAudioSource.PlayOneShot(movementSettings.footStepSounds, movementSettings.footStepSoundVolume);
        footStepTimer = 1f / movementSettings.footStepRate;
    }

    private void CheckCameraTilt()
    {
        if (coyoteTimeCounter > 0)
        {
            switch (horizontalInput)
            {
                case 1:
                    PlayerCam.Instance.TiltCamera(true, 0, 2, 0.3f);
                    break;
                case -1:
                    PlayerCam.Instance.TiltCamera(true, 1, 2, 0.3f);
                    break;
                case 0:
                    PlayerCam.Instance.TiltCamera(false, 0, 2, 0.2f);
                    break;
            }
        }
        else
        {
            int i = onWallLeft ? 0 : 1;
            if (currentMovementState == MovementStates.Wallrunning) { PlayerCam.Instance.TiltCamera(true, i, 15, 0.2f); }
            else { PlayerCam.Instance.TiltCamera(false, i, 15, 0.2f); }
        }
    }
    #endregion
}
