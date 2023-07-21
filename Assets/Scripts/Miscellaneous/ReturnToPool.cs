using UnityEngine;

public class ReturnToPool : MonoBehaviour
{
    private enum ObjectType
    {
        Tracer,
        HitParticle,
    }

    [Header("Settings")]
    [SerializeField] ObjectType objectType;
    [SerializeField] TracerType tracerType;
    [SerializeField] public float returnIn;
    [SerializeField] public bool callReturnOnEnable;

    private void OnEnable()
    {
        if (callReturnOnEnable) ReturnToPoolIn(returnIn);
    }

    public void ReturnToPoolIn(float time)
    {
        Invoke("ReturnObjectToPool", time);
    }

    private void ReturnObjectToPool()
    {
        switch (objectType)
        {
            case ObjectType.Tracer:
                PoolingManager.Instance.ReturnBulletTracer(tracerType, this.gameObject);
                break;
            case ObjectType.HitParticle:
                PoolingManager.Instance.ReturnHitParticle(tracerType, this.gameObject);
                break;
        }
    }
}
