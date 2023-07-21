using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RigidBodyFollow : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Transform pos;
    [SerializeField] private Rigidbody follower;

    // Update is called once per frame
    void FixedUpdate()
    {
        follower.MovePosition(pos.position);
    }
}
