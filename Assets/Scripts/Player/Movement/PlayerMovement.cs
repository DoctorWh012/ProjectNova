using System.Collections;
using System.Collections.Generic;
using Riptide;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    //----READONLY VARIABLES----
    public bool interact { get; private set; }
    public bool grounded { get; private set; }

    //----COMPONENTS----
    [Header("Components")]
    [SerializeField] private Player player;
    [SerializeField] public Rigidbody rb;
    [SerializeField] private CapsuleCollider col;
    [SerializeField] private Transform orientation;
    [SerializeField] private Transform cam;
    [SerializeField] private LayerMask ground;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private PlayerMovementSettings movementSettings;

    //----Multiplayer movement----
    private bool[] inputs;
    private int[] movementInput;
    private bool jumping;
    private bool crouch;
    [HideInInspector] public bool movementFreeze = false;

    //----Movement related stuff----
    private bool readyToJump = true;
    private bool isCrouching;
    public bool wallRunning;
    private bool onWallLeft;
    private bool onWallRight;
    private int horizontalInput;
    private int verticalInput;
    private Vector3 MoveDirection;
    private RaycastHit slopeHit;
    private RaycastHit leftWallHit;
    private RaycastHit rightWallHit;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;

    //----Multiplayer Part----

    private void Awake()
    {
        // if (!NetworkManager.Singleton.Server.IsRunning) { this.enabled = false; return; }
    }

    private void Start()
    {
        movementInput = new int[2];
        inputs = new bool[7];
        rb.freezeRotation = true;
    }

    public void SetInput(bool[] inputs, Vector3 forward, Quaternion cam)
    {
        // print("Set input");
        this.inputs = inputs;
        if (NetworkManager.Singleton.Server.IsRunning && player.IsLocal) return;
        orientation.forward = forward;
        this.cam.rotation = cam;
    }

    public void ZeroMovementInput()
    {
        horizontalInput = 0;
        verticalInput = 0;
        jumping = false;
        crouch = false;
        interact = false;
    }

    private void GetInput()
    {
        // Dumb conversion to multiplayer
        // Resets Inputs
        ZeroMovementInput();

        // Gets the inputs from the client
        if (inputs[0]) verticalInput = 1;
        if (inputs[1]) horizontalInput = -1;
        if (inputs[2]) verticalInput = -1;
        if (inputs[3]) horizontalInput = 1;
        if (inputs[4]) { jumping = true; jumpBufferCounter = movementSettings.jumpBufferTime; }
        else jumpBufferCounter -= Time.deltaTime;
        if (inputs[5]) crouch = true;
        if (inputs[6]) interact = true;


        if (jumpBufferCounter > 0 && coyoteTimeCounter > 0 && readyToJump)
        {
            readyToJump = false;
            Jump();
            Invoke("ResetJump", movementSettings.jumpCooldown);
        }

        if (crouch && !isCrouching && !wallRunning)
        {
            Crouch(true);
        }
        if (!crouch && isCrouching)
        {
            Crouch(false);
        }
        movementInput[0] = verticalInput;
        movementInput[1] = horizontalInput;
    }

    //----MOVEMENT STUFF----
    private void Update()
    {
        GetInput();
        SpeedCap();
        CheckIfGrounded();
        VerifyWallRun();
        ApllyDrag();
    }

    private void FixedUpdate()
    {
        if (wallRunning) WallRunMovement();

        else if (OnSlope()) ApplyMovement(GetSlopeMoveDirection());

        else
        {
            ApplyMovement(orientation.forward);
            IncreaseFallGravity(movementSettings.gravity);
        }

        if (!movementFreeze && !wallRunning) rb.useGravity = !OnSlope();
        SendMovement();
    }

    public void Move(Player player, ushort tick, Vector3 velocity, Vector3 newPosition, Vector3 forward, Quaternion camRot)
    {
        player.interpolation.NewUpdate(tick, newPosition);

        if (player.IsLocal)
        {
            if (player.multiplayerController.serverSimulationState?.currentTick < tick)
            {
                player.multiplayerController.serverSimulationState.position = newPosition;
                player.multiplayerController.serverSimulationState.rotation = forward;
                player.multiplayerController.serverSimulationState.velocity = velocity;
                player.multiplayerController.serverSimulationState.currentTick = tick;
            }
        }
        if (!player.IsLocal && !NetworkManager.Singleton.Server.IsRunning) return;
        orientation.forward = forward;
        cam.rotation = camRot;

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
    }

    private void ApllyDrag()
    {
        if (grounded) rb.drag = movementSettings.groundDrag;
        else rb.drag = movementSettings.airDrag;
    }

    public void FreezePlayerMovement(bool state)
    {
        ZeroMovementInput();
        movementFreeze = state;
        rb.useGravity = !state;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
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

        int i = onWallLeft ? 0 : 1;

        if ((onWallLeft || onWallRight) && !grounded)
        {
            if (!wallRunning)
            {
                wallRunning = true; SendWallRun(wallRunning, i);
                rb.velocity = new Vector3(rb.velocity.x, rb.velocity.y / 2, rb.velocity.z);
            }
        }

        else { if (wallRunning) wallRunning = false; SendWallRun(wallRunning, i); }
    }

    private void CheckIfGrounded()
    {
        grounded = Physics.Raycast(groundCheck.position, Vector3.down, movementSettings.groundCheckHeight, ground);
        if (grounded) coyoteTimeCounter = movementSettings.coyoteTime;
        else coyoteTimeCounter -= Time.deltaTime;
    }

    private void ResetJump()
    {
        readyToJump = true;
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
        if (state) col.height = col.height / 2;
        else col.height = col.height * 2;

        // transform.localScale = new Vector3(1, 0.5f, 1);
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

    private void SendMovement()
    {
        if (!NetworkManager.Singleton.Server.IsRunning) return;
        if (NetworkManager.Singleton.CurrentTick % 2 != 0) return;
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.playerMovement);
        message.AddUShort(player.Id);
        message.AddUShort(NetworkManager.Singleton.CurrentTick);
        message.AddVector3(rb.velocity);
        message.AddVector3(transform.position);
        message.AddVector3(orientation.forward);
        message.AddQuaternion(cam.rotation);
        message.AddInts(movementInput);
        message.AddBool(inputs[5]);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    private void SendWallRun(bool state, int i)
    {
        if (!NetworkManager.Singleton.Server.IsRunning) return;
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.wallRun);
        message.AddBool(state);
        message.AddInt(i);//0=Left / 1=Right
        NetworkManager.Singleton.Server.Send(message, player.Id);
    }

    [MessageHandler((ushort)ClientToServerId.input)]
    private static void Input(ushort fromClientId, Message message)
    {
        if (Player.list.TryGetValue(fromClientId, out Player player))
        {
            print("Client to server input");
            player.Movement.SetInput(message.GetBools(7), message.GetVector3(), message.GetQuaternion());
        }
    }

    [MessageHandler((ushort)ServerToClientId.playerMovement)]
    private static void PlayerMove(Message message)
    {
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            if (NetworkManager.Singleton.Server.IsRunning) return;
            print("Server To Client Move");
            player.Movement.Move(player, message.GetUShort(), message.GetVector3(), message.GetVector3(), message.GetVector3(), message.GetQuaternion());
            int[] inputs = message.GetInts();
            bool isSliding = message.GetBool();
            player.playerEffects.PlayerAnimator(inputs, isSliding);
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
