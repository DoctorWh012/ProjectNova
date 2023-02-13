using System.Collections;
using System.Collections.Generic;
using Riptide;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    //----READONLY VARIABLES----
    public int cSPTick { get; private set; }
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
    private bool crouch;
    public bool movementFreeze = false;

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

    private int clientTick;

    //----Multiplayer Part----

    private void Start()
    {
        movementInput = new int[2];
        inputs = new bool[7];
        rb.freezeRotation = true;
    }

    public void SetInput(bool[] inputs, Vector3 forward, Quaternion cam)
    {
        this.inputs = inputs;
        // IF NetPlayers
        if (!NetworkManager.Singleton.Server.IsRunning || player.IsLocal) return;
        orientation.forward = forward;
        this.cam.rotation = cam;
    }

    public void ZeroMovementInput()
    {
        horizontalInput = 0;
        verticalInput = 0;
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
        if (inputs[4]) { jumpBufferCounter = movementSettings.jumpBufferTime; }
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
        ApplyDrag();
        CheckCameraTilt();
        
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
        cSPTick++;
    }

    // This Receives Movement data from the Server
    public void Move(Player player, ushort tick, int serverCSPTick, Vector3 velocity, Vector3 newPosition, Vector3 forward, Quaternion camRot)
    {
        //Moves Netplayer With interpolation
        if (!player.IsLocal) player.interpolation.NewUpdate(tick, newPosition);

        if (NetworkManager.Singleton.Server.IsRunning) return;

        if (player.IsLocal && player.multiplayerController.serverSimulationState?.currentTick < serverCSPTick)
        {
            player.multiplayerController.serverSimulationState.position = newPosition;
            player.multiplayerController.serverSimulationState.rotation = forward;
            player.multiplayerController.serverSimulationState.velocity = velocity;
            player.multiplayerController.serverSimulationState.currentTick = serverCSPTick;
        }
        // IF NetPlayer on Client
        if (player.IsLocal) return;
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

        player.playerEffects.PlayJumpEffects();
    }

    private void ApplyDrag()
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
        // player.playerEffects.PlayJumpEffects();
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

        else
        {
            if (wallRunning)
            {
                wallRunning = false;
            }
        }
    }

    private void CheckIfGrounded()
    {
        bool i = Physics.Raycast(groundCheck.position, Vector3.down, movementSettings.groundCheckHeight, ground);
        if (!grounded && i) player.playerEffects.jumpSmokeParticle.Play();
        else if (grounded && !i) player.playerEffects.jumpSmokeParticle.Play();
        grounded = i;
        if (grounded) coyoteTimeCounter = movementSettings.coyoteTime;
        else coyoteTimeCounter -= Time.deltaTime;
    }

    private void ResetJump()
    {
        readyToJump = true;
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
        if (state) col.height = col.height / 2;
        else col.height = col.height * 2;
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
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.playerMovement);
        message.AddUShort(player.Id);//Player Id Correct
        message.AddUShort(NetworkManager.Singleton.CurrentTick);//ServerTick correct
        message.AddInt(clientTick);//ServerCSPTick Correct
        message.AddVector3(rb.velocity);
        message.AddVector3(transform.position);
        message.AddVector3(orientation.forward);
        message.AddQuaternion(cam.rotation);
        message.AddInts(movementInput);
        message.AddBool(crouch);

        NetworkManager.Singleton.Server.SendToAll(message);
    }

    [MessageHandler((ushort)ClientToServerId.input)]
    private static void Input(ushort fromClientId, Message message)
    {
        if (Player.list.TryGetValue(fromClientId, out Player player))
        {
            // print("Client to server input");
            player.Movement.SetInput(message.GetBools(7), message.GetVector3(), message.GetQuaternion());
            player.Movement.clientTick = message.GetInt();
        }
    }


    [MessageHandler((ushort)ServerToClientId.playerMovement)]
    private static void PlayerMove(Message message)
    {
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            player.Movement.Move(player, message.GetUShort(), message.GetInt(), message.GetVector3(), message.GetVector3(), message.GetVector3(), message.GetQuaternion());
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
