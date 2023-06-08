using UnityEngine;

public class GunComponents : MonoBehaviour
{
    public Guns gunSettings;
    public GunSway gunSway;
    public Transform barrelTip;
    public Transform gunModelPos;
    public MeshRenderer[] gunMesh;
    public SkinnedMeshRenderer[] armMesh;
    public MeshRenderer scopeMesh;
    public TrailRenderer gunTrail;
    public Animator animator;
    public ParticleSystem muzzleFlash;
    public AudioClip[] gunShootSounds;

    [Header("ScreenShake")]
    public float shakeIntensity;
    public float shakeRoughness;
    public float fadeinTime;
    public float fadeOutTime;
    public float shakeAmmount;
}
