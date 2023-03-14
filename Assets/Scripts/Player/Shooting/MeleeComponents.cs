using UnityEngine;

public class MeleeComponents : MonoBehaviour
{
    public Guns meleeSettings;
    public Transform gunModelPos;
    public MeshRenderer[] meleeMesh;
    public SkinnedMeshRenderer[] armMesh;
    public Animator animator;
    public ParticleSystem meleeParticles;
    public AudioClip[] meleeSounds;
}
