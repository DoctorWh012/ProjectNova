using UnityEngine;
using kTools.Pooling;

public class PoolingManager : MonoBehaviour
{
    private static PoolingManager _singleton;
    public static PoolingManager Singleton
    {
        get { return _singleton; }
        set
        {
            if (_singleton == null)
            {
                _singleton = value;
            }

            else if (_singleton != value)
            {
                Debug.Log($"{nameof(PoolingManager)} instance already exists, destroying duplicate");
                Destroy(value);
            }
        }
    }
    [Header("Pooling Components")]
    [Header("Bullets")]
    [SerializeField] public GameObject yellowBulletPrefab;
    [SerializeField] private int yellowBulletsOnStart;

    [SerializeField] public GameObject redBulletPrefab;
    [SerializeField] private int redBulletsOnStart;

    [SerializeField] public GameObject yellowHitParticlePrefab;
    [SerializeField] private int yellowHitParticlesOnStart;

    [SerializeField] public GameObject redHitParticlePrefab;
    [SerializeField] private int redHitParticlesOnStart;

    private GameObject instance;

    private void Awake()
    {
        Singleton = this;
    }

    private void Start()
    {
        PoolingSystem.CreatePool(yellowBulletPrefab, yellowBulletPrefab, yellowBulletsOnStart, false);
        PoolingSystem.CreatePool(redBulletPrefab, redBulletPrefab, redBulletsOnStart, false);
        PoolingSystem.CreatePool(yellowHitParticlePrefab, yellowHitParticlePrefab, yellowHitParticlesOnStart, false);
        PoolingSystem.CreatePool(redHitParticlePrefab, redHitParticlePrefab, redBulletsOnStart, false);

    }

    public TrailRenderer GetBulletTracer(TracerType tracerType)
    {
        switch (tracerType)
        {
            case TracerType.yellow:
                if (PoolingSystem.TryGetInstance(yellowBulletPrefab, out instance)) return instance.GetComponent<TrailRenderer>();
                return null;

            case TracerType.red:
                if (PoolingSystem.TryGetInstance(redBulletPrefab, out instance)) return instance.GetComponent<TrailRenderer>();
                return null;

            default:
                return null;
        }
    }

    public ParticleSystem GetHitParticle(TracerType tracerType)
    {
        switch (tracerType)
        {
            case TracerType.yellow:
                if (PoolingSystem.TryGetInstance(yellowHitParticlePrefab, out instance)) return instance.GetComponent<ParticleSystem>();
                return null;

            case TracerType.red:
                if (PoolingSystem.TryGetInstance(redHitParticlePrefab, out instance)) return instance.GetComponent<ParticleSystem>();
                return null;

            default:
                return null;
        }
    }

    public void ReturnBulletTracer(TracerType tracerType, GameObject instance)
    {
        switch (tracerType)
        {
            case TracerType.yellow:
                PoolingSystem.ReturnInstance(yellowBulletPrefab, instance);
                break;
            case TracerType.red:
                PoolingSystem.ReturnInstance(redBulletPrefab, instance);
                break;
        }
    }

    public void ReturnHitParticle(TracerType tracerType, GameObject instance)
    {
        switch (tracerType)
        {
            case TracerType.yellow:
                PoolingSystem.ReturnInstance(yellowHitParticlePrefab, instance);
                break;

            case TracerType.red:
                PoolingSystem.ReturnInstance(redHitParticlePrefab, instance);
                break;
        }
    }
}