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
    [SerializeField] private Transform orientation;
    [SerializeField] private Transform cam;
    [SerializeField] private LayerMask ground;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private PlayerMovementSettings movementSettings;

    //----Multiplayer movement bools----
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

    //----Multiplayer Part----

    private void Awake()
    {
        if (!NetworkManager.Singleton.Server.IsRunning) { this.enabled = false; return; }
    }

    public void SetInput(bool[] inputs, Vector3 forward, Quaternion cam)
    {
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
        if (inputs[4]) jumping = true;
        if (inputs[5]) crouch = true;
        if (inputs[6]) interact = true;


        if (jumping && grounded && readyToJump)
        {
            readyToJump = false;
            Jump();
            Invoke("ResetJump", movementSettings.jumpCooldown);
        }

        if (crouch && !isCrouching && !wallRunning)
        {
            StartCrouch();
        }
        if (!crouch && isCrouching)
        {
            EndCrouch();
        }
        movementInput[0] = verticalInput;
        movementInput[1] = horizontalInput;
    }

    //----MOVEMENT STUFF----

    private void Start()
    {
        movementInput = new int[2];
        inputs = new bool[7];
        rb.freezeRotation = true;
    }
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

        if (jumping) WallJump();
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

    private void StartCrouch()
    {
        isCrouching = true;
        transform.localScale = new Vector3(1, 0.5f, 1);
    }

    private void EndCrouch()
    {
        isCrouching = false;

        transform.localScale = new Vector3(1, 1, 1);
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
        // if (NetworkManager.Singleton.CurrentTick % 2 != 0) return;
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.playerMovement);
        message.AddUShort(player.Id);
        message.AddUShort(NetworkManager.Singleton.CurrentTick);
        message.AddVector3(transform.position);
        message.AddVector3(orientation.forward);
        message.AddQuaternion(cam.rotation);
        message.AddInts(movementInput);
        message.AddBool(inputs[5]);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    private void SendWallRun(bool state, int i)
    {
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
            player.Movement.SetInput(message.GetBools(7), message.GetVector3(), message.GetQuaternion());
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
