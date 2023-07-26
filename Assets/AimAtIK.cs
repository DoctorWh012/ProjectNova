using UnityEngine;

public class AimAtIK : MonoBehaviour
{
    [System.Serializable]
    private class BonesWeights
    {
        public Transform bonePos;
        [Range(0, 1)]
        public float boneWeight;
    }

    [Header("Components")]
    [SerializeField] private BonesWeights[] bonesWeights;
    [SerializeField] private Transform target;
    [SerializeField] private Transform weapon;

    [Header("Settings")]
    [Range(1, 10)]
    [SerializeField] private int iterations;
    [Range(0, 1)]
    [SerializeField] private float weight;
    [SerializeField] private float angleLimit = 90f;
    [SerializeField] private float distaceLimit = 1.5f;

    Quaternion aimTowards;
    Quaternion blendedRot;
    float targetAngle;
    float blendOut;
    Vector3 direction;

    private void LateUpdate()
    {
        for (int i = 0; i < iterations; i++)
        {
            for (int j = 0; j < bonesWeights.Length; j++)
                AimAtTarget(bonesWeights[j].bonePos, target.position, bonesWeights[j].boneWeight * weight);
        }
    }

    private void AimAtTarget(Transform bone, Vector3 targetPos, float weight)
    {

        aimTowards = Quaternion.FromToRotation(weapon.forward, targetPos - weapon.position);
        blendedRot = Quaternion.Slerp(Quaternion.identity, aimTowards, weight);
        bone.rotation = blendedRot * bone.rotation;
    }

    private Vector3 GetTargetPos()
    {
        blendOut = 0;
        targetAngle = Vector3.Angle(target.position - weapon.position, weapon.forward);

        if (targetAngle > angleLimit) blendOut += (targetAngle - angleLimit) / 50f;

        if ((target.position - weapon.position).magnitude < distaceLimit) blendOut += distaceLimit - (target.position - weapon.position).magnitude;

        direction = Vector3.Slerp(target.position - weapon.position, weapon.forward, blendOut);
        return weapon.forward + direction;
    }
}
