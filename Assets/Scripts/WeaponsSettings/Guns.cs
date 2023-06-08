using UnityEngine;
using UnityEditor;

public enum Gunslot : int
{
    primary = 0,
    secondary = 1,
    melee = 2,
}
public enum WeaponType : int
{
    rifle,
    shotgun,
    melee,
}

[CreateAssetMenu(fileName = "Guns", menuName = "RUSP/Guns", order = 0)]
public class Guns : ScriptableObject
{
    [Header("Weapon")]
    public Gunslot slot;
    public WeaponType weaponType;
    public GameObject gunModel;
    public Sprite gunIcon;
    public string gunName;

    [Header("Aiming")]
    public bool canAim;
    [Range(1, 120)] public float scopeFov;

    [Header("Damage")]
    public int damage;
    [Range(1, 255)] public int range;
    public float fireRate;

    [Header("Reload")]
    public float reloadTime;
    public int reloadSpins;
    public int maxAmmo;
    [HideInInspector] public int currentAmmo;

    [Header("Recoil")]
    public int recoilForce;
    public int maxRecoilDistance;

    [Header("Shotgun")]
    public bool isShotgun;
    public int pellets;
    public float spread;
    public Vector2[] shotgunSpreadPatterns;


    private void Awake()
    {
        currentAmmo = maxAmmo;
        Debug.Log($"Awake for {this.name}");
    }

    public void CreateGaussianDistribution()
    {
        Debug.Log($"Recalculated spread for {this.name}");
        shotgunSpreadPatterns = new Vector2[pellets];
        GaussianDistribution gaussianDistribution = new GaussianDistribution();

        for (int i = 0; i < pellets; i++)
        {
            shotgunSpreadPatterns[i] = new Vector2(gaussianDistribution.Next(0f, 1, -1, 1), gaussianDistribution.Next(0f, 1, -1, 1));
        }
    }
}

#if (UNITY_EDITOR)
[CustomEditor(typeof(Guns))]
public class MyScriptableObjectEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        Guns scriptableObject = (Guns)target;
        if (scriptableObject.isShotgun)
        {
            if (GUILayout.Button("RecalculateSpread"))
            {
                scriptableObject.CreateGaussianDistribution();
            }
        }
    }
}

#endif
