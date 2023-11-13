using Riptide;
using UnityEngine;

/* WORK REMINDER

    Implement Momentum
    Weapon Tilting is inefficient
    Network FootstepSound
    Dash Rotation Is Fucked

*/

public class SimulationState
{
    public Vector3 position = Vector3.zero;
    public Vector3 playerCharacterPos = Vector3.zero;
    public Vector3 rotation = Vector3.zero;
    public Vector3 camRotation = Vector3.zero;
    public uint currentTick = 0;
}

public class PlayerMovement : MonoBehaviour
{
    public enum MovementStates { Active, Inactive, Crouched, Wallrunning, GroundSlamming, Dashing }
    private enum AnimationId : byte { Idle = 0, RunForward, RunBackwards, RunLeft, RunRight, Crouch }

    [Header("Components")]
    [Space(5)]
    [SerializeField] private Player player;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerHud playerHud;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private CapsuleCollider col;
    [SerializeField] public Rigidbody rb;
    [SerializeField] private Transform playerCam;
    [SerializeField] private Transform playerCharacter;
    [SerializeField] private Transform orientation;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask wallLayer;

    [Header("Interpolation")]
    [SerializeField] private float interpolationThreshold;
    [SerializeField] private float timeToReachPos;

    [Header("Scriptables")]
    [SerializeField] private PlayerMovementSettings movementSettings;
    [SerializeField] private ScriptablePlayer scriptablePlayer;

    [Header("Particles")]
    [SerializeField] private ParticleSystem slideParticles;
    [SerializeField] private ParticleSystem jumpParticles;
    [SerializeField] private ParticleSystem groundSlamParticles;
    [SerializeField] private ParticleSystem groundSlamAirParticles;
    [SerializeField] private ParticleSystem speedLinesEffect;
    [SerializeField] private float speedLineStartAtSpeed;
    [SerializeField] private float speedLineMultiplier;
    [SerializeField] private float speedLineSpoolTime;
    [SerializeField] private ParticleSystem dashParticles;

    [Header("Audio")]
    [SerializeField] private AudioSource playerAudioSource;
    [SerializeField] private AudioSource slideAudioSouce;

    [Header("Debugging Serialized")]
    [SerializeField] public MovementStates currentMovementState = MovementStates.Active;
    [SerializeField] private AnimationId currentAnimationId = AnimationId.Idle;
    [SerializeField] private float coyoteTimeCounter;

    // Movement Variables
    private float verticalInput;
    private float horizontalInput;

    private int availableDashes;
    private float dashTimer;

    private int availableGroundSlams;
    private float groundSlamTimer;

    private float groundedMovementMultiplier;
    private float airMovementMultiplier;

    private float jumpBufferCounter;
    [SerializeField] private bool readyToJump = true;
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

    private bool isLerpingUp = false;
    private ParticleSystem.EmissionModule emission;
    private float lerpDuration = 0;

    private uint lastReceivedCrouchTick;
    private uint lastReceivedGroundSlamTick;
    private uint lastReceivedDashTick;

    // Lag Compensation
    private Vector3 playerCharacterDefaultPos = new Vector3(0, -1, 0);
    public SimulationState[] playerSimulationState = new SimulationState[NetworkManager.lagCompensationCacheSize];

    // Interpolation
    [SerializeField] private float timeSinceReceivedMovement;
    private SimulationState interpolationStartingState = new SimulationState();
    private SimulationState interpolationGoal = new SimulationState();

    private void Awake()
    {
        ApplyMass();
        GetMultipliers();
        SetupSlideAudioSource();
        GetSpecials();
    }

    private void Start()
    {
        if (player.IsLocal) emission = speedLinesEffect.emission;
    }

    private void Update()
    {
        CheckIfGrounded();
        if (playerHealth.currentPlayerState == PlayerState.Dead || currentMovementState == MovementStates.Inactive) return;

        if (!player.IsLocal)
        {
            Interpolate();
            return;
        }
        PlayerEffects();
        if (!PlayerHud.Focused) return;
        GetInput();
    }

