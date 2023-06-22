using UnityEngine;
using DitzelGames.FastIK;


public class InverseKinematics : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] public LayerMask ground; // Ground layer used for raycasting
    [SerializeField] public FastIKFabric[] legs; // Important put opposite legs in their corresponding order like: leftLeg1, rightLeg1, leftLeg2, rightLeg2...
    [SerializeField] private Transform root; // The root part of the body, what drives the orientation

    [Header("Settings")]
    [SerializeField] private float legMoveThreshold; // Max distance the feet can be from the target before moving
    [SerializeField] private float legMoveSpeed; // The speed which the legs lerp to the target
    [SerializeField] private float legUpAmmount;
    [SerializeField] private Vector3 hipTargetOffset; // Set if you want the hipTarget to be offset from the legRoot

    private RigidBot rigidBot;
    private Vector3[] feetCurrentPos;
    private Vector3[] feetTargetPos;
    private Transform[] hipTargets;

    public float heightAboveGround; // Set if you want a custom height off the floor otherwise autoHeight will be used
    private float[] legProgress;
    private Vector3 currentVelocity;

    private Vector3 vector;

    // Start is called before the first frame update
    void Start()
    {
        rigidBot = GetComponent<RigidBot>();
        feetCurrentPos = new Vector3[legs.Length];
        feetTargetPos = new Vector3[legs.Length];
        hipTargets = new Transform[legs.Length];
        legProgress = new float[legs.Length];

        GetHipTargetFromLegs();

        //Get the lenght of the legs if no custom lenght has been declared
        if (heightAboveGround == 0) heightAboveGround = legs[0].CompleteLength;
        if (legMoveThreshold == 0) legMoveThreshold = heightAboveGround;

        UpdateFeetTarget();
        UpdateCurrentLegPosition(0);
        UpdateCurrentLegPosition(1);
        InvokeRepeating("SlowUpdate", 1f, 1f);
    }

    // Update is called once per frame
    void Update()
    {
        currentVelocity = rigidBot.GetVelocity() * legMoveThreshold;
        UpdateFeetTarget();
        UpdateCurrentLegPositions(legMoveThreshold);
        LerpLegs();
    }

    /// <summary> This will get the HipTarget used to raycast down and get the DesiredFeetPos from Chain </summary>
    private void GetHipTargetFromLegs()
    {
        for (int i = 0; i < legs.Length; i++)
        {
            int chainLenght = legs[i].ChainLength;
            Transform chainRoot = legs[i].transform;

            while (chainLenght > 0) { chainRoot = chainRoot.parent; chainLenght--; }
            hipTargets[i] = chainRoot;
        }
    }

    /// <summary> raycasts down from the hipTargetPosition + hipTargetOffset to get the desiredFeetPos </summary>
    private void UpdateFeetTarget()
    {
        for (int i = 0; i < hipTargets.Length; i++)
        {
            vector = hipTargets[i].position - root.position;
            if (Physics.Raycast(hipTargets[i].position + hipTargetOffset.x * vector + currentVelocity + Vector3.up, Vector3.down, out RaycastHit rayHit, 50f, ground))
            {
                feetTargetPos[i] = rayHit.point;
            }
        }
    }


    /// <summary> Check if the opposite leg is grounded using the leg Lerp Progress </summary>
    private bool OppositeLegGrounded(int leg)
    {
        int otherLeg = (leg + 1) % (legs.Length);
        return legProgress[otherLeg] < 0.01f;
    }


    /// <summary> 
    private void UpdateCurrentLegPositions(float threshold)
    {
        for (int i = 0; i < legs.Length && (OppositeLegGrounded(i) || !(legProgress[i] < 0.01f) || !(CheckDistanceFromTargetPoint(i) < 4f)); i++)
        {
            if (CheckDistanceFromTargetPoint(i) > threshold)
            {
                UpdateCurrentLegPosition(i);
            }
        }
    }

    private void UpdateCurrentLegPosition(int leg)
    {
        feetCurrentPos[leg] = feetTargetPos[leg];
        legProgress[leg] = 1;
    }

    private void SlowUpdate()
    {
        UpdateCurrentLegPositions(legMoveThreshold * 0.2f);
    }

    private float CheckDistanceFromTargetPoint(int leg)
    {
        return Vector3.Distance(feetCurrentPos[leg], feetTargetPos[leg]);
    }

    private void LerpLegs()
    {
        for (int i = 0; i < legs.Length; i++)
        {
            Transform legTarget = legs[i].Target;

            legProgress[i] = Mathf.Lerp(legProgress[i], 0, Time.deltaTime * legMoveSpeed);
            Vector3 groundOffset = Vector3.up * legUpAmmount * legProgress[i];

            legTarget.position = Vector3.Lerp(legTarget.position, feetCurrentPos[i] + groundOffset, Time.deltaTime * legMoveSpeed);
        }
    }

    // <summary> Destroys the left over Targets </summary>
    public void DestroyTargets()
    {
        for (int i = 0; i < legs.Length; i++)
        {
            Object.Destroy(legs[i].Target.gameObject);
        }
    }

    public void ForceCurrentPosition(int i)
    {
        if (legProgress != null)
        {
            // Sets the leg progress to complete and the Target to The feet actual position
            legProgress[i] = 1f;
            legs[i].Target.position = legs[i].transform.position;
        }
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        foreach (Transform target in hipTargets)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(target.position, 0.15f);
        }

        foreach (Vector3 feetTarget in feetTargetPos)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(feetTarget, 0.15f);
        }
        foreach (FastIKFabric ik in legs)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(ik.Target.position, 0.15f);

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(ik.transform.position, 0.10f);
        }
    }
}
