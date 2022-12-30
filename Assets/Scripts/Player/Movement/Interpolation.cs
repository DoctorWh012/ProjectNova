using System.Collections.Generic;
using UnityEngine;

public class Interpolation : MonoBehaviour
{

    [SerializeField] private float timeElapsed = 0f;
    [SerializeField] private float timeToReachTarget = 0.05f;
    [SerializeField] private float movementThreshold = 0.05f;

    private readonly List<Transformupdate> futureTransformUpdated = new List<Transformupdate>();
    private float squareMovementThreshold;
    private Transformupdate to;
    private Transformupdate from;
    private Transformupdate previous;

    private void Awake()
    {
        this.enabled = false;
    }

    // Start is called before the first frame update
    void Start()
    {
        squareMovementThreshold = movementThreshold * movementThreshold;
        to = new Transformupdate(NetworkManager.Singleton.ServerTick, transform.position);
        from = new Transformupdate(NetworkManager.Singleton.InterpolationTick, transform.position);
        previous = new Transformupdate(NetworkManager.Singleton.InterpolationTick, transform.position);
    }

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < futureTransformUpdated.Count; i++)
        {
            if (NetworkManager.Singleton.ServerTick >= futureTransformUpdated[i].Tick)
            {
                previous = to;
                to = futureTransformUpdated[i];
                from = new Transformupdate(NetworkManager.Singleton.InterpolationTick, transform.position);
            }
            futureTransformUpdated.RemoveAt(i);
            i--;
            timeElapsed = 0;
            float ticksToReach = (to.Tick - from.Tick);
            if (ticksToReach == 0f) ticksToReach = 1f;
            timeToReachTarget = ticksToReach * Time.fixedDeltaTime;
        }
        timeElapsed += Time.deltaTime;
        InterpolatePosition(timeElapsed / timeToReachTarget);
    }

    private void InterpolatePosition(float lerpAmmount)
    {
        if ((to.Position - previous.Position).sqrMagnitude < squareMovementThreshold)
        {
            if (to.Position != from.Position)
            {
                transform.position = Vector3.Lerp(from.Position, to.Position, lerpAmmount);
            }
            return;
        }
        transform.position = Vector3.LerpUnclamped(from.Position, to.Position, lerpAmmount);
    }

    public void NewUpdate(ushort tick, Vector3 position)
    {
        if (tick <= NetworkManager.Singleton.InterpolationTick) return;

        for (int i = 0; i < futureTransformUpdated.Count; i++)
        {
            if (tick < futureTransformUpdated[i].Tick)
            {
                futureTransformUpdated.Insert(i, new Transformupdate(tick, position));
                return;
            }
        }
        futureTransformUpdated.Add(new Transformupdate(tick, position));
    }
}
