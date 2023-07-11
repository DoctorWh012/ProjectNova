using UnityEngine;

public class MeleeComponents : MonoBehaviour
{
    public Guns meleeSettings;
    public Transform gunModelPos;
    public MeshRenderer[] meleeMesh;
    [SerializeField] public Transform rightArmTarget;
    [SerializeField] public Transform leftArmTarget;
    public Animator animator;
    public AudioClip[] meleeSounds;
}
