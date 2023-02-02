using Riptide;
using UnityEngine;

public class SimulationState
{
    public Vector3 position;
    public Vector3 rotation;
    public Vector3 velocity;
    public int currentTick;
}

public class ClientInputState
{
    public bool[] inputs;
    public int currentTick;
}

public class MultiplayerController : MonoBehaviour
{
    public const int StateCacheSize = 1024;
    [Header("Components")]
    [SerializeField] private PlayerMovement playerMovement;
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



    //----Client Side Prediction---
    private SimulationState[] simulationStateCache = new SimulationState[StateCacheSize];
    private ClientInputState[] inputStateCache = new ClientInputState[StateCacheSize];
    public SimulationState serverSimulationState;
    private int lastCorrectedFrame;


    public bool isGrounded { get; private set; }
    public ClientInputState inputs { get; private set; }

    private void Start()
    {
        inputs = new ClientInputState();
        inputs.inputs = new bool[7];
    }

    private void Update()
    {
        CheckIfGrounded();

        if (Input.GetKeyDown(pause)) UIManager.Instance.InGameFocusUnfocus();
        if (!UIManager.Instance.focused) return;

        // inputs = new ClientInputState();
        // inputs.inputs = new bool[7];

        if (Input.GetKey(forward)) inputs.inputs[0] = true;
        if (Input.GetKey(backward)) inputs.inputs[1] = true;
        if (Input.GetKey(left)) inputs.inputs[2] = true;
        if (Input.GetKey(right)) inputs.inputs[3] = true;
        if (Input.GetKeyDown(jump)) inputs.inputs[4] = true;
        if (Input.GetKey(crouch)) inputs.inputs[5] = true;
        if (Input.GetKey(interact)) inputs.inputs[6] = true;
    }

    private void FixedUpdate()
    {
        inputs.currentTick = NetworkManager.Singleton.CurrentTick;

        playerMovement.SetInput(inputs.inputs, orientation.forward, cam.rotation);

        if (!NetworkManager.Singleton.Server.IsRunning) SendInput();

        if (serverSimulationState != null) Reconciliate();

        SimulationState simulationState = CurrentSimulationState(inputs);

        int cacheIndex = NetworkManager.Singleton.CurrentTick % StateCacheSize;

        simulationStateCache[cacheIndex] = simulationState;
        inputStateCache[cacheIndex] = inputs;

        for (int i = 0; i < inputs.inputs.Length; i++)
        {
            inputs.inputs[i] = false;
        }
    }

    private void CheckIfGrounded()
    {
        isGrounded = Physics.Raycast(groundCheck.position, Vector3.down, groundCheckHeight, groundLayer);
    }

    public SimulationState CurrentSimulationState(ClientInputState inputs)
    {
        return new SimulationState
        {
            position = transform.position,
            rotation = orientation.forward,
            velocity = playerMovement.rb.velocity,
            currentTick = inputs.currentTick
        };
    }

    private void Reconciliate()
    {
        if (serverSimulationState.currentTick <= lastCorrectedFrame) return;

        int cacheIndex = serverSimulationState.currentTick % StateCacheSize;

        ClientInputState cachedInputState = inputStateCache[cacheIndex];
        SimulationState cachedSimulationState = simulationStateCache[cacheIndex];

        if (cachedInputState == null || cachedSimulationState == null)
        {
            transform.position = serverSimulationState.position;
            orientation.forward = serverSimulationState.rotation;
            playerMovement.rb.velocity = serverSimulationState.velocity;

            lastCorrectedFrame = serverSimulationState.currentTick;
            return;
        }

        // Find the difference between the vector's values. 
        float differenceX = Mathf.Abs(cachedSimulationState.position.x - serverSimulationState.position.x);
        float differenceY = Mathf.Abs(cachedSimulationState.position.y - serverSimulationState.position.y);
        float differenceZ = Mathf.Abs(cachedSimulationState.position.z - serverSimulationState.position.z);

        //  The amount of distance in units that we will allow the client's
        //  prediction to drift from it's position on the server, before a
        //  correction is necessary. 
        float tolerance = 0F;

        // A correction is necessary.
        if (differenceX > tolerance || differenceY > tolerance || differenceZ > tolerance)
        {
            print("Correction was necessary");
            // Set the player's position to match the server's state. 
            transform.position = serverSimulationState.position;
            playerMovement.rb.velocity = serverSimulationState.velocity;

            // Declare the rewindFrame as we're about to resimulate our cached inputs. 
            int rewindFrame = serverSimulationState.currentTick;

            // Loop through and apply cached inputs until we're 
            // caught up to our current simulation frame. 
            while (rewindFrame < NetworkManager.Singleton.CurrentTick)
            {
                // Determine the cache index 
                int rewindCacheIndex = rewindFrame % StateCacheSize;

                // Obtain the cached input and simulation states.
                ClientInputState rewindCachedInputState = inputStateCache[rewindCacheIndex];
                SimulationState rewindCachedSimulationState = simulationStateCache[rewindCacheIndex];

                // If there's no state to simulate, for whatever reason, 
                // increment the rewindFrame and continue.
                if (rewindCachedInputState == null || rewindCachedSimulationState == null)
                {
                    ++rewindFrame;
                    continue;
                }

                // Process the cached inputs. 
                playerMovement.SetInput(rewindCachedInputState.inputs, orientation.forward, cam.rotation);

                // Replace the simulationStateCache index with the new value.
                SimulationState rewoundSimulationState = CurrentSimulationState(inputs);
                rewoundSimulationState.currentTick = rewindFrame;
                simulationStateCache[rewindCacheIndex] = rewoundSimulationState;

                // Increase the amount of frames that we've rewound.
                ++rewindFrame;
            }
        }

        // Once we're complete, update the lastCorrectedFrame to match.
        // NOTE: Set this even if there's no correction to be made. 
        lastCorrectedFrame = serverSimulationState.currentTick;
    }

    #region  Messages
    private void SendInput()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ClientToServerId.input);
        message.AddBools(inputs.inputs, false);
        message.AddVector3(orientation.forward);
        message.AddQuaternion(cam.rotation);
        message.AddInt(NetworkManager.Singleton.CurrentTick);
        NetworkManager.Singleton.Client.Send(message);
    }
    #endregion
}
