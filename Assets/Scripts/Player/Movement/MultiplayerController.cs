using Riptide;
using UnityEngine;

public class MultiplayerController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Transform orientation;
    [SerializeField] private Transform cam;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;

    [Header("Settings")]
    [SerializeField] private float groundCheckHeight;

    [Header("Keybinds")]
    [SerializeField] private KeyCode forward;
    [SerializeField] private KeyCode backward;
    [SerializeField] private KeyCode left;
    [SerializeField] private KeyCode right;
    [SerializeField] private KeyCode jump;
    [SerializeField] private KeyCode crouch;
    [SerializeField] public KeyCode interact;
    [SerializeField] private KeyCode pause;

    public bool isGrounded { get; private set; }
    public bool[] inputs { get; private set; }

    private void Start()
    {
        inputs = new bool[7];
    }

    private void Update()
    {
        CheckIfGrounded();

        if (Input.GetKeyDown(pause)) UIManager.Instance.InGameFocusUnfocus();
        if (!UIManager.Instance.focused) return;

        if (Input.GetKey(forward)) inputs[0] = true;
        if (Input.GetKey(backward)) inputs[1] = true;
        if (Input.GetKey(left)) inputs[2] = true;
        if (Input.GetKey(right)) inputs[3] = true;
        if (Input.GetKeyDown(jump)) inputs[4] = true;
        if (Input.GetKey(crouch)) inputs[5] = true;
        if (Input.GetKey(interact)) inputs[6] = true;
    }

    private void FixedUpdate()
    {
        SendInput();

        for (int i = 0; i < inputs.Length; i++)
        {
            inputs[i] = false;
        }
    }

    private void CheckIfGrounded()
    {
        isGrounded = Physics.Raycast(groundCheck.position, Vector3.down, groundCheckHeight, groundLayer);
    }

    #region  Messages
    private void SendInput()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ClientToServerId.input);
        message.AddBools(inputs, false);
        message.AddVector3(orientation.forward);
        message.AddQuaternion(cam.rotation);
        NetworkManager.Singleton.Client.Send(message);
    }
    #endregion
}
