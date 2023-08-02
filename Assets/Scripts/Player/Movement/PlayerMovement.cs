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
    //----READONLY VARIABLES----
    public bool interacting { get; private set; }
    public bool grounded { get; private set; }
    public bool isCrouching { get; private set; } = false;

    [Header("Keybinds")]
    [SerializeField] private KeyCode crouchKey;
    [SerializeField] private KeyCode interact;
    [SerializeField] private KeyCode pause;

    //----COMPONENTS----
    [Header("Components")]
    [SerializeField] private Player player;
    [SerializeField] private GunShoot gunShoot;
    [SerializeField] private PlayerEffects playerEffects;
    [SerializeField] public Rigidbody rb;
    [SerializeField] private CapsuleCollider groundingCol;
    [SerializeField] private Transform orientation;
    [SerializeField] private Transform playerCharacter;
    [SerializeField] public Transform cam;
    [SerializeField] private LayerMask ground;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private PlayerMovementSettings movementSettings;

    private float interactBufferCounter;
    private float timer;

    //----Movement related stuff----
    public bool movementFreeze = false;
    private float horizontalInput;
    private float verticalInput;
    public bool wallRunning;

    private Vector3 moveDirection;
    private RaycastHit slopeHit;

    // Jump Related
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private bool readyToJump = true;

    // Wallruning
    private bool onWallLeft;
    private bool onWallRight;
    private RaycastHit leftWallHit;
    private RaycastHit rightWallHit;

    // Lag Compensation
    public SimulationState[] playerSimulationState = new SimulationState[NetworkManager.lagCompensationCacheSize];
    private Vector3 savedPlayerPos;

    private void Awake()
    {
        rb.freezeRotation = true;
    }

    //----MOVEMENT STUFF----
    private void Update()
    {
        CheckIfGrounded();
        if (!player.IsLocal) return;
        CheckSlideGrind(isCrouching, rb.velocity);
        CheckCameraTilt();
        GetInput();

        timer += Time.deltaTime;
        while (timer >= NetworkManager.Singleton.minTimeBetweenTicks)
        {
            timer -= NetworkManager.Singleton.minTimeBetweenTicks;

            if (movementFreeze) return;
            SpeedCap();
            ApplyDrag();
            VerifyWallRun();

            if (jumpBufferCounter > 0 && coyoteTimeCounter > 0 && readyToJump)
            {
                readyToJump = false;
                Jump();
                Invoke("ResetJump", movementSettings.jumpCooldown);
            }

            if (wallRunning) WallRunMovement();

            else if (OnSlope()) ApplyMovement(GetSlopeMoveDirection());
            else
            {
                ApplyMovement(orientation.forward);
                IncreaseFallGravity(movementSettings.gravity);
            }
            if (!wallRunning) rb.useGravity = !OnSlope();

            if (NetworkManager.Singleton.Server.IsRunning) SendServerPlayerMovement();
            else SendClientPlayerMovement();
        }
    }

    public void GetInput()
    {
        // Pausing
        if (Input.GetKeyDown(pause)) UIManager.Instance.InGameFocusUnfocus();
        if (!UIManager.Instance.focused) return;

        // Forwards Sideways movement
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        // Crouching
        if (Input.GetKeyDown(crouchKey)) Crouch(true);
        if (Input.GetKeyUp(crouchKey)) Crouch(false);

        // Jumping
        if (Input.GetAxisRaw("Jump") > 0) { jumpBufferCounter = movementSettings.jumpBufferTime; }
        else jumpBufferCounter -= Time.deltaTime;

        // Interacting
        if (Input.GetKeyDown(interact)) interactBufferCounter = movementSettings.interactBufferTime;
        else interactBufferCounter -= Time.deltaTime;

        interacting = interactBufferCounter > 0;
    }

    // Lag Compensation 
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
        // print($"Before SetPlayerPos call character was at {playerCharacter.position} on tick {NetworkManager.Singleton.serverTick}");
        savedPlayerPos = playerCharacter.position;
        int cacheIndex = tick % NetworkManager.lagCompensationCacheSize;

        if (playerSimulationState[cacheIndex].currentTick != tick) return;

        playerCharacter.position = playerSimulationState[cacheIndex].position;
    }

    public void ResetPlayerPosition()
    {
        // print($"Reset Character pos to {playerCharacter.position} on tick {NetworkManager.Singleton.serverTick}");
        playerCharacter.position = savedPlayerPos;
    }

    // Movement

    private void ApplyMovement(Vector3 trueForward)
    {
        moveDirection = trueForward * verticalInput + orientation.right * horizontalInput;

        if (grounded) rb.AddForce(moveDirection.normalized * movementSettings.moveSpeed * 10, ForceMode.Force);
        else rb.AddForce(moveDirection.normalized * movementSettings.airMultiplier * 10, ForceMode.Force);
    }

    private void WallRunMovement()
    {
        rb.useGravity = false;
        IncreaseFallGravity(movementSettings.wallRunGravity);

        Vector3 wallNormal = onWallRight ? rightWallHit.normal : leftWallHit.normal;
        Vector3 wallForward = Vector3.Cross(wallNormal, transform.up);

        if ((orientation.forward - wallForward).magnitude > (orientation.forward - -wallForward).magnitude) wallForward = -wallForward;

        ApplyMovement(wallForward);

        //STICKS PLAYER TO THE WALL
        if (!(onWallLeft && horizontalInput > 0) && !(onWallRight && horizontalInput < 0))
            rb.AddForce(-wallNormal * 100, ForceMode.Force);

        if (jumpBufferCounter > 0 && readyToJump)
        {
            readyToJump = false;
            WallJump();
            Invoke("ResetJump", movementSettings.jumpCooldown);
        }
    }

    private void WallJump()
    {
        Vector3 wallNormal = onWallRight ? rightWallHit.normal : leftWallHit.normal;
        Vector3 forceToApply = transform.up * movementSettings.wallJumpUpForce + wallNormal * movementSettings.wallJumpSideForce;

        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        rb.AddForce(forceToApply, ForceMode.Impulse);

        playerEffects.PlayJumpEffects();
    }

    private void ApplyDrag()
    {
        if (grounded) rb.drag = movementSettings.groundDrag;
        else rb.drag = movementSettings.airDrag;
    }

    public void FreezePlayerMovement(bool state)
    {
        movementFreeze = state;
        rb.useGravity = !state;
        rb.velocity = Vector3.zero;
    }

    private void Jump()
    {
        rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        rb.AddForce(transform.up * movementSettings.jumpForce, ForceMode.Impulse);
    }
    private void ResetJump()
    {
        readyToJump = true;
    }

    //----CHECKS----
    private void CheckForWall()
    {
        onWallLeft = Physics.Raycast(orientation.position, -orientation.right, out leftWallHit, movementSettings.wallDistance, wallLayer);
        onWallRight = Physics.Raycast(orientation.position, orientation.right, out rightWallHit, movementSettings.wallDistance, wallLayer);
    }

    private void VerifyWallRun()
    {
        CheckForWall();
        if ((onWallLeft || onWallRight) && !grounded)
        {
            if (!wallRunning)
            {
                wallRunning = true;
                rb.velocity = new Vector3(rb.velocity.x, rb.velocity.y / 1.5f, rb.velocity.z);
            }
        }

        else if (wallRunning) wallRunning = false;
    }

    public void CheckIfGrounded()
    {
        bool onGround = Physics.Raycast(groundCheck.position, Vector3.down, movementSettings.groundCheckHeight, ground);
        if (!grounded && onGround) playerEffects.PlayJumpEffects();
        else if (grounded && !onGround) playerEffects.PlayJumpEffects();
        grounded = onGround;
        if (grounded) coyoteTimeCounter = movementSettings.coyoteTime;
        else coyoteTimeCounter -= Time.deltaTime;
    }

    private void CheckSlideGrind(bool sliding, Vector3 velocity)
    {
        if (sliding && velocity.magnitude > 5f)
        {
            if (grounded) playerEffects.PlaySlideEffects(true);
            if (player.IsLocal && !gunShoot.isWeaponTilted) gunShoot.TiltGun(30, 0.2f);
        }
        else
        {
            playerEffects.PlaySlideEffects(false);
            if (player.IsLocal && gunShoot.isWeaponTilted) gunShoot.TiltGun(0, 0.2f);
        }
        if (!grounded) playerEffects.PlaySlideEffects(false);
    }

    private void CheckCameraTilt()
    {
        if (!player.IsLocal) return;
        if (grounded)
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
            if (wallRunning) { PlayerCam.Instance.TiltCamera(true, i, 15, 0.2f); }
            else { PlayerCam.Instance.TiltCamera(false, i, 15, 0.2f); }
        }
    }

    private void SpeedCap()
    {
        Vector3 flatVel = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        if (!isCrouching)
        {
            if (flatVel.magnitude > movementSettings.moveSpeed)
            {
                Vector3 limitedVel = flatVel.normalized * movementSettings.moveSpeed;
                rb.velocity = new Vector3(limitedVel.x, rb.velocity.y, limitedVel.z);
            }
        }
        else
        {
            if (flatVel.magnitude > movementSettings.moveSpeed)
            {
                Vector3 limitedVel = flatVel.normalized * (movementSettings.moveSpeed * movementSettings.crouchedSpeedMultiplier);
                rb.velocity = new Vector3(limitedVel.x, rb.velocity.y, limitedVel.z);
            }
        }
    }

    private void IncreaseFallGravity(float force)
    {
        rb.AddForce(Vector3.down * force);
    }

    private void Crouch(bool state)
    {
        isCrouching = state;
        if (state)
        {
            groundingCol.height = groundingCol.height / 2;
            groundCheck.localPosition = new Vector3(0, groundCheck.localPosition.y / 2, 0);
        }
        else
        {
            groundingCol.height = groundingCol.height * 2;
            groundCheck.localPosition = new Vector3(0, groundCheck.localPosition.y * 2, 0);
            if (!player.IsLocal) return;
            if (gunShoot.isWeaponTilted) gunShoot.TiltGun(0, 0.2f);
        }
    }

    private bool OnSlope()
    {
        if (Physics.Raycast(groundCheck.position, Vector3.down, out slopeHit, movementSettings.groundCheckHeight))
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

    private void SetNetPlayerOrientation(Vector3 orient, Quaternion camRot)
    {
        orientation.forward = orient;
        cam.rotation = camRot;
    }

    private void SendServerPlayerMovement()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.playerMovement);

        message.AddUShort(player.Id);
        message.AddBool(isCrouching);
        message.AddVector3(rb.position);
        message.AddVector3(rb.velocity);
        message.AddVector3(orientation.forward);
        message.AddQuaternion(cam.rotation);
        message.AddSByte((sbyte)horizontalInput);
        message.AddSByte((sbyte)verticalInput);

        NetworkManager.Singleton.Server.SendToAll(message);
    }

    private void SendClientPlayerMovement()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ClientToServerId.playerMovement);

        message.AddBool(isCrouching);
        message.AddBool(interacting);
        message.AddVector3(rb.position);
        message.AddVector3(rb.velocity);
        message.AddVector3(orientation.forward);
        message.AddQuaternion(cam.rotation);
        message.AddSByte((sbyte)horizontalInput);
        message.AddSByte((sbyte)verticalInput);

        NetworkManager.Singleton.Client.Send(message);
    }

    [MessageHandler((ushort)ServerToClientId.playerMovement)]
    private static void MoveOnClient(Message message)
    {
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            if (player.IsLocal || NetworkManager.Singleton.Server.IsRunning) return;
            bool crouch = message.GetBool();
            player.rb.position = message.GetVector3();
            player.playerMovement.CheckSlideGrind(crouch, message.GetVector3());
            player.playerMovement.SetNetPlayerOrientation(message.GetVector3(), message.GetQuaternion());
            player.playerEffects.PlayerAnimator(message.GetSByte(), message.GetSByte(), crouch);
        }
    }

    [MessageHandler((ushort)ClientToServerId.playerMovement)]
    private static void MoveOnServer(ushort fromClientId, Message message)
    {
        if (Player.list.TryGetValue(fromClientId, out Player player))
        {
            bool crouch = message.GetBool();
            player.playerMovement.interacting = message.GetBool();
            player.rb.position = message.GetVector3();
            player.playerMovement.CheckSlideGrind(crouch, message.GetVector3());
            player.playerMovement.SetNetPlayerOrientation(message.GetVector3(), message.GetQuaternion());
            player.playerEffects.PlayerAnimator(message.GetSByte(), message.GetSByte(), crouch);
            player.playerMovement.SendServerPlayerMovement();
        }
    }
}
