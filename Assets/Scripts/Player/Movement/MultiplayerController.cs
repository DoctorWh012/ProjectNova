using Riptide;
using UnityEngine;

public class SimulationState
{
    public Vector3 position;
    public Vector3 rotation;
    public Vector3 velocity;
    public Vector3 angularVelocity;
    public int currentTick;
}

public class ClientInputState
{
    public sbyte horizontal;
    public sbyte vertical;
    public bool crouch;
    public bool jump;
    public bool interact;

    public int currentTick;
}

public class MultiplayerController : MonoBehaviour
{
    public int cSPTick { get; private set; }
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
    private Vector3 positionError = Vector3.zero;
    private Quaternion rotationError = Quaternion.identity;
    private int lastCorrectedFrame;

    public ClientInputState inputs { get; private set; } = new ClientInputState();
    public float minTimeBetweenTicks { get; private set; }
    private float timer;

    private void Start()
    {
        minTimeBetweenTicks = 1f / NetworkManager.ServerTickRate;
    }

    private void Update()
    {
        GetInput();
        timer += Time.deltaTime;

        while (timer >= minTimeBetweenTicks)
        {
            timer -= minTimeBetweenTicks;
            int cacheIndex = cSPTick % StateCacheSize;

            // Get the Inputs and store them in the cache
            inputs.currentTick = cSPTick;
            inputStateCache[cacheIndex] = inputs;

            // Stores the current SimState on a cache
            SimulationState simulationState = CurrentSimulationState(inputs);
            simulationStateCache[cacheIndex] = simulationState;

            // Applies the movent
            playerMovement.SetInput(inputs.horizontal, inputs.vertical, inputs.jump, inputs.crouch, inputs.interact);

            if (!GameManager.Singleton.networking) return;
            // Sends a message containing this player input to the server im not the host
            if (!NetworkManager.Singleton.Server.IsRunning) SendInput();

            cSPTick++;
        }

        // If there's a ServerSimState available checks for reconciliation
        if (serverSimulationState != null) Reconciliate();

        positionError *= 0.9f;
        rotationError = Quaternion.Slerp(rotationError, Quaternion.identity, 0.1f);

        transform.position = playerMovement.rb.position + positionError;
        orientation.rotation = orientation.rotation * rotationError;
    }

    private void GetInput()
    {
        if (Input.GetKeyDown(pause)) UIManager.Instance.InGameFocusUnfocus();
        if (!UIManager.Instance.focused) return;
        inputs.horizontal = (sbyte)Input.GetAxisRaw("Horizontal");
        inputs.vertical = (sbyte)Input.GetAxisRaw("Vertical");

        inputs.jump = Input.GetKey(jump);
        inputs.crouch = Input.GetKey(crouch);
        inputs.interact = Input.GetKey(interact);
    }

    public SimulationState CurrentSimulationState(ClientInputState inputs)
    {
        return new SimulationState
        {
            position = playerMovement.rb.position,
            rotation = orientation.forward,
            velocity = playerMovement.rb.velocity,
            angularVelocity = playerMovement.rb.angularVelocity,
            currentTick = inputs.currentTick
        };
    }

    private void Reconciliate()
    {
        // Makes sure that the ServerSimState is not outdated
        if (serverSimulationState.currentTick <= lastCorrectedFrame) return;

        int cacheIndex = serverSimulationState.currentTick % StateCacheSize;

        ClientInputState cachedInputState = inputStateCache[cacheIndex];
        SimulationState cachedSimulationState = simulationStateCache[cacheIndex];

        if (cachedInputState == null || cachedSimulationState == null)
        {
            Debug.LogError("Needed Reconciliation Because of cache error");
            playerMovement.rb.position = serverSimulationState.position;
            playerMovement.rb.velocity = serverSimulationState.velocity;
            playerMovement.rb.angularVelocity = serverSimulationState.angularVelocity;
            orientation.forward = serverSimulationState.rotation;

            lastCorrectedFrame = serverSimulationState.currentTick;
            return;
        }

        // Find the difference between the Server Player Pos And the Client predicted Pos

        float posDif = Vector3.Distance(cachedSimulationState.position, serverSimulationState.position);

        // A correction is necessary.
        if (posDif > 0.001f)
        {
            // Get the predicted correct position
            Vector3 predictedPos = playerMovement.rb.position + positionError;
            Quaternion predictedRot = orientation.rotation * rotationError;

            // Set the player's position to match the server's state. 
            playerMovement.rb.position = serverSimulationState.position;
            playerMovement.rb.velocity = serverSimulationState.velocity;
            playerMovement.rb.angularVelocity = serverSimulationState.angularVelocity;
            orientation.forward = serverSimulationState.rotation;

            // Declare the rewindFrame as we're about to resimulate our cached inputs. 
            int rewindTick = serverSimulationState.currentTick;

            // Loop through and apply cached inputs until we're 
            // caught up to our current simulation frame. 
            while (rewindTick < cSPTick)
            {
                // Determine the cache index 
                int rewindCacheIndex = rewindTick % StateCacheSize;

                // Obtain the cached input and simulation states.
                ClientInputState rewindCachedInputState = inputStateCache[rewindCacheIndex];
                SimulationState rewindCachedSimulationState = simulationStateCache[rewindCacheIndex];

                // If there's no state to simulate, for whatever reason, 
                // increment the rewindFrame and continue.
                if (rewindCachedInputState == null || rewindCachedSimulationState == null)
                {
                    ++rewindTick;
                    continue;
                }

                // Process the cached inputs. 
                playerMovement.SetInput(rewindCachedInputState.horizontal, rewindCachedInputState.vertical, rewindCachedInputState.jump, rewindCachedInputState.crouch, rewindCachedInputState.interact);

                // Replace the simulationStateCache index with the new value.
                SimulationState rewoundSimulationState = CurrentSimulationState(rewindCachedInputState);
                rewoundSimulationState.currentTick = rewindTick;
                simulationStateCache[rewindCacheIndex] = rewoundSimulationState;

                // Increase the amount of frames that we've rewound.
                ++rewindTick;
            }

            if ((predictedPos - playerMovement.rb.position).sqrMagnitude > -4f)
            {
                positionError = Vector3.zero;
                rotationError = Quaternion.identity;
            }

            else
            {
                positionError = positionError - playerMovement.rb.position;
                rotationError = Quaternion.Inverse(playerMovement.rb.rotation) * rotationError;
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
        message.AddInt(inputs.currentTick);
        message.AddSByte(inputs.horizontal);
        message.AddSByte(inputs.vertical);
        message.AddBool(inputs.jump);
        message.AddBool(inputs.crouch);
        message.AddBool(inputs.interact);

        message.AddVector3(orientation.forward);
        message.AddQuaternion(cam.rotation);
        NetworkManager.Singleton.Client.Send(message);
    }
    #endregion
}
