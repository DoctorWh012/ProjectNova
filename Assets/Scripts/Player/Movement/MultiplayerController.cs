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
    private float timer;

    private void Update()
    {
        timer += Time.deltaTime;
        while (timer >= GameManager.Singleton.minTimeBetweenTicks)
        {
            timer -= GameManager.Singleton.minTimeBetweenTicks;
            int cacheIndex = cSPTick % StateCacheSize;

            // Get the Inputs and store them in the cache
            inputStateCache[cacheIndex] = GetInput();

            // Stores the current SimState on a cache
            simulationStateCache[cacheIndex] = CurrentSimulationState();

            // Applies the movent
            playerMovement.SetInput(inputStateCache[cacheIndex].horizontal, inputStateCache[cacheIndex].vertical, inputStateCache[cacheIndex].jump, inputStateCache[cacheIndex].crouch, inputStateCache[cacheIndex].interact);

            if (!GameManager.Singleton.networking) return;

            // Sends a message containing this player input to the server im not the host
            if (!NetworkManager.Singleton.Server.IsRunning) SendInput();

            cSPTick++;
        }

        // If there's a ServerSimState available checks for reconciliation
        if (serverSimulationState != null) Reconciliate();
    }

    private ClientInputState GetInput()
    {
        if (Input.GetKeyDown(pause)) UIManager.Instance.InGameFocusUnfocus();
        if (!UIManager.Instance.focused) return new ClientInputState
        {
            horizontal = 0,
            vertical = 0,
            jump = false,
            crouch = false,
            interact = false,
            currentTick = cSPTick


        };

        return new ClientInputState
        {
            horizontal = (sbyte)Input.GetAxisRaw("Horizontal"),
            vertical = (sbyte)Input.GetAxisRaw("Vertical"),
            jump = Input.GetKey(jump),
            crouch = Input.GetKey(crouch),
            interact = Input.GetKey(interact),
            currentTick = cSPTick
        };
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
        // if (posDif > 0.001f || rotDif > 0.001f)
        // {
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
        // }
        // Once we're complete, update the lastCorrectedFrame to match.
        // NOTE: Set this even if there's no correction to be made. 
        lastCorrectedFrame = serverSimulationState.currentTick;
    }

    #region  Messages
    private void SendInput()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ClientToServerId.input);
        message.AddByte((byte)(cSPTick - serverSimulationState.currentTick));

        // Sends all the messages starting from the last received server tick until our current tick
        print($"Sending {(cSPTick - serverSimulationState.currentTick)}");

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
        NetworkManager.Singleton.Client.Send(message);
    }
    #endregion
}
