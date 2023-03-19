using System.Collections;
using System.Collections.Generic;
using Riptide;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    [SerializeField] private CapsuleCollider col;
    [SerializeField] private Transform orientation;
    [SerializeField] public Transform cam;
    [SerializeField] private LayerMask ground;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private PlayerMovementSettings movementSettings;
    [SerializeField] private MultiplayerController multiplayerController;

    //----Movement related stuff----
    public bool movementFreeze = false;
    private bool readyToJump = true;
    public bool wallRunning;
    private bool onWallLeft;
    private bool onWallRight;
    private float horizontalInput;
    private float verticalInput;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private Vector3 MoveDirection;
    private RaycastHit slopeHit;
    private RaycastHit leftWallHit;
    private RaycastHit rightWallHit;

    private ClientInputState lastReceivedInputs = new ClientInputState();
    private float timer;

    private void Awake()
    {
        rb.freezeRotation = true;
    }

    //----MOVEMENT STUFF----
    private void Update()
    {
        CheckIfGrounded();
        // Stops Movement PHYSICS from being applied on client's NetPlayers
        if (GameManager.Singleton.networking && !player.IsLocal && !NetworkManager.Singleton.Server.IsRunning) return;
        CheckSlideGrind(isCrouching, rb.velocity);
        SpeedCap();
        VerifyWallRun();
        ApplyDrag();
        CheckCameraTilt();

        timer += Time.deltaTime;
        while (timer >= GameManager.Singleton.minTimeBetweenTicks)
        {
            timer -= GameManager.Singleton.minTimeBetweenTicks;
            if (wallRunning) WallRunMovement();
            else if (OnSlope()) ApplyMovement(GetSlopeMoveDirection());
            else
            {
                ApplyMovement(orientation.forward);
                IncreaseFallGravity(movementSettings.gravity);
            }
            if (!movementFreeze && !wallRunning) rb.useGravity = !OnSlope();
            if (GameManager.Singleton.networking) SendMovement();
        }
    }

    // This runs On LocalPlayer For CSP and on the NetPlayer on the server
    public void SetInput(float horizontal, float vertical, bool jump, bool crouch, bool interact)
    {
        // Forwards Sideways movement
        horizontalInput = horizontal;
        verticalInput = vertical;

        // Jumping
        if (jump) { jumpBufferCounter = movementSettings.jumpBufferTime; }
        else jumpBufferCounter -= Time.deltaTime;

        if (jumpBufferCounter > 0 && coyoteTimeCounter > 0 && readyToJump)
        {
            readyToJump = false;
            Jump();
            Invoke("ResetJump", movementSettings.jumpCooldown);
        }

        // Crouching
        if (crouch && !isCrouching) Crouch(true);
        else if (!crouch && isCrouching) Crouch(false);

        // Interacting
        interacting = interact;
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
                SetInput(inputs[i].horizontal, inputs[i].vertical, inputs[i].jump, inputs[i].crouch, inputs[i].interact);
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
        if (movementFreeze) return;
        MoveDirection = trueForward * verticalInput + orientation.right * horizontalInput;

        if (grounded) rb.AddForce(MoveDirection.normalized * movementSettings.moveSpeed * 10, ForceMode.Force);
        else rb.AddForce(MoveDirection.normalized * movementSettings.airMultiplier * 10, ForceMode.Force);
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

        if (jumpBufferCounter > 0) WallJump();
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
        rb.velocity = Vector3.zero;
    }

    private void Jump()
    {
        if (movementFreeze) return;
        rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);

        rb.AddForce(transform.up * movementSettings.jumpForce, ForceMode.Impulse);
        coyoteTimeCounter = 0;
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
                rb.velocity = new Vector3(rb.velocity.x, rb.velocity.y / 2, rb.velocity.z);
            }
        }

        else if (wallRunning) wallRunning = false;
    }

    private void CheckIfGrounded()
    {
        bool i = Physics.Raycast(groundCheck.position, Vector3.down, movementSettings.groundCheckHeight, ground);
        if (!grounded && i) player.playerEffects.PlayJumpEffects();
        else if (grounded && !i) player.playerEffects.PlayJumpEffects();
        grounded = i;
        if (grounded) coyoteTimeCounter = movementSettings.coyoteTime;
        else coyoteTimeCounter -= Time.deltaTime;
    }

    private void ResetJump()
    {
        readyToJump = true;
    }

    private void CheckSlideGrind(bool sliding, Vector3 velocity)
    {
        if (sliding && velocity.magnitude > 5f)
        {
            if (grounded) player.playerEffects.PlaySlideEffects(true);
            if (player.IsLocal && !player.playerShooting.isWeaponTilted) player.playerShooting.TiltGun(30, 0.2f);
        }
        else
        {
            player.playerEffects.PlaySlideEffects(false);
            if (player.IsLocal && player.playerShooting.isWeaponTilted) player.playerShooting.TiltGun(0, 0.2f);
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
            col.height = col.height / 2;
            groundCheck.localPosition = new Vector3(0, groundCheck.localPosition.y / 2, 0);
        }
        else
        {
            col.height = col.height * 2;
            groundCheck.localPosition = new Vector3(0, groundCheck.localPosition.y * 2, 0);
            if (!player.IsLocal) return;
            if (player.playerShooting.isWeaponTilted) player.playerShooting.TiltGun(0, 0.2f);
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

    private void SendMovement()
    {
        if (!NetworkManager.Singleton.Server.IsRunning) return;
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.playerMovement);
        message.AddUShort(player.Id);
        message.AddBool(isCrouching);
        message.AddUShort(NetworkManager.Singleton.CurrentTick);
        message.AddUShort(lastReceivedInputs.currentTick);
        message.AddVector3(rb.velocity);
        message.AddVector3(rb.position);
        message.AddVector3(orientation.forward);
        message.AddQuaternion(cam.rotation);
        message.AddFloat(horizontalInput);
        message.AddFloat(verticalInput);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    [MessageHandler((ushort)ServerToClientId.playerMovement)]
    private static void PlayerMove(Message message)
    {
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            bool crouching = message.GetBool();
            player.Movement.Move(player, message.GetUShort(), message.GetUShort(), message.GetVector3(), message.GetVector3(), message.GetVector3(), message.GetQuaternion(), crouching);
            player.playerEffects.PlayerAnimator(message.GetFloat(), message.GetFloat(), crouching);
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

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(orientation.position, orientation.forward * 50);

        Gizmos.color = Color.green;
        Gizmos.DrawRay(orientation.position, orientation.right * 1f);
        Gizmos.DrawRay(orientation.position, -orientation.right * 1f);
    }
}
