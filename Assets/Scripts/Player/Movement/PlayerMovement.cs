using System.Collections;
using System.Collections.Generic;
using Riptide;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public bool interact { get; private set; }
    [Header("Components")]
    [SerializeField] private Player player;
    [SerializeField] public Rigidbody rb;
    [SerializeField] private Transform orientation;
    [SerializeField] private Transform cam;
    [SerializeField] private LayerMask ground;
    [SerializeField] private Transform groundCheck;

    [Space]
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 13;
    [SerializeField] private float crouchedSpeedMultiplier = 1.2f;
    [SerializeField] private float groundDrag = 5;
    [SerializeField] private float airDrag = 0;
    [SerializeField] private float jumpForce = 15;
    [SerializeField] private float maxSlopeAngle = 45;

    [Space]
    [Header("Other Settings")]
    [SerializeField] private float groundCheckHeight = 0.2f;
    [SerializeField] private float jumpCooldown = 0.3f;
    [SerializeField] private float airMultiplier = 4;

    // Multiplayer movement bools
    private bool[] inputs;
    private int[] movementInput;
    private bool jumping;
    private bool crouch;

    // Movement related stuff
    private bool readyToJump = true;
    private bool grounded;
    private bool isCrouching;
    private int horizontalInput;
    private int verticalInput;
    private Vector3 MoveDirection;
    private RaycastHit slopeHit;

    // Multiplayer Part

    private void Awake()
    {
        if (!NetworkManager.Singleton.Server.IsRunning) { this.enabled = false; return; }
    }

    private void OnValidate()
    {
        if (player == null)
        {
            player = GetComponent<Player>();
        }
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
            Invoke("ResetJump", jumpCooldown);
        }

        if (crouch && !isCrouching)
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

    // Normal Movement Stuff

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
        ApllyDrag();
    }

    private void FixedUpdate()
    {
        if (OnSlope())
            ApplyMovement(GetSlopeMoveDirection());
        else
        {
            ApplyMovement(orientation.forward);
            IncreaseFallGravity();
        }
        rb.useGravity = !OnSlope();
        SendMovement();
    }

    private void ApplyMovement(Vector3 trueForward)
    {
        MoveDirection = trueForward * verticalInput + orientation.right * horizontalInput;

        if (grounded) rb.AddForce(MoveDirection.normalized * moveSpeed * 10, ForceMode.Force);
        else rb.AddForce(MoveDirection.normalized * airMultiplier * 10, ForceMode.Force);
    }

    private void CheckIfGrounded()
    {
        grounded = Physics.Raycast(groundCheck.position, Vector3.down, groundCheckHeight, ground);
    }

    private void ApllyDrag()
    {
        if (grounded) rb.drag = groundDrag;
        else rb.drag = airDrag;
    }

    private void ResetJump()
    {
        readyToJump = true;
    }

    private void Jump()
    {
        rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);

        rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
    }

    private void SpeedCap()
    {
        Vector3 flatVel = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        if (!isCrouching)
        {
            if (flatVel.magnitude > moveSpeed)
            {
                Vector3 limitedVel = flatVel.normalized * moveSpeed;
                rb.velocity = new Vector3(limitedVel.x, rb.velocity.y, limitedVel.z);
            }
        }
        else
        {
            if (flatVel.magnitude > moveSpeed)
            {
                Vector3 limitedVel = flatVel.normalized * (moveSpeed * crouchedSpeedMultiplier);
                rb.velocity = new Vector3(limitedVel.x, rb.velocity.y, limitedVel.z);
            }
        }
    }

    private void IncreaseFallGravity()
    {
        rb.AddForce(Vector3.down * 10);
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
        if (Physics.Raycast(groundCheck.position, Vector3.down, out slopeHit, groundCheckHeight))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle < maxSlopeAngle && angle != 0;
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
    }
}