    private void FixedUpdate()
    {
        if (!player.IsLocal || playerHealth.currentPlayerState == PlayerState.Dead || currentMovementState == MovementStates.Inactive) return;

        CheckWallRun();
        ApplyMovement(GetTrueForward());
        if (CanJump()) Jump();
        IncreaseGravity();
        ApplyDrag();
        if (NetworkManager.Singleton.Server.IsRunning) SendServerMovement(transform.position, rb.velocity, orientation.forward, playerCam.forward, (byte)currentAnimationId);
        else SendClientMovement();
    }


    private void GetInput()
    {
        // Desired Input
        verticalInput = Input.GetKey(SettingsManager.playerPreferences.forwardKey) ? 1 : (Input.GetKey(SettingsManager.playerPreferences.backwardKey) ? -1 : 0);
        horizontalInput = Input.GetKey(SettingsManager.playerPreferences.rightKey) ? 1 : (Input.GetKey(SettingsManager.playerPreferences.leftKey) ? -1 : 0);

        // Counter movement keys
        if (Input.GetKey(SettingsManager.playerPreferences.forwardKey) && Input.GetKey(SettingsManager.playerPreferences.backwardKey)) verticalInput = 0;
        if (Input.GetKey(SettingsManager.playerPreferences.rightKey) && Input.GetKey(SettingsManager.playerPreferences.leftKey)) horizontalInput = 0;

        // Jumping
        jumpBufferCounter = Input.GetKey(SettingsManager.playerPreferences.jumpKey) ? movementSettings.jumpBufferTime : jumpBufferCounter > 0 ? jumpBufferCounter - Time.deltaTime : 0;

        // Crouching / GroundSlam
        if (Input.GetKey(SettingsManager.playerPreferences.crouchKey) && (coyoteTimeCounter > 0) && jumpBufferCounter == 0) StartCrouch();
        if (Input.GetKeyDown(SettingsManager.playerPreferences.crouchKey) && coyoteTimeCounter == 0) GroundSlam();
        if (Input.GetKeyUp(SettingsManager.playerPreferences.crouchKey)) EndCrouch();

        // Dashing
        if (Input.GetKeyDown(SettingsManager.playerPreferences.dashKey)) Dash();
    }

    private void SwitchMovementState(MovementStates state)
    {
        currentMovementState = state;
    }

    public void FreezePlayerMovement()
    {
        if (currentMovementState == MovementStates.Inactive) return;

        if (currentMovementState == MovementStates.Crouched) EndCrouch();
        CancelInvoke("FinishDashing");
        CancelInvoke("RestoreJump");
        readyToJump = false;
        if (currentMovementState == MovementStates.Dashing) FinishDashing();
        if (currentMovementState == MovementStates.GroundSlamming) FinishGroundSlam();

        currentMovementState = MovementStates.Inactive;

        StopAllEffects();

        rb.useGravity = false;
        rb.velocity = Vector3.zero;
    }

    public void FreePlayerMovement()
    {
        if (currentMovementState != MovementStates.Inactive) return;
        currentMovementState = MovementStates.Active;

        Invoke("RestoreJump", movementSettings.jumpCooldown);
        rb.useGravity = true;
    }

    public void FreezeNetPlayerMovement()
    {
        if (currentMovementState == MovementStates.Inactive) return;
        currentMovementState = MovementStates.Inactive;

        interpolationGoal = new SimulationState();
        interpolationStartingState = new SimulationState();

        StopAllEffects();
    }

