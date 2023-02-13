using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RigidBot : MonoBehaviour
{
    public enum EnemyState
    {
        active = 0,
        tumbling = 1,
        falling = 2,
        recovering = 3,
        dead = 4
    }

    [Header("State")]
    [SerializeField] public EnemyState state;

    [Header("Components")]
    [SerializeField] private InverseKinematics botIk;
    [SerializeField] private Transform root;
    [SerializeField] private Transform spine;
    [SerializeField] private Transform head;

    [Header("Settings")]
    [SerializeField] private float legGroundCheckRadius;
    [SerializeField] private float legPushForce;
    [SerializeField] private float moveLegsWithSpeedScale;
    [SerializeField] private float recoverTime;
    [SerializeField] private float recoverTimeRand;
    [SerializeField] private float recoveryForce;
    [SerializeField] private float maxRotationForce;
    [SerializeField] private float getUpAngle; // The angle of the root which the bot is consider to have gotten up after falling
    [SerializeField] private float getUpMagT;
    [SerializeField] private float tumbleAngle; // The angle which the bot starts to tumble
    [SerializeField] private float fallAngle; // The angle which the bot starts to fall

    private Rigidbody rootRb;
    private Rigidbody spineRb;
    private Rigidbody headRb;

    private bool isRagdoll;
    private bool legGrounded;
    private bool recovering;
    private int legAmmount;
    private float balancingForce;
    private float stateForceMultiplier;
    private float rotationForce;
    private Transform[] groundChecks;


    // Start is called before the first frame update
    void Start()
    {
        GetMainRigidBodies();
        CalculateBalancingForce();
        UpdateState(EnemyState.active);
        legAmmount = botIk.legs.Length;
        groundChecks = new Transform[legAmmount];
        GetGroundCheckPositions();
        DisableSelfCollision(true);
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (state == EnemyState.dead) return;
        CheckIfLegsGrounded();

        float distanceFromGround = 0;
        if (state != EnemyState.dead)
        {
            // Checks if bot is falling
            if (!Physics.Raycast(root.position, Vector3.down, out RaycastHit hitInfo, botIk.heightAboveGround * 3f, botIk.ground))
                UpdateState(EnemyState.falling);
            // If it's not falling saves the current distance from the ground
            else
            {
                distanceFromGround = hitInfo.distance;
            }
        }

        // Gets the boot root angle
        float rootAngle = Vector3.Angle(Vector3.up, root.up);
        // This is used for landing the bot
        if (state == EnemyState.falling)
        {
            // If the spine is not completely straight or over 50 degrees The bot does not fall
            if (rootAngle != 0f && distanceFromGround < botIk.heightAboveGround * 1.5f && rootAngle < 50f)
            {
                UpdateState(EnemyState.active);
                CancelInvoke("GetUp");
                ConfigureLegs(false);
                recovering = false;
            }
            // If it falls and is not already invoking Getup It asks for it to get up
            else if (!IsInvoking("GetUp")) Invoke("GetUp", recoverTime + Random.Range(-recoverTimeRand, recoverTimeRand));
            return;
        }

        if (state == EnemyState.recovering)
        {
            // Checks if the root of the bot is grounded
            bool rootGrounded = Physics.CheckSphere(root.position, 0.5f, botIk.ground);
            if (distanceFromGround < botIk.heightAboveGround || rootGrounded)
            {
                headRb.AddForce(Vector3.up * balancingForce * recoveryForce * 1.1f);
                rootRb.AddForce(Vector3.up * balancingForce * recoveryForce * 0.9f);
            }

            // Checks to see if the Root angle is bellow the getUpAngle and the bot spine is not moving anymore or the distance from the ground is higher than IDK WHAT THIS CHECKS ACTUALLY
            if ((rootAngle < getUpAngle && spineRb.velocity.magnitude < getUpMagT) || (distanceFromGround > botIk.heightAboveGround * 0.85f && distanceFromGround < botIk.heightAboveGround * 1.85f && rootAngle < 30f))
            {
                UpdateState(EnemyState.active);
                CancelInvoke("FinishRecovery");
                Invoke("FinishRecovery", 2f);
            }
            return;
        }
        // If the bot is not moving and distance from the ground is higher than the Ik distance from the ground but not than the IKDFG +10%
        if (state == EnemyState.active && rootRb.velocity.magnitude < 1f && distanceFromGround > botIk.heightAboveGround && distanceFromGround < botIk.heightAboveGround + botIk.heightAboveGround * 0.1f)
        {
            headRb.AddForce(Vector3.up * balancingForce * 0.86f);
            return;
        }

        // No ideia what num 3 is
        float num3 = Mathf.Clamp(1f - RootHeight() / botIk.heightAboveGround, -1f, 1f);

        // This is just verifiying if the root angle to see the bot state 
        if (rootAngle < tumbleAngle) UpdateState(EnemyState.active);
        else if (rootAngle < fallAngle) UpdateState(EnemyState.tumbling);
        else if (rootAngle > fallAngle) UpdateState(EnemyState.falling);


        if (legGrounded)
        {
            rootRb.AddForce(root.up * balancingForce * num3 * 2f);
            rootRb.AddForce(root.up * balancingForce * legPushForce);
        }
        if (distanceFromGround < botIk.heightAboveGround * 2f) StabilizingBody();
    }


    private void GetMainRigidBodies()
    {
        rootRb = root.GetComponent<Rigidbody>();
        spineRb = spine.GetComponent<Rigidbody>();
        headRb = head.GetComponent<Rigidbody>();
    }

    private void GetGroundCheckPositions()
    {
        for (int i = 0; i < legAmmount; i++) { groundChecks[i] = botIk.legs[i].transform; }
    }

    /// <summary> This will check if at least one leg is grounded </summary>
    private void CheckIfLegsGrounded()
    {
        legGrounded = false;
        for (int i = 0; i < legAmmount; i++)
        {
            if (Physics.CheckSphere(groundChecks[i].position, legGroundCheckRadius, botIk.ground)) legGrounded = true;
        }
    }

    /// <summary> This calculates the force necessary to balance the ragdoll using the mass of all rbs and the gravity </summary>
    private void CalculateBalancingForce()
    {
        float mass = 0f;
        Rigidbody[] botRigidBodies = GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rigidbody in botRigidBodies)
        {
            if (!rigidbody.isKinematic)
            {
                mass += rigidbody.mass;
            }
        }
        balancingForce = Mathf.Abs(mass * Physics.gravity.y);
    }


    /// <summary> This is used to update the bot state </summary>
    private void UpdateState(EnemyState s)
    {
        if (state == s) return;
        state = s;
        switch (s)
        {
            case EnemyState.active:
                ConfigureRb(5f, 5f, maxRotationForce, 1f);
                break;
            case EnemyState.tumbling:
                ConfigureRb(1f, 4f, 0f, 0.1f);
                break;
            case EnemyState.falling:
                ConfigureRb(0f, 0f, 0f, 0f);
                BotFell();
                break;
            case EnemyState.recovering:
                ConfigureRb(4f, 4f, maxRotationForce, 0.15f);
                break;
            case EnemyState.dead:
                ConfigureRb(0f, 0f, 0f, 0f);
                KillRigidEnemy();
                break;
            default:
                rootRb.drag = 0f;
                rootRb.angularDrag = 0f;
                break;
        }

    }

    /// <summary> This is used to update the rigidBody configuration depending on the state of the bot </summary>
    private void ConfigureRb(float drag, float angularDrag, float rotation, float stabilizerMultiplier)
    {
        if (drag != -1f)
        {
            rootRb.drag = drag;
            spineRb.drag = drag;
        }
        if (angularDrag != -1f)
        {
            rootRb.angularDrag = angularDrag;
            spineRb.angularDrag = angularDrag;
        }
        if (rotationForce != -1f)
        {
            rotationForce = rotation;
        }
        if (stabilizerMultiplier != -1f)
        {
            stateForceMultiplier = stabilizerMultiplier;
        }
    }

    private void ConfigureLegs(bool makeRagdoll)
    {
        if (makeRagdoll == isRagdoll) return;
        isRagdoll = makeRagdoll;

        // Iterates through every leg
        for (int i = 0; i < botIk.legs.Length; i++)
        {
            // Gets the lenght of the LegChain
            int currentLegChainLenght = botIk.legs[i].ChainLength;
            Transform parent = botIk.legs[i].transform;
            while (currentLegChainLenght > 0)
            {
                // As it should start from the feet this will iterate through every bone after the feet
                parent = parent.parent;
                // Adds a hinge joint if should ragdoll
                if (makeRagdoll) parent.gameObject.AddComponent<HingeJoint>().connectedBody = parent.parent.GetComponent<Rigidbody>();

                // Removes the hinge joint if should not ragdoll
                else UnityEngine.Object.Destroy(parent.gameObject.GetComponent<Joint>());
                currentLegChainLenght--;
            }

            // This will configure The legs rigidbody
            Rigidbody[] componentsInChildren = parent.GetComponentsInChildren<Rigidbody>();
            foreach (Rigidbody obj in componentsInChildren)
            {
                obj.isKinematic = !makeRagdoll;
                obj.interpolation = (makeRagdoll ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None);
            }
            // Disables or enables Inverse kinematics on the legs
            botIk.legs[i].enabled = !makeRagdoll;
            botIk.ForceCurrentPosition(i);
        }
    }

    /// <summary> Returns the root's current height from the ground
    private float RootHeight()
    {
        if (Physics.Raycast(root.position, Vector3.down, out var hitInfo, 10f, botIk.ground)) return hitInfo.distance;
        return 0f;
    }

    private void StabilizingBody()
    {
        headRb.AddForce(Vector3.up * balancingForce * stateForceMultiplier);
        spineRb.AddForce(Vector3.down * balancingForce * stateForceMultiplier);
    }

    private void BotFell()
    {
        UpdateState(EnemyState.falling);
        ConfigureLegs(true);
        recovering = true;
        Invoke("GetUp", recoverTime * Random.Range(-recoverTimeRand, recoverTimeRand));
    }

    public void KillRigidEnemy()
    {
        DisableSelfCollision(false);
        ConfigureLegs(true);
        CancelInvoke();
        botIk.DestroyTargets();
    }

    private void GetUp()
    {
        if (Physics.CheckSphere(root.position, botIk.heightAboveGround * 0.5f, botIk.ground))
        {
            UpdateState(EnemyState.recovering);
            ConfigureLegs(false);
        }
        else Invoke("GetUp", recoverTime);
    }

    private void FinishRecovery()
    {
        recovering = false;
    }

    public Vector3 GetVelocity()
    {
        if (!rootRb)

            return Vector3.zero;

        Vector3 result = rootRb.velocity * moveLegsWithSpeedScale;
        if (result.magnitude > 1f)
        {
            return result.normalized;
        }
        return result;
    }

    private void DisableSelfCollision(bool ignore)
    {
        Collider[] botColliders = GetComponentsInChildren<Collider>();
        for (int i = 0; i < botColliders.Length; i++)
        {
            if (botColliders[i].gameObject.CompareTag("Ignore") && !ignore) continue;

            for (int j = i; j < botColliders.Length; j++)
            {
                if (!botColliders[j].gameObject.CompareTag("Ignore") || ignore)
                {
                    Physics.IgnoreCollision(botColliders[i], botColliders[j], ignore);
                }
            }
        }

    }

    public void RotateBody(Vector3 dir)
    {
        float y = root.transform.eulerAngles.y;
        float y2 = Quaternion.LookRotation(dir).eulerAngles.y;
        float value = Mathf.DeltaAngle(y, y2);
        value = Mathf.Clamp(value, -2f, 2f);
        rootRb.AddTorque(Vector3.up * value * balancingForce * rotationForce);
    }

    public void MoveBody(Vector3 dir, float moveSpeed)
    {
        rootRb.AddForce(dir * moveSpeed * rootRb.mass);
        headRb.AddForce(dir * moveSpeed * headRb.mass);
        spineRb.AddForce(dir * moveSpeed * spineRb.mass);
    }
}
