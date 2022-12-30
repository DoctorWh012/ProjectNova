using UnityEngine;

public class MeleeComponents : MonoBehaviour
{
    public Guns meleeSettings;
    public Transform gunModelPos;
    public MeshRenderer[] meleeMesh;
    public Animator animator;
    public ParticleSystem meleeParticles;
    public AudioClip[] melleSounds;
    public AudioSource audioSource;
}
