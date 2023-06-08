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

    private bool interacted = false;

    private ClientInputState inputs = new ClientInputState();

    private void Update()
    {
        inputs = GetInput();

        timer += Time.deltaTime;
        while (timer >= GameManager.Singleton.minTimeBetweenTicks)
        {
            timer -= GameManager.Singleton.minTimeBetweenTicks;

            int cacheIndex = cSPTick % StateCacheSize;

            // Get the Inputs and store them in the cache
            inputs.currentTick = cSPTick;
            inputStateCache[cacheIndex] = inputs;

            // Stores the current SimState on a cache
            simulationStateCache[cacheIndex] = CurrentSimulationState();

            // Applies the movent
            playerMovement.SetInput(inputStateCache[cacheIndex].horizontal, inputStateCache[cacheIndex].vertical,
                inputStateCache[cacheIndex].jump, inputStateCache[cacheIndex].crouch, inputStateCache[cacheIndex].interact, false);

            // Sends a message containing this player input to the server im not the host
            if (!NetworkManager.Singleton.Server.IsRunning) SendInput();
            interacted = false;

            cSPTick++;
        }
        if (!GameManager.Singleton.networking || NetworkManager.Singleton.Server.IsRunning) return;

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

        if (Input.GetKeyDown(interact)) interacted = true;

        return new ClientInputState
        {
            horizontal = (sbyte)Input.GetAxisRaw("Horizontal"),
            vertical = (sbyte)Input.GetAxisRaw("Vertical"),
            jump = Input.GetKey(jump),
            crouch = Input.GetKey(crouch),
            interact = interacted,
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
        if (serverSimulationState.currentTick <= lastCorrectedFrame) return;

        // Makes sure that the ServerSimState is not outdated
        int cacheIndex = serverSimulationState.currentTick % StateCacheSize;

        // ClientInputState cachedInputState = inputStateCache[cacheIndex];
        SimulationState cachedSimulationState = simulationStateCache[cacheIndex];

        // print($"ServerPos {serverSimulationState.currentTick} | CachedPos {cachedSimulationState.currentTick}");
        // print($"<color=yellow>Serverpos: {serverSimulationState.position} | CachedPos: {cachedSimulationState.position}</color>");
        // Find the difference between the Server Player Pos And the Client predicted Pos
        // float posDif = Vector3.Distance(serverSimulationState.position, cachedSimulationState.position);
        // float rotDif = 1f - Vector3.Dot(serverSimulationState.rotation, cachedSimulationState.rotation);

        Vector3 posDif = serverSimulationState.position - cachedSimulationState.position;
        print($"<color=red>Tick: {serverSimulationState.currentTick} | PosE: {posDif} </color>");

        // A correction is necessary.
        if (posDif.sqrMagnitude > 0.001f)
        {
            // print("<color=blue>Recon</color>");
            // Set the player's position to match the server's state. 
            playerMovement.rb.position = serverSimulationState.position;
            playerMovement.speed = serverSimulationState.velocity;
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

                // Replace the simulationStateCache index with the new value.
                SimulationState rewoundSimulationState = CurrentSimulationState();
                rewoundSimulationState.currentTick = rewindTick;
                simulationStateCache[rewindCacheIndex] = rewoundSimulationState;

                // Process the cached inputs.
                playerMovement.SetInput(rewindCachedInputState.horizontal, rewindCachedInputState.vertical, rewindCachedInputState.jump, rewindCachedInputState.crouch, rewindCachedInputState.interact, true);

                // Increase the amount of frames that we've rewound.
                ++rewindTick;
            }
        }
        lastCorrectedFrame = serverSimulationState.currentTick;
    }

    #region  Messages
    private void SendInput()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ClientToServerId.input);
        message.AddByte((byte)(cSPTick - serverSimulationState.currentTick));

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
        NetworkManager.Singleton.Client.Send(message);
    }
    #endregion
}
