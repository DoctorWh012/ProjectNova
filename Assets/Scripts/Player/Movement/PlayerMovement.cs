using UnityEngine;
using Riptide;
using DG.Tweening;
using UnityEditor;
using System.Linq;

public class SimulationState : IMessageSerializable
{
    public Vector3 position;
    public Vector3 velocity;
    public Vector3 orientationForward;
    public Vector3 cameraHolderForward;
    public bool alive;
    public Vector3[] characterColliderBonesPos;
    public Quaternion[] characterColliderBonesForward;
    public uint currentTick;

    public void Deserialize(Message message)
    {
        position = message.GetVector3();
        velocity = message.GetVector3();
        orientationForward = message.GetVector3();
        cameraHolderForward = message.GetVector3();
        alive = message.GetBool();
        characterColliderBonesPos = message.GetVector3s();
        characterColliderBonesForward = message.GetQuaternions();
        currentTick = message.GetUInt();
    }

    public void Serialize(Message message)
    {
        message.AddVector3(position);
        message.AddVector3(velocity);
        message.AddVector3(orientationForward);
        message.AddVector3(cameraHolderForward);
        message.AddBool(alive);
        message.AddVector3s(characterColliderBonesPos);
        message.AddQuaternions(characterColliderBonesForward);
        message.AddUInt(currentTick);
    }
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
    [SerializeField] private Camera localPlayerCam;
    [SerializeField] private Player player;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerHud localPlayerHud;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] public Rigidbody rb;

    [Header("Transforms")]
    [SerializeField] private Transform localPlayerCameraPos;
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private Transform localPlayerCameraTilt;

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
    [SerializeField] private Transform slideParticlesPivot;
    [SerializeField] private ParticleSystem slideParticles;
    [SerializeField] private ParticleSystem jumpParticles;
    [SerializeField] private ParticleSystem groundSlamParticles;
    [SerializeField] private ParticleSystem groundSlamAirParticles;
    [SerializeField] private ParticleSystem localPlayerSpeedLinesEffect;
    [SerializeField] private ParticleSystem dashParticles;

    [Header("Audio")]
    [SerializeField] private AudioSource playerAudioSource;
    [SerializeField] private AudioSource slideAudioSouce;
    [SerializeField] private AudioSource groundSlamAudioSource;

    [Header("Debuggin")]
    [SerializeField] private Material transparentGreen;
    [SerializeField] private Material transparentRed;
    [SerializeField] private bool wannaMoveDumb;

    // Movement Variables
    bool movingDumb = false;
    Vector3 netPlayerVelocity;
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
        SetupAudioSources();
        stamina = movementSettings.maxStamina;
    }

    private void Update()
    {
        if (playerHealth.currentPlayerState == PlayerState.Dead || currentMovementState == MovementStates.Inactive) return;
        CheckIfGrounded();
        AnimatePlayer();
        if (!player.IsLocal) { Interpolate(); return; }
        RefillStamina();
        CameraEffects();
        if (!GameManager.Focused) { ZeroInput(); return; }
        GetInput();
    }

    private void FixedUpdate()
    {
        if (!player.IsLocal || playerHealth.currentPlayerState == PlayerState.Dead || currentMovementState == MovementStates.Inactive) return;
        CheckWallRun();
        ApplyMovement();
        if (CanJump()) Jump();
        IncreaseGravity();

        if (NetworkManager.Singleton.Server.IsRunning) SendServerMovement(CurrentSimulationState());
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
        if (Input.GetKeyDown(SettingsManager.playerPreferences.crouchKey) && coyoteTimeCounter > 0 && jumpBufferCounter == 0) StartCrouch();
        if (Input.GetKeyDown(SettingsManager.playerPreferences.crouchKey) && coyoteTimeCounter == 0) GroundSlam();
        if (!Input.GetKey(SettingsManager.playerPreferences.crouchKey) && (currentMovementState == MovementStates.Crouching || currentMovementState == MovementStates.Sliding)) EndCrouch();

        // Dashing
        if (Input.GetKeyDown(SettingsManager.playerPreferences.dashKey)) Dash();
    }

    private void ZeroInput()
    {
        if (wannaMoveDumb)
        {
            if (!movingDumb) MoveDumb();
            return;
        }
        verticalInput = 0;
        horizontalInput = 0;
        jumpBufferCounter = 0;
        EndCrouch();
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
            localPlayerHud.UpdateStamina(stamina);
        }
        else FreeNetPlayerMovement();
    }

    private void MoveDumb()
    {
        print("SHIT");
        movingDumb = true;
        verticalInput = Random.Range(-1, 2);
        horizontalInput = Random.Range(-1, 2);

        jumpBufferCounter = Random.Range(0, 2) == 1 ? movementSettings.jumpBufferTime : 0;

        Invoke(nameof(RestoreMoveDumb), Random.Range(2, 5));
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
        playerAnimator.CrossFade(state.ToString(), transitionDuration, 0);
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

        currentMovementState = MovementStates.Inactive;
        CancelInvoke("FinishDashing");
        CancelInvoke("RestoreJump");
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
        if (currentMovementState == MovementStates.Wallrunning && (onWallLeft && horizontalInput < 0) && (onWallRight && horizontalInput > 0) && readyToJump)
            rb.AddForce(-wallNormal * movementSettings.wallStickForce * movementSettings.mass, ForceMode.Force);

        // Counter Movement
        flatVel = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        rb.AddForce(-flatVel * movementSettings.mass * movementSettings.counterMovement);

        // if (flatVel.magnitude > movementSettings.moveSpeed)
        //     rb.AddForce(-flatVel * (flatVel.magnitude - movementSettings.moveSpeed) * movementSettings.mass * movementSettings.excessSpeedCounterMovement);

        // Apply Movement Force
        float multiplier = currentMovementState == MovementStates.Sliding ? movementSettings.slidingMovementMultiplier : currentMovementState == MovementStates.Wallrunning ? movementSettings.wallRunMoveMultiplier : 1;

        if (currentMovementState != MovementStates.Sliding)
        {
            moveDir = trueForward * verticalInput + orientation.right * horizontalInput;
            moveDir = moveDir.sqrMagnitude > 0 ? moveDir.normalized : moveDir;
        }

        else
        {
            if (Physics.Raycast(orientation.position, slideDir, movementSettings.slideWallBlockDistance, groundLayer)) EndCrouch();
            else moveDir = trueForward;
        }

        if (coyoteTimeCounter > 0) rb.AddForce(moveDir * groundedMovementMultiplier * multiplier, ForceMode.Force);
        else rb.AddForce(moveDir * airMovementMultiplier, ForceMode.Force);
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
            jumpParticles.Play();
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
        if (currentMovementState == MovementStates.Wallrunning)
        {
            wallNormal = onWallRight ? rightWallHit.normal : leftWallHit.normal;
            wallForward = Vector3.Cross(wallNormal, transform.up);
            if ((orientation.forward - wallForward).magnitude > (orientation.forward - -wallForward).magnitude) wallForward = -wallForward;

            return wallForward;
        }

        Vector3 forward = currentMovementState != MovementStates.Sliding ? orientation.forward : slideDir;
        if (OnSlope()) return GetSlopeMoveDirection(forward);
        return forward;
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
            velocity = player.IsLocal ? rb.velocity : netPlayerVelocity,
            orientationForward = orientation.forward,
            cameraHolderForward = cameraHolder.forward,
            alive = playerHealth.currentPlayerState == PlayerState.Alive,
            characterColliderBonesPos = playerCharacterColliderBones.Select(a => a.position).ToArray(),
            characterColliderBonesForward = playerCharacterColliderBones.Select(a => a.rotation).ToArray(),
            currentTick = NetworkManager.Singleton.serverTick
        };
    }

    public void SetPlayerPositionToTick(uint tick)
    {
        SpawnGhost(true);
        uint cacheIndex = tick % NetworkManager.lagCompensationCacheSize;

        if (playerSimulationState[cacheIndex].currentTick != tick)
        {
            print($"<color=red>FAILED COMPENSATION TICK OUT OF BOUNDS WANTED {tick} IS {playerSimulationState[cacheIndex].currentTick}</color>");
            return;
        }

        for (int i = 0; i < playerCharacterColliderBones.Length; i++)
        {
            playerCharacterColliderBones[i].position = playerSimulationState[cacheIndex].characterColliderBonesPos[i];
            playerCharacterColliderBones[i].rotation = playerSimulationState[cacheIndex].characterColliderBonesForward[i];
        }

        SpawnGhost(false);

        // EditorApplication.isPaused = true;
    }

    public void ResetPlayerPosition()
    {
        // playerCharacter.localPosition = playerCharacterDefaultPos;
    }

    private void SpawnGhost(bool pre)
    {
        Animator ghostAnimator = Instantiate(playerAnimator);
        foreach (Transform tr in ghostAnimator.GetComponentsInChildren<Transform>()) tr.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        ghostAnimator.transform.position = playerAnimator.transform.position;
        ghostAnimator.transform.rotation = playerAnimator.transform.rotation;
        ghostAnimator.enabled = false;

        foreach (SkinnedMeshRenderer renderer in ghostAnimator.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            renderer.material = pre ? transparentGreen : transparentRed;
        }
    }
    #endregion

    #region Actions
    private void RefillStamina()
    {
        if (currentMovementState == MovementStates.Sliding || stamina >= movementSettings.maxStamina) return;
        stamina += movementSettings.staminaRefillRate * Time.deltaTime;
        if (stamina > movementSettings.maxStamina) stamina = movementSettings.maxStamina;
        localPlayerHud.UpdateStamina(stamina);
    }

    private void StartCrouch()
    {
        if (currentMovementState == MovementStates.Sliding || currentMovementState == MovementStates.Dashing) return;

        SwitchMovementState(MovementStates.Sliding);

        slideDir = orientation.forward;
        slideParticlesPivot.forward = slideDir;
        slideParticles.Play();
        slideAudioSouce.GetRandomPitch();
        slideAudioSouce.Play();

        standingCol.SetActive(false);
        crouchedCol.SetActive(true);

        localPlayerCameraPos.DOKill();
        localPlayerCameraPos.DOLocalMoveY(movementSettings.crouchedCameraHeight, 0.1f).SetEase(Ease.OutQuad);

        if (NetworkManager.Singleton.Server.IsRunning) SendServerCrouch();
        else SendClientCrouch();
    }

    private void EndCrouch()
    {
        if (currentMovementState == MovementStates.Sliding) SwitchMovementState(MovementStates.Crouching);
        slideParticles.Stop();
        slideAudioSouce.Stop();
        if (currentMovementState != MovementStates.Crouching) return;
        if (Physics.Raycast(ceilingCheck.position, Vector3.up, 1.1f, groundLayer)) return;

        SwitchMovementState(MovementStates.Active);

        standingCol.SetActive(true);
        crouchedCol.SetActive(false);

        localPlayerCameraPos.DOKill();
        localPlayerCameraPos.DOLocalMoveY(movementSettings.cameraHeight, 0.1f).SetEase(Ease.OutQuad);

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

            standingCol.SetActive(false);
            crouchedCol.SetActive(true);
        }

        else
        {
            SwitchMovementState(MovementStates.Active);

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
        localPlayerHud.UpdateStamina(stamina);

        groundSlamAudioSource.GetRandomPitch();
        groundSlamAudioSource.Play();

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

        groundSlamAudioSource.Stop();
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
        localPlayerHud.UpdateStamina(stamina);

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
        localPlayerCameraPos.DOPunchPosition(new Vector3(0, -movementSettings.landCameraOffset), movementSettings.landCameraOffsetTime, 0, 0).SetEase(Ease.OutQuad);
        jumpParticles.Play();
        if (player.IsLocal) FinishGroundSlam();
    }

    private void OnStayGrounded()
    {

    }

    private void OnLeaveGrounded()
    {
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

    private Vector3 GetSlopeMoveDirection(Vector3 forward)
    {
        return Vector3.ProjectOnPlane(forward, slopeHit.normal).normalized;
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
    private void AnimatePlayer()
    {
        Vector3 velocity = player.IsLocal ? rb.velocity : netPlayerVelocity;
        switch (currentMovementState)
        {
            case MovementStates.Active:
                if (coyoteTimeCounter == 0) { SwitchAnimationState(PlayerAnimationsStates.Jump, 0); break; }
                if (velocity.magnitude < movementSettings.runAnimationStartSpeed) SwitchAnimationState(PlayerAnimationsStates.Idle, 0.01f);
                else SwitchAnimationState(PlayerAnimationsStates.RunForward, 0.01f);
                break;
            case MovementStates.Sliding:
                SwitchAnimationState(PlayerAnimationsStates.Slide, 0.03f);
                break;
        }
    }

    private void StopAllEffects()
    {
        // Stops Sliding Particles
        slideParticles.Stop();
        slideAudioSouce.Stop();

        groundSlamAirParticles.Stop();
        groundSlamAudioSource.Stop();

        dashParticles.Stop();
    }

    private void SetupAudioSources()
    {
        slideAudioSouce.loop = true;
        slideAudioSouce.clip = movementSettings.slideSound;
        slideAudioSouce.volume = movementSettings.slideSoundVolume;

        groundSlamAudioSource.loop = true;
        groundSlamAudioSource.clip = movementSettings.groundSlamFallingSound;
        groundSlamAudioSource.volume = movementSettings.groundSlamSoundVolume;
    }

    private void CameraEffects()
    {
        // Camera Tilt
        CheckCameraTilt();

        // Fov Shift
        Vector2 flatVel = new Vector2(rb.velocity.x, rb.velocity.z);
        float magnitudeMultiplier = flatVel.magnitude / movementSettings.consideredFastMagnitude;
        localPlayerCam.fieldOfView = Mathf.Lerp(localPlayerCam.fieldOfView, SettingsManager.playerPreferences.cameraFov + movementSettings.speedFovOffset * magnitudeMultiplier, movementSettings.speedFovOffsetSpeed * Time.deltaTime);

        // Speed Lines
        ParticleSystem.EmissionModule psEmission = localPlayerSpeedLinesEffect.emission;
        psEmission.rateOverTime = Mathf.Lerp(localPlayerSpeedLinesEffect.emission.rateOverTime.constant, movementSettings.speedLinesRate * magnitudeMultiplier, movementSettings.speedLinesRateSpeed * Time.deltaTime);
    }

    private void CheckCameraTilt()
    {
        float desiredTilt;
        if (currentMovementState == MovementStates.Wallrunning) desiredTilt = horizontalInput * movementSettings.cameraWallRunTilt;
        else desiredTilt = -horizontalInput * movementSettings.cameraSideMovementTilt;
        if (cameraSideMovementTiltDir == desiredTilt) return;
        localPlayerCameraTilt.DOKill();
        localPlayerCameraTilt.DOLocalRotate(new Vector3(0, 0, desiredTilt), 0.5f);
        cameraSideMovementTiltDir = desiredTilt;
    }
    #endregion

    #region Interpolation
    private void HandleMovementData(SimulationState simulationState)
    {
        if (simulationState.currentTick <= interpolationGoal.currentTick) return;
        if (currentMovementState == MovementStates.Inactive || playerHealth.currentPlayerState == PlayerState.Dead) return;

        netPlayerVelocity = simulationState.velocity;
        moveDir = simulationState.velocity.normalized;
        cameraHolder.forward = simulationState.cameraHolderForward;
        orientation.forward = simulationState.orientationForward;

        timeSinceReceivedMovement = 0;
        interpolationStartingState = CurrentSimulationState();
        interpolationGoal = simulationState;

        if (NetworkManager.Singleton.Server.IsRunning) SendServerMovement(simulationState);
    }

    private void Interpolate()
    {
        if (currentMovementState == MovementStates.Inactive || playerHealth.currentPlayerState == PlayerState.Dead) return;

        timeSinceReceivedMovement += Time.deltaTime;
        float interpolationAmount = timeSinceReceivedMovement / timeToReachPos;

        transform.position = Vector3.Lerp(interpolationStartingState.position, interpolationGoal.position, interpolationAmount);
    }
    #endregion

    #region ServerSenders
    private void SendServerMovement(SimulationState simulationState)
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.playerMovement);
        message.AddUShort(player.Id);
        message.AddSerializable(simulationState);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    private void SendServerCrouch()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.playerCrouch);
        message.AddUShort(player.Id);
        message.AddBool(currentMovementState == MovementStates.Sliding || currentMovementState == MovementStates.Crouching);
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
        message.AddSerializable(CurrentSimulationState());
        NetworkManager.Singleton.Client.Send(message);
    }

    private void SendClientCrouch()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ClientToServerId.playerCrouch);
        message.AddBool(currentMovementState == MovementStates.Sliding || currentMovementState == MovementStates.Crouching);
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
            player.playerMovement.HandleMovementData(message.GetSerializable<SimulationState>());
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
            player.playerMovement.HandleMovementData(message.GetSerializable<SimulationState>());
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

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawRay(groundCheck.position, -Vector3.up * movementSettings.groundCheckHeight);
        Gizmos.DrawRay(orientation.position, orientation.forward * 3);

        Gizmos.color = Color.magenta;
        Gizmos.DrawRay(orientation.position, slideDir * movementSettings.slideWallBlockDistance);

        Gizmos.color = Color.green;
        Gizmos.DrawRay(cameraHolder.position, cameraHolder.forward * 50f);
    }
}