    public void FreeNetPlayerMovement()
    {
        if (currentMovementState != MovementStates.Inactive) return;
        currentMovementState = MovementStates.Active;
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

    private void GetSpecials()
    {
        availableDashes = movementSettings.dashQuantity;
        availableGroundSlams = movementSettings.groundSlamQuantity;
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
            position = transform.position,
            playerCharacterPos = playerCharacter.position,
            rotation = orientation.forward,
            camRotation = playerCam.forward,
            currentTick = NetworkManager.Singleton.serverTick
        };
    }

    public void SetPlayerPositionToTick(uint tick)
    {
        uint cacheIndex = tick % NetworkManager.lagCompensationCacheSize;

        if (playerSimulationState[cacheIndex].currentTick != tick) return;

        playerCharacter.position = playerSimulationState[cacheIndex].playerCharacterPos;
    }

    public void ResetPlayerPosition()
    {
        playerCharacter.localPosition = playerCharacterDefaultPos;
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
        if (NetworkManager.Singleton.Server.IsRunning) SendServerCrouch();
        else SendClientCrouch();
    }

    private void EndCrouch()
    {
        if (currentMovementState != MovementStates.Crouched) return;

        SwitchMovementState(MovementStates.Active);
        col.height = scriptablePlayer.playerHeight;
        groundCheck.localPosition = movementSettings.groundCheckPos;

        if (NetworkManager.Singleton.Server.IsRunning) SendServerCrouch();
        else SendClientCrouch();
    }

    private void HandleClientCrouch(bool state, uint tick)
    {
        if (tick <= lastReceivedCrouchTick) return;
        lastReceivedCrouchTick = tick;

        if (state)
        {
            SwitchMovementState(MovementStates.Crouched);
            col.height = scriptablePlayer.crouchedHeight;
            groundCheck.localPosition = movementSettings.crouchedGroundCheckPos;
        }
        else
        {
            SwitchMovementState(MovementStates.Active);
            col.height = scriptablePlayer.playerHeight;
            groundCheck.localPosition = movementSettings.groundCheckPos;
        }

        if (NetworkManager.Singleton.Server.IsRunning) SendServerCrouch();
    }

    private void GroundSlam()
    {
        if (currentMovementState != MovementStates.Active) return;

        if (availableGroundSlams <= 0) return;
        availableGroundSlams--;

        rb.velocity = Vector3.zero;
        rb.AddForce(Vector3.down * movementSettings.groundSlamImpulse * movementSettings.mass, ForceMode.Impulse);

        SwitchMovementState(MovementStates.GroundSlamming);
        groundSlamAirParticles.Play();

        if (player.IsLocal)
        {
            playerHud.UpdateGroundSlamIcon(false);
            playerHud.groundSlamSlider.value = 0;
            if (NetworkManager.Singleton.Server.IsRunning) SendServerGroundSlam();
        }
        else SendClientGroundSlam();
    }

    private void FinishGroundSlam()
    {
        if (currentMovementState != MovementStates.GroundSlamming) return;

        SwitchMovementState(MovementStates.Active);
        if (movementSettings.groundSlamAudioClip)
        {
            playerAudioSource.pitch = Utilities.GetRandomPitch(-0.1f, 0.05f);
            playerAudioSource.PlayOneShot(movementSettings.groundSlamAudioClip, movementSettings.groundSlamAudioVolume);
        }
        groundSlamAirParticles.Stop();
        groundSlamParticles.Play();

        if (NetworkManager.Singleton.Server.IsRunning && player.IsLocal) SendServerGroundSlam();
        else SendClientGroundSlam();
    }

    private void HandleClientGroundSlam(bool state, uint tick)
    {
        if (tick <= lastReceivedGroundSlamTick) return;
        lastReceivedGroundSlamTick = tick;

        if (state)
        {
            SwitchMovementState(MovementStates.GroundSlamming);
            groundSlamAirParticles.Play();
        }
        else
        {
            SwitchMovementState(MovementStates.Active);
            if (movementSettings.groundSlamAudioClip)
            {
                playerAudioSource.pitch = Utilities.GetRandomPitch(-0.1f, 0.05f);
                playerAudioSource.PlayOneShot(movementSettings.groundSlamAudioClip, movementSettings.groundSlamAudioVolume);
            }
            groundSlamAirParticles.Stop();
            groundSlamParticles.Play();
        }

        if (NetworkManager.Singleton.Server.IsRunning) SendServerGroundSlam();
    }

