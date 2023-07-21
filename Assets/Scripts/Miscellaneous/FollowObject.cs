using UnityEngine;

public class FollowObject : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Transform pos;
    [SerializeField] private Transform follower;

    void FixedUpdate()
    {
        follower.position = pos.position;
    }
}
