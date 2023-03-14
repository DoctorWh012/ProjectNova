using Riptide;
using UnityEngine;

public class SimulationState
{
    public Vector3 position;
    public Vector3 rotation;
    public Vector3 velocity;
    public ushort currentTick;
}

public class ClientInputState
{
    public sbyte horizontal;
    public sbyte vertical;
    public bool crouch;
    public bool jump;
    public bool interact;

    public ushort currentTick;
}

public class MultiplayerController : MonoBehaviour
{
    public ushort cSPTick { get; private set; }
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
    private Vector3 posError = Vector3.zero;
    private Quaternion rotError = Quaternion.identity;

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
            simulationStateCache[cacheIndex] = CurrentSimulationState();

            // Applies the movent
            playerMovement.SetInput(inputs.horizontal, inputs.vertical, inputs.jump, inputs.crouch, inputs.interact);

            if (!GameManager.Singleton.networking) return;
            // Sends a message containing this player input to the server im not the host
            if (!NetworkManager.Singleton.Server.IsRunning) SendInput();

            cSPTick++;
        }

        // If there's a ServerSimState available checks for reconciliation
        if (serverSimulationState != null) Reconciliate();

        // posError *= 0.9f;
        // rotError = Quaternion.Slerp(rotError, Quaternion.identity, 0.1f);
        // playerMovement.cam.position = (playerMovement.rb.position + posError) + new Vector3(0, 0.7f, 0);
        // playerMovement.cam.rotation = orientation.rotation * rotError;
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

    public SimulationState CurrentSimulationState()
    {
        return new SimulationState
        {
            position = playerMovement.rb.position,
            rotation = orientation.forward,
            velocity = playerMovement.rb.velocity,
            currentTick = cSPTick
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
            orientation.forward = serverSimulationState.rotation;

            lastCorrectedFrame = serverSimulationState.currentTick;
            return;
        }

        // Find the difference between the Server Player Pos And the Client predicted Pos

        float posDif = Vector3.Distance(cachedSimulationState.position, serverSimulationState.position);
        float rotDif = 1f - Vector3.Dot(serverSimulationState.rotation, cachedSimulationState.rotation);
        // A correction is necessary.
        if (posDif > 0.001f || rotDif > 0.001f)
        {
            // Saves the predicted position for smoothing
            Vector3 predPos = playerMovement.rb.position + posError;
            Quaternion predRot = playerMovement.rb.rotation * rotError;

            // Set the player's position to match the server's state. 
            playerMovement.rb.position = serverSimulationState.position;
            playerMovement.rb.velocity = serverSimulationState.velocity;
            orientation.forward = serverSimulationState.rotation;

            // Declare the rewindFrame as we're about to resimulate our cached inputs. 
            ushort rewindTick = serverSimulationState.currentTick;

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
                SimulationState rewoundSimulationState = CurrentSimulationState();
                rewoundSimulationState.currentTick = rewindTick;
                simulationStateCache[rewindCacheIndex] = rewoundSimulationState;

                // Increase the amount of frames that we've rewound.
                ++rewindTick;
            }
            if ((predPos - playerMovement.rb.position).sqrMagnitude >= 4f)
            {
                posError = Vector3.zero;
                rotError = Quaternion.identity;
            }
            else
            {
                posError = predPos - playerMovement.rb.position;
                rotError = Quaternion.Inverse(playerMovement.rb.rotation) * predRot;
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

        message.AddByte((byte)(cSPTick - serverSimulationState.currentTick));
        print($"Last received tick from the server is{serverSimulationState.currentTick} | Current tick is {cSPTick} | Sending {cSPTick - serverSimulationState.currentTick} inputs to the server");
        // Sends all the messages starting from the last received server tick until our current tick
        for (int i = serverSimulationState.currentTick; i < cSPTick; i++)
        {
            message.AddSByte(inputStateCache[i % StateCacheSize].horizontal);
            message.AddSByte(inputStateCache[i % StateCacheSize].vertical);
            message.AddBool(inputStateCache[i % StateCacheSize].jump);
            message.AddBool(inputStateCache[i % StateCacheSize].crouch);
            message.AddBool(inputStateCache[i % StateCacheSize].interact);
            message.AddUShort(inputStateCache[i % StateCacheSize].currentTick);
        }

        message.AddVector3(orientation.forward);
        message.AddQuaternion(cam.rotation);
        print($"Message size is {message.UnreadLength}");
        NetworkManager.Singleton.Client.Send(message);
    }
    #endregion
}