    private void RefilGroundSlam()
    {
        if (availableGroundSlams >= movementSettings.groundSlamQuantity) return;

        groundSlamTimer -= Time.deltaTime;
        playerHud.groundSlamSlider.value = (movementSettings.groundSlamRefilTime - groundSlamTimer) / movementSettings.groundSlamRefilTime;
        if (groundSlamTimer <= 0)
        {
            playerHud.UpdateGroundSlamIcon(true);
            availableGroundSlams++;
            groundSlamTimer = movementSettings.groundSlamRefilTime;
            playerAudioSource.pitch = Utilities.GetRandomPitch();
            playerAudioSource.PlayOneShot(movementSettings.groundSlamRefilAudioClip, movementSettings.groundSlamRefilAudioVolume);
        }
    }

    private void Dash()
    {
        if (currentMovementState == MovementStates.Inactive || currentMovementState == MovementStates.GroundSlamming) return;
        if (currentMovementState == MovementStates.Crouched) return;

        if (availableDashes <= 0) return;
        availableDashes--;
        playerHud.UpdateDashIcons(availableDashes);

        SwitchMovementState(MovementStates.Dashing);
        CancelInvoke("FinishDashing");
        Invoke("FinishDashing", movementSettings.dashDuration);

        rb.velocity = new Vector3(0, rb.velocity.y, 0);
        rb.AddForce(orientation.forward * movementSettings.dashForce * movementSettings.mass, ForceMode.Impulse);

        dashParticles.Play();

        if (movementSettings.dashAudioClip)
        {
            playerAudioSource.pitch = Utilities.GetRandomPitch();
            playerAudioSource.PlayOneShot(movementSettings.dashAudioClip, movementSettings.dashAudioVolume);
        }

        if (NetworkManager.Singleton.Server.IsRunning) SendServerDash();
        else SendClientDash();
    }

    private void FinishDashing()
    {
        SwitchMovementState(MovementStates.Active);
        dashParticles.Stop();
        if (NetworkManager.Singleton.Server.IsRunning) SendServerDash();
        else SendClientDash();
    }

    private void HandleClientDash(bool state, uint tick)
    {
        if (tick <= lastReceivedDashTick) return;
        lastReceivedDashTick = tick;

        if (state)
        {
            dashParticles.Play();

            if (movementSettings.dashAudioClip)
            {
                playerAudioSource.pitch = Utilities.GetRandomPitch();
                playerAudioSource.PlayOneShot(movementSettings.dashAudioClip, movementSettings.dashAudioVolume);
            }
        }
        else
        {
            SwitchMovementState(MovementStates.Active);
            dashParticles.Stop();
        }

        if (NetworkManager.Singleton.Server.IsRunning) SendServerDash();
    }

    private void RefilDash()
    {
        if (availableDashes >= movementSettings.dashQuantity) return;
        dashTimer -= Time.deltaTime;

        playerHud.dashSliders[availableDashes].value = (movementSettings.dashRefilTime - dashTimer) / movementSettings.dashRefilTime;
        if (dashTimer <= 0)
        {
            availableDashes++;
            playerHud.UpdateDashIcons(availableDashes);
            dashTimer = movementSettings.dashRefilTime;
            playerAudioSource.pitch = Utilities.GetRandomPitch(-0.05f, 0.1f);
            playerAudioSource.PlayOneShot(movementSettings.dashRefilAudioClip, movementSettings.dashRefilAudioVolume);
        }
    }
    #endregion

