using UnityEngine;

public class MeleeComponents : MonoBehaviour
{
    [Header("Required Components")]
    [SerializeField] public Guns meleeSettings;
    [SerializeField] public MeshRenderer[] meleeMesh;
    [SerializeField] public Animator animator;
    [SerializeField] private AudioSource humSource;

    [Header("Extra Components")]
    [SerializeField] public Transform rightArmTarget;
    [SerializeField] public Transform leftArmTarget;

    private void Awake()
    {
        if (!meleeSettings.weaponHum) return;
        humSource.clip = meleeSettings.weaponHum;
        humSource.loop = true;
        humSource.Play();
    }
}
