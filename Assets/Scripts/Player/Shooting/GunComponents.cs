using UnityEngine;

public class GunComponents : MonoBehaviour
{
    [Header("Required Components")]
    [SerializeField] public Guns gunSettings;
    [SerializeField] public GunSway gunSway;
    [SerializeField] public Transform barrelTip;
    [SerializeField] public Transform gunModelPos;
    [SerializeField] public MeshRenderer[] gunMesh;
    [SerializeField] public Animator animator;
    [SerializeField] public ParticleSystem muzzleFlash;
    [SerializeField] public TrailRenderer gunTrail;
    [SerializeField] private AudioSource humSource;

    [Header("Extra Components")]
    [SerializeField] public MeshRenderer scopeMesh;
    [SerializeField] public Transform rightArmTarget;
    [SerializeField] public Transform leftArmTarget;

    [Header("ScreenShake")]
    [SerializeField] public float shakeIntensity;
    [SerializeField] public float shakeRoughness;
    [SerializeField] public float fadeinTime;
    [SerializeField] public float fadeOutTime;
    [SerializeField] public float shakeAmmount;

    private void Awake()
    {
        if (!gunSettings.weaponHum) return;
        humSource.clip = gunSettings.weaponHum;
        humSource.loop = true;
        humSource.Play();
    }
}
