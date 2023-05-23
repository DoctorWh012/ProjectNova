using Riptide;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    //----READONLY VARIABLES----
    public bool interacting { get; private set; }
    public bool grounded { get; private set; }
    public bool isCrouching { get; private set; } = false;

    //----COMPONENTS----
    [Header("Components")]
    [SerializeField] private Player player;
    [SerializeField] public Rigidbody rb;
    [SerializeField] private CapsuleCollider groundingCol;
    [SerializeField] private Transform orientation;
    [SerializeField] public Transform cam;
    [SerializeField] private LayerMask ground;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private PlayerMovementSettings movementSettings;
    [SerializeField] private MultiplayerController multiplayerController;

    private float interactBufferCounter;

    //----Movement related stuff----
    public bool movementFreeze = false;
    private float horizontalInput;
    private float verticalInput;
    public bool wallRunning;

    private Vector3 moveDirection;
    private RaycastHit slopeHit;

    // Jump Related
    public bool jumping;

    // Wallruning
    private bool onWallLeft;
    private bool onWallRight;
    private RaycastHit leftWallHit;
    private RaycastHit rightWallHit;

    // Client side prediction
    public Vector3 speed;
    private ClientInputState lastReceivedInputs = new ClientInputState();
    public SimulationState[] playerSimulationState = new SimulationState[GameManager.lagCompensationCacheSize];
    private float timer;

    private Vector3 savedPlayerPos;

    private void Awake()
    {
        rb.freezeRotation = true;
    }

    private void Start()
    {
        SetPlayerKinematic(true);
    }

    //----MOVEMENT STUFF----
    private void Update()
    {
        PerformChecks();
    }

    public SimulationState CurrentSimulationState()
    {
        return new SimulationState
        {
            position = rb.position,
            rotation = orientation.forward,
            currentTick = GameManager.Singleton.serverTick
        };
    }

    public void SetPlayerPositionToTick(ushort tick)
    {
        savedPlayerPos = rb.position;
        int cacheIndex = tick % GameManager.lagCompensationCacheSize;

        if (playerSimulationState[cacheIndex].currentTick != tick) return;

        rb.position = playerSimulationState[cacheIndex].position;
    }

    public void ResetPlayerPosition()
    {
        rb.position = savedPlayerPos;
    }

    public void PerformChecks()
    {
        CheckIfGrounded(false);
        if (GameManager.Singleton.networking && !player.IsLocal && !NetworkManager.Singleton.Server.IsRunning) return; // Stops Movement PHYSICS from being applied on client's NetPlayers
        CheckSlideGrind(isCrouching, speed);
        CheckCameraTilt();
    }

    public void SetPlayerKinematic(bool state)
    {
        if (state)
        {
            rb.detectCollisions = false;
            speed = rb.velocity;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.isKinematic = true;
        }
        else
        {
            rb.detectCollisions = true;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.velocity = speed;
        }
    }

    private void MovementTick(bool resim)
    {
        if (movementFreeze) return;

        Physics.autoSimulation = false;
        SetPlayerKinematic(false);
        SpeedCap();
        ApplyDrag();
        VerifyWallRun();

        if (jumping && grounded)
        {
            Jump();
            if (resim) { print("<color=red>JUMPED WHILE RESIM</color>"); }
        }

        if (wallRunning) WallRunMovement();

        else if (OnSlope()) ApplyMovement(GetSlopeMoveDirection());
        else
        {
            ApplyMovement(orientation.forward);
            IncreaseFallGravity(movementSettings.gravity);
        }
        if (!wallRunning) rb.useGravity = !OnSlope();

        Physics.Simulate(GameManager.Singleton.minTimeBetweenTicks);
        SetPlayerKinematic(true);

        Physics.autoSimulation = true;
    }

    // This runs On LocalPlayer For CSP and on the NetPlayer on the server
    public void SetInput(float horizontal, float vertical, bool jump, bool crouch, bool interact, bool resimulating)
    {
        // Forwards Sideways movement
        horizontalInput = horizontal;
        verticalInput = vertical;

        // Crouching
        if (crouch && !isCrouching) Crouch(true);
        else if (!crouch && isCrouching) Crouch(false);

        // Jumping
        jumping = jump;

        if (resimulating) CheckIfGrounded(resimulating);

        // Interacting
        if (interact) interactBufferCounter = movementSettings.interactBufferTime;
        else interactBufferCounter -= Time.deltaTime;

        interacting = interactBufferCounter > 0;

        MovementTick(resimulating);
        if (NetworkManager.Singleton.Server.IsRunning) SendMovement(lastReceivedInputs.currentTick);
    }

    private void HandleClientInput(ClientInputState[] inputs)
    {
        if (inputs.Length == 0) return;

        // Last input in the array is the newest one
        // Here we check to see if the inputs sent by the client are newer than the ones we already have received
        if (inputs[inputs.Length - 1].currentTick >= lastReceivedInputs.currentTick)
        {
            // Here we check for were to start processing the inputs
            // if the iputs we already have are newer than the first ones sent we start at their difference 
            // if not we start at the first one
            int start = lastReceivedInputs.currentTick > inputs[0].currentTick ? (lastReceivedInputs.currentTick - inputs[0].currentTick) : 0;

            // Now that we have when to start we can simply apply all relevant inputs to the player
            for (int i = start; i < inputs.Length - 1; i++)
            {
                SetInput(inputs[i].horizontal, inputs[i].vertical, inputs[i].jump, inputs[i].crouch, inputs[i].interact, false);
                SendMovement(inputs[i].currentTick);
            }

            // Now we save the client newest input
            lastReceivedInputs = inputs[inputs.Length - 1];
        }
    }

    // This Receives Movement data from the Server
    public void Move(Player player, ushort tick, ushort serverCSPTick, Vector3 velocity, Vector3 newPosition, Vector3 forward, Quaternion camRot, bool crouching)
    {
        if (NetworkManager.Singleton.Server.IsRunning) return;

        //Moves Netplayer With interpolation Should only Run for client
        if (!player.IsLocal)
        {
            player.interpolation.NewUpdate(tick, newPosition);
            if (!isCrouching && crouching) Crouch(true);
            else if (isCrouching && !crouching) Crouch(false);
            CheckSlideGrind(crouching, velocity);
            orientation.forward = forward;
            cam.rotation = camRot;
        }

        if (player.IsLocal && multiplayerController.serverSimulationState?.currentTick < serverCSPTick)
        {
            multiplayerController.serverSimulationState.position = newPosition;
            multiplayerController.serverSimulationState.rotation = forward;
            multiplayerController.serverSimulationState.velocity = velocity;
            multiplayerController.serverSimulationState.currentTick = serverCSPTick;
        }
    }

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

        if (jumping) WallJump();
    }

    private void WallJump()
    {
        Vector3 wallNormal = onWallRight ? rightWallHit.normal : leftWallHit.normal;
        Vector3 forceToApply = transform.up * movementSettings.wallJumpUpForce + wallNormal * movementSettings.wallJumpSideForce;

        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        rb.AddForce(forceToApply, ForceMode.Impulse);

        player.playerEffects.PlayJumpEffects();
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
        speed = Vector3.zero;
    }

    private void Jump()
    {
        rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);

        rb.AddForce(transform.up * movementSettings.jumpForce, ForceMode.Impulse);
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

    public void CheckIfGrounded(bool resimulating)
    {
        bool onGround = Physics.Raycast(groundCheck.position, Vector3.down, movementSettings.groundCheckHeight, ground);

        if (!resimulating)
        {
            if (!grounded && onGround) player.playerEffects.PlayJumpEffects();
            else if (grounded && !onGround) player.playerEffects.PlayJumpEffects();
        }

        grounded = onGround;
    }

    private void CheckSlideGrind(bool sliding, Vector3 velocity)
    {
        if (sliding && velocity.magnitude > 5f)
        {
            if (grounded) player.playerEffects.PlaySlideEffects(true);
            if (player.IsLocal && !player.GunShoot.isWeaponTilted) player.GunShoot.TiltGun(30, 0.2f);
        }
        else
        {
            player.playerEffects.PlaySlideEffects(false);
            if (player.IsLocal && player.GunShoot.isWeaponTilted) player.GunShoot.TiltGun(0, 0.2f);
        }
        if (!grounded) player.playerEffects.PlaySlideEffects(false);
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
            if (player.GunShoot.isWeaponTilted) player.GunShoot.TiltGun(0, 0.2f);
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

    public void SetNetPlayerOrientation(Vector3 forward, Quaternion camRot)
    {
        orientation.forward = forward;
        cam.rotation = camRot;
    }

    private void SendMovement(ushort clientTick)
    {
        if (!NetworkManager.Singleton.Server.IsRunning) return;
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.playerMovement);
        message.AddUShort(player.Id);
        message.AddBool(isCrouching);
        message.AddUShort(NetworkManager.Singleton.CurrentTick);
        message.AddUShort(clientTick);
        message.AddVector3(speed);
        message.AddVector3(rb.position);
        message.AddVector3(orientation.forward);
        message.AddQuaternion(cam.rotation);
        message.AddByte((byte)horizontalInput);
        message.AddByte((byte)verticalInput);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    [MessageHandler((ushort)ServerToClientId.playerMovement)]
    private static void PlayerMove(Message message)
    {
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            bool crouching = message.GetBool();
            player.Movement.Move(player, message.GetUShort(), message.GetUShort(), message.GetVector3(), message.GetVector3(), message.GetVector3(), message.GetQuaternion(), crouching);
            player.playerEffects.PlayerAnimator((int)message.GetByte(), (int)message.GetByte(), crouching);
        }
    }

    [MessageHandler((ushort)ClientToServerId.input)]
    private static void Input(ushort fromClientId, Message message)
    {
        if (Player.list.TryGetValue(fromClientId, out Player player))
        {
            byte inputsQuantity = message.GetByte();
            ClientInputState[] inputs = new ClientInputState[inputsQuantity];

            for (int i = 0; i < inputsQuantity; i++)
            {
                inputs[i] = new ClientInputState
                {
                    horizontal = message.GetSByte(),
                    vertical = message.GetSByte(),
                    jump = message.GetBool(),
                    crouch = message.GetBool(),
                    interact = message.GetBool(),
                    currentTick = message.GetUShort()
                };
            }

            player.Movement.HandleClientInput(inputs);

            if (player.IsLocal) return;
            player.Movement.SetNetPlayerOrientation(message.GetVector3(), message.GetQuaternion());
        }
    }
}
