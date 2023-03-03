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
    public float horizontal;
    public float vertical;
    public bool crouch;
    public bool jump;
    public bool interact;

    public int currentTick;
}

public class MultiplayerController : MonoBehaviour
{
    public const int StateCacheSize = 1024;
    [Header("Components")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Transform orientation;
    [SerializeField] private Transform cam;

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
    public SimulationState serverSimulationState = new SimulationState();
    private int lastCorrectedFrame;

    public ClientInputState inputs { get; private set; }

    private void Start()
    {
        inputs = new ClientInputState();
    }

    private void Update()
    {
        GetInput();
    }

    private void FixedUpdate()
    {
        inputs.currentTick = playerMovement.cSPTick;

        playerMovement.SetInput(inputs.horizontal, inputs.vertical, inputs.jump, inputs.crouch, inputs.interact);

        if (!GameManager.Singleton.networking) return;
        // Sends a message containing this player input to the server im not the host
        if (!NetworkManager.Singleton.Server.IsRunning) SendInput();

        // Client side prediction stuff
        if (serverSimulationState != null) Reconciliate();

        SimulationState simulationState = CurrentSimulationState(inputs);

        int cacheIndex = playerMovement.cSPTick % StateCacheSize;

        simulationStateCache[cacheIndex] = simulationState;
        inputStateCache[cacheIndex] = inputs;
    }

    private void GetInput()
    {
        if (Input.GetKeyDown(pause)) UIManager.Instance.InGameFocusUnfocus();
        if (!UIManager.Instance.focused) return;

        inputs.horizontal = Input.GetAxisRaw("Horizontal");
        inputs.vertical = Input.GetAxisRaw("Vertical");

        inputs.jump = Input.GetKey(jump);
        inputs.crouch = Input.GetKey(crouch);
        inputs.interact = Input.GetKey(interact);
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
            Debug.LogError("Needed Reconciliation Because of cache error");
            transform.position = serverSimulationState.position;
            orientation.forward = serverSimulationState.rotation;
            playerMovement.rb.velocity = serverSimulationState.velocity;

            lastCorrectedFrame = serverSimulationState.currentTick;
            return;
        }

        // Find the difference between the vector's values. 
        float differenceX = Mathf.Abs(cachedSimulationState.position.x - serverSimulationState.position.x);
        float differenceY = Mathf.Abs(cachedSimulationState.position.y - serverSimulationState.position.y);
        float differenceZ = Mathf.Abs(cachedSimulationState.position.z - serverSimulationState.position.z); ;


        print($"difference X = {differenceX} : Cached player pos = {cachedSimulationState.position.x} : Server Pos {serverSimulationState.position.x} ");
        print($"difference y = {differenceY} : Cached player pos = {cachedSimulationState.position.y} : Server Pos {serverSimulationState.position.y} ");
        print($"difference z = {differenceZ} : Cached player pos = {cachedSimulationState.position.z} : Server Pos {serverSimulationState.position.z} ");

        //  The amount of distance in units that we will allow the client's
        //  prediction to drift from it's position on the server, before a
        //  correction is necessary. 
        float tolerance = 0;

        // A correction is necessary.
        if (differenceX > tolerance || differenceY > tolerance || differenceZ > tolerance)
        {
            Debug.LogError("Needed Reconciliation");
            // Set the player's position to match the server's state. 
            transform.position = serverSimulationState.position;
            playerMovement.rb.velocity = serverSimulationState.velocity;

            // Declare the rewindFrame as we're about to resimulate our cached inputs. 
            int rewindFrame = serverSimulationState.currentTick;

            // Loop through and apply cached inputs until we're 
            // caught up to our current simulation frame. 
            while (rewindFrame < playerMovement.cSPTick)
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
                playerMovement.SetInput(inputs.horizontal, inputs.vertical, inputs.jump, inputs.crouch, inputs.interact);

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
        // Inputs
        message.AddFloat(inputs.horizontal);
        message.AddFloat(inputs.vertical);
        message.AddBool(inputs.jump);
        message.AddBool(inputs.crouch);
        message.AddBool(inputs.interact);
        message.AddInt(playerMovement.cSPTick);

        message.AddVector3(orientation.forward);
        message.AddQuaternion(cam.rotation);
        NetworkManager.Singleton.Client.Send(message);
    }
    #endregion
}
