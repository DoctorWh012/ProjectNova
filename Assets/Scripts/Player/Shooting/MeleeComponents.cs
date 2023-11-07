using UnityEngine;

public class MeleeComponents : MonoBehaviour
{
    [Header("Required Components")]
    [SerializeField] public Guns meleeSettings;
    [SerializeField] public MeshRenderer[] meleeMesh;
    [SerializeField] public Animator animator;

    [Header("Extra Components")]
    [SerializeField] public Transform rightArmTarget;
    [SerializeField] public Transform leftArmTarget;

    private void Start()
    {
        animator.keepAnimatorStateOnDisable = true;
    }
}
