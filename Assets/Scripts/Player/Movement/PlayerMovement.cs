using DG.Tweening;
using Riptide;
using UnityEngine;

public class SimulationState
{
    public Vector3 position = Vector3.zero;
    public Vector3 orientation = Vector3.zero;
    public Vector3 camRotation = Vector3.zero;
    public bool alive = true;
    public Transform[] characterColliderBones;
    public uint currentTick = 0;
}

public class PlayerMovement : MonoBehaviour
{
    public enum MovementStates
    {
        Active,
        Inactive,
        Sliding,
        Crouching,
        Wallrunning,
        GroundSlamming,
        Dashing
    }

    public enum PlayerAnimationsStates
    {
        Idle,
        RunForward,
        Jump,
        Land,
        Slide,
    }

    public MovementStates currentMovementState = MovementStates.Active; //{ get; private set; } = MovementStates.Active;
    public PlayerAnimationsStates currentAnimationState = PlayerAnimationsStates.Idle;
    public float coyoteTimeCounter { get; private set; }
    public float verticalInput { get; private set; }
    public float horizontalInput { get; private set; }

    [Header("Components")]
    [Space(5)]
    [SerializeField] private Player player;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerHud playerHud;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] public Rigidbody rb;

    [Header("Transforms")]
    [SerializeField] private Transform cameraPos;
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private Transform cameraTilt;

    [SerializeField] public Transform playerCharacter;
    [SerializeField] private Transform[] playerCharacterColliderBones;
    [SerializeField] private Transform orientation;

    [Header("Checkers")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Transform ceilingCheck;
    [SerializeField] private LayerMask groundLayer;

    [Header("Colliders")]
    [SerializeField] private GameObject standingCol;
    [SerializeField] private GameObject crouchedCol;

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
    [SerializeField] private float speedLineSpoolSpeed;
    [SerializeField] private ParticleSystem dashParticles;

    [Header("Audio")]
    [SerializeField] private AudioSource playerAudioSource;
    [SerializeField] private AudioSource slideAudioSouce;

    [Header("Debugging Serialized")]
    [SerializeField] bool movingDumb = false;

    // Movement Variables
    private float cameraSideMovementTiltDir;

    private float stamina;

    private float groundedMovementMultiplier;
    private float airMovementMultiplier;

    private float jumpBufferCounter;
    private bool readyToJump = true;

    public Vector3 moveDir;
    public Vector3 slideDir;
    private RaycastHit slopeHit;

    private bool onWallLeft;
    private bool onWallRight;

    private RaycastHit leftWallHit;
    private RaycastHit rightWallHit;

    Vector3 flatVel;
    private Vector3 wallNormal;
    private Vector3 wallForward;

    private uint lastReceivedCrouchTick;
    private uint lastReceivedGroundSlamTick;
    private uint lastReceivedDashTick;

    // Lag Compensation
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
        stamina = movementSettings.maxStamina;
    }

    private void Update()
    {
        if (playerHealth.currentPlayerState == PlayerState.Dead || currentMovementState == MovementStates.Inactive) return;
        CheckIfGrounded();
        RefillStamina();
        if (!player.IsLocal)
        {
            Interpolate();
            return;
        }

        CheckCameraTilt();
        SlideEffects(rb.velocity);
        UpdateSpeedLinesEmission();

        if (currentAnimationState == PlayerAnimationsStates.RunForward || currentAnimationState == PlayerAnimationsStates.Idle)
        {
            if (flatVel.magnitude < 4) SwitchAnimationState(PlayerAnimationsStates.Idle, 0.2f);
            else SwitchAnimationState(PlayerAnimationsStates.RunForward, 0.02f);
        }

        if (!GameManager.Focused)
        {
            verticalInput = 0;
            horizontalInput = 0;
            jumpBufferCounter = 0;
            EndCrouch();
            // if (!movingDumb) MoveDumb();
            return;
        }
        GetInput();
    }

    private void FixedUpdate()
    {
        if (!player.IsLocal || playerHealth.currentPlayerState == PlayerState.Dead || currentMovementState == MovementStates.Inactive) return;

        CheckWallRun();
        ApplyMovement();
        if (CanJump()) Jump();
        IncreaseGravity();

        if (NetworkManager.Singleton.Server.IsRunning) SendServerMovement(transform.position, rb.velocity, orientation.forward, cameraHolder.forward);
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
        if (Input.GetKey(SettingsManager.playerPreferences.crouchKey) && coyoteTimeCounter > 0 && jumpBufferCounter == 0) StartCrouch();
        if (Input.GetKeyDown(SettingsManager.playerPreferences.crouchKey) && coyoteTimeCounter == 0) GroundSlam();
        if (!Input.GetKey(SettingsManager.playerPreferences.crouchKey) && currentMovementState == MovementStates.Crouching) EndCrouch();

        // Dashing
        if (Input.GetKeyDown(SettingsManager.playerPreferences.dashKey)) Dash();
    }

    public void PlayerDied()
    {
        if (player.IsLocal) FreezePlayerMovement();
        else FreezeNetPlayerMovement();
    }

    public void PlayerRespawned()
    {
        if (player.IsLocal)
        {
            FreePlayerMovement();
            stamina = movementSettings.maxStamina;
        }
        else FreeNetPlayerMovement();
    }

    private void MoveDumb()
    {
        movingDumb = true;
        verticalInput = Random.Range(-1, 2);
        horizontalInput = Random.Range(-1, 2);

        jumpBufferCounter = Random.Range(0, 2) == 1 ? movementSettings.jumpBufferTime : 0;

        Invoke("RestoreMoveDumb", Random.Range(2, 5));
    }

    private void RestoreMoveDumb()
    {
        movingDumb = false;
    }

    private void SwitchMovementState(MovementStates state)
    {
        currentMovementState = state;
    }

    private void SwitchAnimationState(PlayerAnimationsStates state, float transitionDuration)
    {
        if (currentAnimationState == state) return;
        currentAnimationState = state;
        playerAnimator.CrossFade(state.ToString(), transitionDuration);
    }

    public void FreezeNetPlayerMovement()
    {
        if (currentMovementState == MovementStates.Inactive) return;
        currentMovementState = MovementStates.Inactive;

        interpolationGoal = new SimulationState();
        interpolationStartingState = new SimulationState();

        StopAllEffects();
    }

    public void FreezePlayerMovement()
    {
        if (currentMovementState == MovementStates.Inactive) return;

        if (currentMovementState == MovementStates.Sliding) EndCrouch();
        CancelInvoke("FinishDashing");
        CancelInvoke("RestoreJump");
        readyToJump = false;
        if (currentMovementState == MovementStates.Dashing) FinishDashing();
        if (currentMovementState == MovementStates.GroundSlamming) FinishGroundSlam();

        currentMovementState = MovementStates.Inactive;

        StopAllEffects();

        rb.velocity = Vector3.zero;
    }

    public void FreePlayerMovement()
    {
        if (currentMovementState != MovementStates.Inactive) return;
        currentMovementState = MovementStates.Active;

        Invoke("RestoreJump", movementSettings.jumpCooldown);
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

    #region Moving
    private void ApplyMovement()
    {
        if (currentMovementState == MovementStates.GroundSlamming || currentMovementState == MovementStates.Dashing) return;

        Vector3 trueForward = GetTrueForward();

        // Sticks The Player To The Wall When Wallrunning
        if (currentMovementState == MovementStates.Wallrunning && !(onWallLeft && horizontalInput > 0) && !(onWallRight && horizontalInput < 0) && readyToJump)
            rb.AddForce(-wallNormal * movementSettings.wallStickForce * movementSettings.mass, ForceMode.Force);

        // Counter Movement
        flatVel = new Vector3(rb.velocity.x, 0, rb.velocity.z);

        // if (OnSlope()) rb.AddForce(trueForward * movementSettings.mass * movementSettings.slopeCounterSlide);

        rb.AddForce(-flatVel * movementSettings.mass * movementSettings.counterMovement);

        // if (flatVel.magnitude > movementSettings.moveSpeed)
        // {
        //     rb.AddForce(-flatVel * (flatVel.magnitude - movementSettings.moveSpeed) * movementSettings.mass * movementSettings.excessSpeedCounterMovement);
        // }

        // Apply Movement Force
        float multiplier = currentMovementState == MovementStates.Sliding ? movementSettings.slidingMovementMultiplier : currentMovementState == MovementStates.Wallrunning ? movementSettings.wallRunMoveMultiplier : 1;

        moveDir = trueForward * verticalInput + orientation.right * horizontalInput;
        moveDir = moveDir.sqrMagnitude > 0 ? moveDir.normalized : moveDir;

        if (coyoteTimeCounter > 0) rb.AddForce(moveDir * groundedMovementMultiplier * multiplier, ForceMode.Force);
        else rb.AddForce(moveDir * airMovementMultiplier, ForceMode.Force);
        Debug.DrawRay(groundCheck.position, trueForward * 3, Color.magenta);
    }

    private void Jump()
    {
        readyToJump = false;
        Invoke("RestoreJump", movementSettings.jumpCooldown);

        rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);

        if (currentMovementState == MovementStates.Wallrunning)
        {
            rb.AddForce((transform.up * movementSettings.wallJumpUpForce * movementSettings.mass) + (wallNormal * movementSettings.wallJumpSideForce * movementSettings.mass), ForceMode.Impulse);
            playerAudioSource.pitch = Utilities.GetRandomPitch(-0.1f, 0.02f);
            playerAudioSource.PlayOneShot(movementSettings.wallrunJumpSound, movementSettings.jumpSoundVolume);
        }
        else
        {
            rb.AddForce(transform.up * movementSettings.jumpForce * movementSettings.mass, ForceMode.Impulse);
            playerAudioSource.pitch = Utilities.GetRandomPitch(-0.1f, 0.2f);
            playerAudioSource.PlayOneShot(movementSettings.jumpSound, movementSettings.wallJumpSoundVolume);
        }
    }

    private void IncreaseGravity()
    {
        if (OnSlope())
        {
            rb.AddForce(-slopeHit.normal * movementSettings.gravity * movementSettings.mass);
            return;
        }
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
    public void SaveSimulationState(uint index)
    {
        playerSimulationState[index] = CurrentSimulationState();
    }

    public SimulationState CurrentSimulationState()
    {
        return new SimulationState
        {
            position = transform.position,
            orientation = orientation.forward,
            camRotation = cameraHolder.forward,
            alive = playerHealth.currentPlayerState == PlayerState.Alive,
            characterColliderBones = playerCharacterColliderBones,
            currentTick = NetworkManager.Singleton.serverTick

        };
    }

    public void SetPlayerPositionToTick(uint tick)
    {
        uint cacheIndex = tick % NetworkManager.lagCompensationCacheSize;

        if (playerSimulationState[cacheIndex].currentTick != tick)
        {
            print($"<color=red>FAILED COMPENSATION TICK OUT OF BOUNDS WANTED {tick} IS {playerSimulationState[cacheIndex].currentTick}</color>");
            return;
        }

        for (int i = 0; i < playerCharacterColliderBones.Length; i++)
        {
            playerCharacterColliderBones[i].position = playerSimulationState[cacheIndex].characterColliderBones[i].position;
            playerCharacterColliderBones[i].rotation = playerSimulationState[cacheIndex].characterColliderBones[i].rotation;
        }

        // EditorApplication.isPaused = true;
    }

    public void ResetPlayerPosition()
    {
        // playerCharacter.localPosition = playerCharacterDefaultPos;
    }
    #endregion

    #region Actions
    private void RefillStamina()
    {
        if (currentMovementState == MovementStates.Sliding || stamina >= movementSettings.maxStamina) return;
        stamina += movementSettings.staminaRefillRate * Time.deltaTime;
        if (stamina > movementSettings.maxStamina) stamina = movementSettings.maxStamina;
        playerHud.UpdateStamina(stamina);
    }

    private void StartCrouch()
    {
        if (currentMovementState == MovementStates.Sliding || currentMovementState == MovementStates.Crouching || currentMovementState == MovementStates.Dashing) return;

        SwitchMovementState(MovementStates.Crouching);
        SwitchAnimationState(PlayerAnimationsStates.Slide, 0.05f);

        standingCol.SetActive(false);
        crouchedCol.SetActive(true);

        cameraPos.DOKill();
        cameraPos.DOLocalMoveY(movementSettings.crouchedCameraHeight, 0.1f).SetEase(Ease.OutQuad);

        if (NetworkManager.Singleton.Server.IsRunning) SendServerCrouch();
        else SendClientCrouch();
    }

    private void EndCrouch()
    {
        if (currentMovementState != MovementStates.Crouching) return;
        if (Physics.Raycast(ceilingCheck.position, Vector3.up, 1.1f, groundLayer)) return;

        SwitchMovementState(MovementStates.Active);
        SwitchAnimationState(PlayerAnimationsStates.Idle, 0.05f);

        standingCol.SetActive(true);
        crouchedCol.SetActive(false);

        cameraPos.DOKill();
        cameraPos.DOLocalMoveY(movementSettings.cameraHeight, 0.1f).SetEase(Ease.OutQuad);

        if (NetworkManager.Singleton.Server.IsRunning) SendServerCrouch();
        else SendClientCrouch();
    }

    private void HandleClientCrouch(bool state, uint tick)
    {
        if (tick <= lastReceivedCrouchTick) return;
        lastReceivedCrouchTick = tick;

        if (state)
        {
            SwitchMovementState(MovementStates.Sliding);
            SwitchAnimationState(PlayerAnimationsStates.Slide, 0.05f);

            standingCol.SetActive(false);
            crouchedCol.SetActive(true);
        }

        else
        {
            SwitchMovementState(MovementStates.Active);
            SwitchAnimationState(PlayerAnimationsStates.Idle, 0.05f);

            standingCol.SetActive(true);
            crouchedCol.SetActive(false);
        }

        if (NetworkManager.Singleton.Server.IsRunning) SendServerCrouch();
    }

    private void GroundSlam()
    {
        if (currentMovementState != MovementStates.Active) return;

        if (stamina < movementSettings.groundSlamStaminaCost) return;
        stamina -= movementSettings.groundSlamStaminaCost;
        playerHud.UpdateStamina(stamina);

        rb.velocity = Vector3.zero;
        rb.AddForce(Vector3.down * movementSettings.groundSlamImpulse * movementSettings.mass, ForceMode.Impulse);

        SwitchMovementState(MovementStates.GroundSlamming);
        groundSlamAirParticles.Play();

        if (NetworkManager.Singleton.Server.IsRunning) SendServerGroundSlam();
        else SendClientGroundSlam();
    }

    private void FinishGroundSlam()
    {
        if (currentMovementState != MovementStates.GroundSlamming) return;

        SwitchMovementState(MovementStates.Active);
        if (movementSettings.groundSlamSound)
        {
            playerAudioSource.pitch = Utilities.GetRandomPitch(-0.1f, 0.05f);
            playerAudioSource.PlayOneShot(movementSettings.groundSlamSound, movementSettings.groundSlamSoundVolume);
        }
        groundSlamAirParticles.Stop();
        groundSlamParticles.Play();

        if (NetworkManager.Singleton.Server.IsRunning) SendServerGroundSlam();
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
            if (movementSettings.groundSlamSound)
            {
                playerAudioSource.pitch = Utilities.GetRandomPitch(-0.1f, 0.05f);
                playerAudioSource.PlayOneShot(movementSettings.groundSlamSound, movementSettings.groundSlamSoundVolume);
            }
            groundSlamAirParticles.Stop();
            groundSlamParticles.Play();
        }

        if (NetworkManager.Singleton.Server.IsRunning) SendServerGroundSlam();
    }

    private void Dash()
    {
        if (currentMovementState == MovementStates.Inactive || currentMovementState == MovementStates.GroundSlamming) return;
        if (currentMovementState == MovementStates.Sliding) return;
        if (stamina < movementSettings.slideStaminaCost) return;

        stamina -= movementSettings.slideStaminaCost;
        playerHud.UpdateStamina(stamina);

        SwitchMovementState(MovementStates.Dashing);
        CancelInvoke("FinishDashing");
        Invoke("FinishDashing", movementSettings.dashDuration);

        Vector3 dashDir = horizontalInput == 0 && verticalInput == 0 ? GetTrueForward() : moveDir;
        rb.AddForce(dashDir * movementSettings.dashForce * movementSettings.mass, ForceMode.Impulse);

        dashParticles.transform.forward = dashDir;
        dashParticles.Play();

        if (movementSettings.dashSound)
        {
            playerAudioSource.pitch = Utilities.GetRandomPitch();
            playerAudioSource.PlayOneShot(movementSettings.dashSound, movementSettings.dashSoundVolume);
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

            if (movementSettings.dashSound)
            {
                playerAudioSource.pitch = Utilities.GetRandomPitch();
                playerAudioSource.PlayOneShot(movementSettings.dashSound, movementSettings.dashSoundVolume);
            }
        }
        else
        {
            SwitchMovementState(MovementStates.Active);
            dashParticles.Stop();
        }

        if (NetworkManager.Singleton.Server.IsRunning) SendServerDash();
    }
    #endregion

    #region Checks
    private void CheckIfGrounded()
    {
        Debug.DrawRay(groundCheck.position, Vector3.down * movementSettings.groundCheckHeight, Color.green);

        if (Physics.Raycast(groundCheck.position, Vector3.down, movementSettings.groundCheckHeight, groundLayer))
        {
            if (coyoteTimeCounter == 0) OnEnterGrounded();
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
        SwitchAnimationState(PlayerAnimationsStates.Land, 0);
        Invoke(nameof(PlayIdleAnimation), 0.5f);
        jumpParticles.Play();
        if (player.IsLocal) FinishGroundSlam();
    }

    private void OnStayGrounded()
    {

    }

    private void OnLeaveGrounded()
    {
        CancelInvoke(nameof(PlayIdleAnimation));
        SwitchAnimationState(PlayerAnimationsStates.Jump, 0);
        jumpParticles.Play();
        if (player.IsLocal) EndCrouch();
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
        onWallLeft = Physics.Raycast(orientation.position, -orientation.right, out leftWallHit, movementSettings.wallDistance, groundLayer);
        onWallRight = Physics.Raycast(orientation.position, orientation.right, out rightWallHit, movementSettings.wallDistance, groundLayer);

        bool pressingAgainstWall = onWallLeft && horizontalInput == -1 || onWallRight && horizontalInput == 1;

        if (pressingAgainstWall && coyoteTimeCounter == 0 && currentMovementState == MovementStates.Active)
        {
            SwitchMovementState(MovementStates.Wallrunning);
            rb.velocity = new Vector3(rb.velocity.x, rb.velocity.y * movementSettings.wallStickUpMultiplier, rb.velocity.z);
        }

        else if (currentMovementState == MovementStates.Wallrunning && !pressingAgainstWall) SwitchMovementState(MovementStates.Active);
    }
    #endregion

    #region Effects
    private void StopAllEffects()
    {
        // Stops Sliding Particles
        if (slideParticles.isEmitting)
        {
            slideParticles.Stop();
            slideAudioSouce.Stop();
        }

        groundSlamAirParticles.Stop();
        dashParticles.Stop();
    }

    private void SlideEffects(Vector3 velocity)
    {
        if (currentMovementState == MovementStates.Sliding && velocity.magnitude > movementSettings.slideParticlesThreshold && coyoteTimeCounter > 0f)
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
        slideAudioSouce.clip = movementSettings.slideSound;
        slideAudioSouce.volume = movementSettings.slideSoundVolume;
    }

    private void CheckCameraTilt()
    {
        if (currentMovementState == MovementStates.Wallrunning)
        {
            float desiredTilt = horizontalInput * movementSettings.cameraWallRunTilt;
            if (cameraSideMovementTiltDir == desiredTilt) return;
            cameraTilt.DOKill();
            cameraTilt.DOLocalRotate(new Vector3(0, 0, desiredTilt), 0.5f);
            cameraSideMovementTiltDir = desiredTilt;
        }
        else
        {
            float desiredTilt = horizontalInput * movementSettings.cameraSideMovementTilt;
            if (cameraSideMovementTiltDir == desiredTilt) return;
            cameraTilt.DOKill();
            cameraTilt.DOLocalRotate(new Vector3(0, 0, desiredTilt), 0.5f);
            cameraSideMovementTiltDir = desiredTilt;
        }
    }

    private void UpdateSpeedLinesEmission()
    {
        // float target = flatVel.magnitude > speedLineStartAtSpeed ? flatVel.magnitude * speedLineMultiplier : 0;
        // speedLineEmission.rateOverTime = Mathf.Lerp(speedLineEmission.rateOverTime.constant, target, speedLineSpoolSpeed * Time.deltaTime);
        // speedLinesEffect.do
    }

    private void PlayIdleAnimation()
    {
        SwitchAnimationState(PlayerAnimationsStates.Idle, 0.5f);
    }
    #endregion

    #region Interpolation
    private void HandleMovementData(Vector3 receivedPosition, Vector3 receivedVelocity, Vector3 receivedOrientation, Vector3 receivedCamForward, uint tick)
    {
        if (tick <= interpolationGoal.currentTick) return;
        if (currentMovementState == MovementStates.Inactive || playerHealth.currentPlayerState == PlayerState.Dead) return;

        timeSinceReceivedMovement = 0;

        interpolationStartingState = CurrentSimulationState();
        interpolationGoal = new SimulationState
        {
            position = receivedPosition,
            orientation = receivedOrientation,
            camRotation = receivedCamForward,
            currentTick = tick
        };

        flatVel = new Vector3(receivedVelocity.x, 0, receivedVelocity.z);
        moveDir = flatVel.normalized;
        if (currentAnimationState == PlayerAnimationsStates.RunForward || currentAnimationState == PlayerAnimationsStates.Idle)
        {
            if (flatVel.magnitude < 4) SwitchAnimationState(PlayerAnimationsStates.Idle, 0.2f);
            else SwitchAnimationState(PlayerAnimationsStates.RunForward, 0.02f);
        }

        SlideEffects(receivedVelocity);

        if (NetworkManager.Singleton.Server.IsRunning) SendServerMovement(receivedPosition, receivedVelocity, receivedOrientation, receivedCamForward);
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

        if (Vector3.Distance(interpolationStartingState.orientation, interpolationGoal.orientation) > interpolationThreshold)
        {
            orientation.forward = Vector3.Lerp(interpolationStartingState.orientation, interpolationGoal.orientation, interpolationAmount);
        }

        if (Vector3.Distance(interpolationStartingState.camRotation, interpolationGoal.camRotation) > interpolationThreshold)
        {
            cameraHolder.forward = Vector3.Lerp(interpolationStartingState.camRotation, interpolationGoal.camRotation, interpolationAmount);
        }

    }
    #endregion

    #region ServerSenders
    private void SendServerMovement(Vector3 position, Vector3 velocity, Vector3 orientation, Vector3 camForward)
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.playerMovement);
        message.AddUShort(player.Id);
        message.AddVector3(position);
        message.AddVector3(velocity);
        message.AddVector3(orientation);
        message.AddVector3(camForward);
        message.AddUInt(NetworkManager.Singleton.serverTick);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    private void SendServerCrouch()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.playerCrouch);
        message.AddUShort(player.Id);
        message.AddBool(currentMovementState == MovementStates.Sliding);
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
        message.AddVector3(cameraHolder.forward);
        message.AddUInt(NetworkManager.Singleton.serverTick);
        NetworkManager.Singleton.Client.Send(message);
    }

    private void SendClientCrouch()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ClientToServerId.playerCrouch);
        message.AddBool(currentMovementState == MovementStates.Sliding);
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
            player.playerMovement.HandleMovementData(message.GetVector3(), message.GetVector3(), message.GetVector3(), message.GetVector3(), message.GetUInt());
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

    [MessageHandler((ushort)ServerToClientId.playerMovementFreeze)]
    private static void GetPlayerMovementFreeze(Message message)
    {
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            if (player.IsLocal) player.playerMovement.FreezePlayerMovement();
            else player.playerMovement.FreezeNetPlayerMovement();
        }
    }

    [MessageHandler((ushort)ServerToClientId.playerMovementFree)]
    private static void GetPlayerMovementFree(Message message)
    {
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            if (player.IsLocal) player.playerMovement.FreePlayerMovement();
            else player.playerMovement.FreeNetPlayerMovement();
        }
    }
    #endregion

    #region  ClientToServerHandlers
    [MessageHandler((ushort)ClientToServerId.playerMovement)]
    private static void GetClientMovement(ushort fromClientId, Message message)
    {
        if (Player.list.TryGetValue(fromClientId, out Player player))
        {
            player.playerMovement.HandleMovementData(message.GetVector3(), message.GetVector3(), message.GetVector3(), message.GetVector3(), message.GetUInt());
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