    #region Checks
    private void CheckIfGrounded()
    {
        if (Physics.Raycast(groundCheck.position, Vector3.down, movementSettings.groundCheckHeight, groundLayer))
        {
            if (coyoteTimeCounter != movementSettings.coyoteTime) OnEnterGrounded();
            else OnStayGrounded();

            coyoteTimeCounter = movementSettings.coyoteTime;
        }
        else
        {
            if (coyoteTimeCounter == 0) return;

            coyoteTimeCounter -= Time.deltaTime;
            if (coyoteTimeCounter <= 0)
            {
                OnLeaveGrounded();
                coyoteTimeCounter = 0;
            }
        }
    }

    private void OnEnterGrounded()
    {
        if (player.IsLocal)
        {
            FinishGroundSlam();
        }
    }

    private void OnStayGrounded()
    {
        if (player.IsLocal)
        {
            RefilDash();
            RefilGroundSlam();
        }
    }

    private void OnLeaveGrounded()
    {
        if (player.IsLocal)
        {
            EndCrouch();
            dashTimer = movementSettings.dashRefilTime;
            groundSlamTimer = movementSettings.groundSlamRefilTime;
            playerHud.UpdateDashIcons(availableDashes);
            playerHud.groundSlamSlider.value = availableGroundSlams;
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
    private void PlayerEffects()
    {
        AnimatePlayer();
        PlayFootStepSound();
        CheckCameraTilt();
        SlideEffects(rb.velocity);
        UpdateSpeedLinesEmission();
    }

    private void StopAllEffects()
    {
        // Stops Sliding Particles
        if (slideParticles.isEmitting)
        {
            slideParticles.Stop();
            slideAudioSouce.Stop();
        }
        if (player.IsLocal) emission.rateOverTime = 0;
    }

    private void SlideEffects(Vector3 velocity)
    {
        if (currentMovementState == MovementStates.Crouched && velocity.magnitude > movementSettings.slideParticlesThreshold && coyoteTimeCounter > 0f)
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
        if (rb.velocity.magnitude < movementSettings.footStepStartVelocity || footStepTimer > 0) return;


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
            if (currentMovementState == MovementStates.Wallrunning) { PlayerCam.Instance.TiltCamera(true, i, 10, 0.2f); }
            else { PlayerCam.Instance.TiltCamera(false, i, 10, 0.2f); }
        }
    }

    private void AnimatePlayer()
    {
        if (currentMovementState == MovementStates.Crouched) currentAnimationId = AnimationId.Crouch;

        else if (horizontalInput == 0 && verticalInput == 0) currentAnimationId = AnimationId.Idle;

        else if (verticalInput == 1) currentAnimationId = AnimationId.RunForward;
        else if (verticalInput == -1) currentAnimationId = AnimationId.RunBackwards;

        else if (horizontalInput == 1) currentAnimationId = AnimationId.RunRight;
        else if (horizontalInput == -1) currentAnimationId = AnimationId.RunLeft;

        PlayAnimationFromId(currentAnimationId);
    }

    private void PlayAnimationFromId(AnimationId id)
    {
        switch (id)
        {
            case AnimationId.Idle:
                playerAnimator.Play("Idle");
                break;
            case AnimationId.RunForward:
                playerAnimator.Play("Run");
                break;
            case AnimationId.RunBackwards:
                playerAnimator.Play("RunBackwards");
                break;
            case AnimationId.RunLeft:
                playerAnimator.Play("RunLeft");
                break;
            case AnimationId.RunRight:
                playerAnimator.Play("RunRight");
                break;
            case AnimationId.Crouch:
                playerAnimator.Play("Slide");
                break;
        }
    }

    private void UpdateSpeedLinesEmission()
    {
        float speed = rb.velocity.magnitude;

        if (speed < speedLineStartAtSpeed && emission.rateOverTimeMultiplier > 0)
        {
            if (isLerpingUp) { lerpDuration = 0; isLerpingUp = false; }
            emission.rateOverTime = Mathf.Lerp(emission.rateOverTimeMultiplier, 0, lerpDuration / speedLineSpoolTime);
            lerpDuration += Time.deltaTime;
        }

        else if (speed > speedLineStartAtSpeed)
        {
            if (!isLerpingUp) { lerpDuration = 0; isLerpingUp = true; }
            emission.rateOverTime = Mathf.Lerp(emission.rateOverTimeMultiplier, Mathf.Abs(speed * speedLineMultiplier), Time.deltaTime / (speedLineSpoolTime / 2));
            lerpDuration += Time.deltaTime;
        }
    }
    #endregion

    #region Interpolation
    private void HandleMovementData(Vector3 receivedPosition, Vector3 receivedVelocity, Vector3 receivedOrientation, Vector3 receivedCamForward, byte animationId, uint tick)
    {
        if (tick <= interpolationGoal.currentTick) return;
        if (currentMovementState == MovementStates.Inactive || playerHealth.currentPlayerState == PlayerState.Dead) return;

        timeSinceReceivedMovement = 0;

        interpolationStartingState = CurrentSimulationState();
        interpolationGoal = new SimulationState
        {
            position = receivedPosition,
            rotation = receivedOrientation,
            camRotation = receivedCamForward,
            currentTick = tick
        };

        PlayAnimationFromId((AnimationId)animationId);
        SlideEffects(receivedVelocity);

        if (NetworkManager.Singleton.Server.IsRunning) SendServerMovement(receivedPosition, receivedVelocity, receivedOrientation, receivedCamForward, animationId);
    }

    private void Interpolate()
    {
        if (interpolationGoal.currentTick == 0) return;
        if (currentMovementState == MovementStates.Inactive) return;

        timeSinceReceivedMovement += Time.deltaTime;
        float interpolationAmount = timeSinceReceivedMovement / timeToReachPos;

        if (Vector3.Distance(interpolationStartingState.position, interpolationGoal.position) > interpolationThreshold)
        {
            transform.position = Vector3.Lerp(interpolationStartingState.position, interpolationGoal.position, interpolationAmount);
        }

        if (Vector3.Distance(interpolationStartingState.rotation, interpolationGoal.rotation) > interpolationThreshold)
        {
            orientation.forward = Vector3.Lerp(interpolationStartingState.rotation, interpolationGoal.rotation, interpolationAmount);
        }

        if (Vector3.Distance(interpolationStartingState.camRotation, interpolationGoal.camRotation) > interpolationThreshold)
        {
            playerCam.forward = Vector3.Lerp(interpolationStartingState.camRotation, interpolationGoal.camRotation, interpolationAmount);
        }

    }
    #endregion

    #region ServerSenders
    private void SendServerMovement(Vector3 position, Vector3 velocity, Vector3 orientation, Vector3 camForward, byte animationId)
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.playerMovement);
        message.AddUShort(player.Id);
        message.AddVector3(position);
        message.AddVector3(velocity);
        message.AddVector3(orientation);
        message.AddVector3(camForward);
        message.AddByte(animationId);
        message.AddUInt(NetworkManager.Singleton.serverTick);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    private void SendServerCrouch()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.playerCrouch);
        message.AddUShort(player.Id);
        message.AddBool(currentMovementState == MovementStates.Crouched);
        message.AddUInt(NetworkManager.Singleton.serverTick);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    private void SendServerDash()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.playerDash);
        message.AddUShort(player.Id);
        message.AddBool(currentMovementState == MovementStates.Dashing);
        message.AddUInt(NetworkManager.Singleton.serverTick);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    private void SendServerGroundSlam()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.playerGroundSlam);
        message.AddUShort(player.Id);
        message.AddBool(currentMovementState == MovementStates.GroundSlamming);
        message.AddUInt(NetworkManager.Singleton.serverTick);
        NetworkManager.Singleton.Server.SendToAll(message);
    }
    #endregion

    #region ClientSenders
    private void SendClientMovement()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ClientToServerId.playerMovement);
        message.AddVector3(transform.position);
        message.AddVector3(rb.velocity);
        message.AddVector3(orientation.forward);
        message.AddVector3(playerCam.forward);
        message.AddByte((byte)currentAnimationId);
        message.AddUInt(NetworkManager.Singleton.serverTick);
        NetworkManager.Singleton.Client.Send(message);
    }

    private void SendClientCrouch()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ClientToServerId.playerCrouch);
        message.AddBool(currentMovementState == MovementStates.Crouched);
        message.AddUInt(NetworkManager.Singleton.serverTick);
        NetworkManager.Singleton.Client.Send(message);
    }

    private void SendClientDash()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ClientToServerId.playerDash);
        message.AddBool(currentMovementState == MovementStates.Dashing);
        message.AddUInt(NetworkManager.Singleton.serverTick);
        NetworkManager.Singleton.Client.Send(message);
    }

    private void SendClientGroundSlam()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ClientToServerId.playerGroundSlam);
        message.AddBool(currentMovementState == MovementStates.GroundSlamming);
        message.AddUInt(NetworkManager.Singleton.serverTick);
        NetworkManager.Singleton.Client.Send(message);
    }
    #endregion

    #region ServerToClientHandlers
    [MessageHandler((ushort)ServerToClientId.playerMovement)]
    private static void GetServerMovement(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            if (player.IsLocal) return;
            player.playerMovement.HandleMovementData(message.GetVector3(), message.GetVector3(), message.GetVector3(), message.GetVector3(), message.GetByte(), message.GetUInt());
        }
    }

    [MessageHandler((ushort)ServerToClientId.playerCrouch)]
    private static void GetServerCrouch(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            if (player.IsLocal) return;
            player.playerMovement.HandleClientCrouch(message.GetBool(), message.GetUInt());
        }
    }

    [MessageHandler((ushort)ServerToClientId.playerDash)]
    private static void GetServerDash(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            if (player.IsLocal) return;
            player.playerMovement.HandleClientDash(message.GetBool(), message.GetUInt());
        }
    }

    [MessageHandler((ushort)ServerToClientId.playerGroundSlam)]
    private static void GetServerGroundSlam(Message message)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            if (player.IsLocal) return;
            player.playerMovement.HandleClientGroundSlam(message.GetBool(), message.GetUInt());
        }
    }
    #endregion

    #region  ClientToServerHandlers
    [MessageHandler((ushort)ClientToServerId.playerMovement)]
    private static void GetClientMovement(ushort fromClientId, Message message)
    {
        if (Player.list.TryGetValue(fromClientId, out Player player))
        {
            player.playerMovement.HandleMovementData(message.GetVector3(), message.GetVector3(), message.GetVector3(), message.GetVector3(), message.GetByte(), message.GetUInt());
        }
    }

    [MessageHandler((ushort)ClientToServerId.playerCrouch)]
    private static void GetClientCrouch(ushort fromClientId, Message message)
    {
        if (Player.list.TryGetValue(fromClientId, out Player player))
        {
            player.playerMovement.HandleClientCrouch(message.GetBool(), message.GetUInt());
        }
    }

    [MessageHandler((ushort)ClientToServerId.playerGroundSlam)]
    private static void GetClientGroundSlam(ushort fromClientId, Message message)
    {
        if (Player.list.TryGetValue(fromClientId, out Player player))
        {
            player.playerMovement.HandleClientGroundSlam(message.GetBool(), message.GetUInt());
        }
    }

    [MessageHandler((ushort)ClientToServerId.playerDash)]
    private static void GetClientDash(ushort fromClientId, Message message)
    {
        if (Player.list.TryGetValue(fromClientId, out Player player))
        {
            player.playerMovement.HandleClientDash(message.GetBool(), message.GetUInt());
        }
    }
    #endregion
}
