using UnityEngine;

public class StandardBot : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private GameObject bullet;
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private Rigidbody head;
    [SerializeField] private Rigidbody upperArm;
    [SerializeField] private Rigidbody lowerArm;

    [Header("Settings")]
    [SerializeField] private float bulletForce;
    [SerializeField] private float fireRate;
    [SerializeField] private float fireRateRand;
    [SerializeField] private float armForce;
    [SerializeField] private float armUpForce;
    [SerializeField] private float sightRange;
    [SerializeField] private float sightCheckTime;
    [SerializeField] private float chaseRange;
    [SerializeField] private float movementSpeed;

    [SerializeField] private Transform target;
    private RigidBot rigidBot;
    private float nextTimeToFire = 0f;

    // Start is called before the first frame update
    void Start()
    {
        rigidBot = GetComponent<RigidBot>();
        InvokeRepeating("CheckSight", 0, sightCheckTime);
    }

    private void Update()
    {
        if (target == null || Vector3.Distance(head.position, target.position) > chaseRange || rigidBot.state != RigidBot.EnemyState.active) return;
        if (Time.time >= nextTimeToFire)
        {
            nextTimeToFire = Time.time + 1f / (fireRate + Random.Range(-fireRateRand, fireRateRand));
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (target == null || Vector3.Distance(head.position, target.position) > chaseRange || rigidBot.state != RigidBot.EnemyState.active) return;
        Vector3 targetDirection = target.position - head.position;
        rigidBot.RotateBody(targetDirection);
        rigidBot.MoveBody(targetDirection.normalized, movementSpeed);

        // Try to move the hand in the target's direction
        Vector3 dir = (target.position - lowerArm.position).normalized;
        lowerArm.AddForce(dir * armForce, ForceMode.Force);
        upperArm.AddForce(-dir * armForce, ForceMode.Force);

        lowerArm.AddForce(Vector3.up * armUpForce);
        upperArm.AddForce(Vector3.down * armUpForce);

        float y = lowerArm.velocity.y;
        y = Mathf.Clamp(y, -2f, 2f);
        lowerArm.AddForce(Vector3.up * (0f - y));
    }

    private void CheckSight()
    {
        float targetDist = 15;
        Collider[] hitCollider = Physics.OverlapSphere(head.position, sightRange, targetLayer);
        foreach (Collider colHit in hitCollider)
        {
            if (Vector3.Distance(head.position, colHit.transform.position) < targetDist || targetDist == 0)
            {
                target = colHit.transform;
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (target == null) return;
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(muzzleFlash.transform.position, muzzleFlash.transform.up * 30);
    }
}
