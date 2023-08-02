using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum Gunslot : int
{
    primary = 0,
    secondary = 1,
    melee = 2,
}
public enum WeaponType
{
    rifle,
    shotgun,
    melee,
}

public enum TracerType
{
    yellow,
    red,
}

[CreateAssetMenu(fileName = "Guns", menuName = "RUSP/Guns", order = 0)]
public class Guns : ScriptableObject
{
    public float tickFireRate { get; private set; }

    [Header("Weapon")]
    [SerializeField] public Gunslot slot;
    [SerializeField] public WeaponType weaponType;
    [SerializeField] public GameObject gunModel;
    [SerializeField] public Sprite gunIcon;
    [SerializeField] public string gunName;

    [Header("Damage")]
    [SerializeField] public int damage;
    [Range(1, 255)] public int range;
    [SerializeField] public float fireRate;

    [Header("Reload")]
    [SerializeField] public float reloadTime;
    [SerializeField] public int reloadSpins;
    [SerializeField] public int maxAmmo;
    [HideInInspector] public int currentAmmo;

    [Header("Recoil")]
    [SerializeField] public float knockbackForce;
    [SerializeField] public float knockbackMaxHitDistance;

    [Header("Audio")]
    [SerializeField] public AudioClip weaponHum;
    [SerializeField] public AudioClip weaponPickupSound;
    [SerializeField] public AudioClip weaponSpinSound;
    [SerializeField] public AudioClip weaponReloadSound;
    [Range(0, 1)]
    [SerializeField] public float weaponShootingSoundVolume = 1;
    [SerializeField] public AudioClip[] weaponShootingSounds;

    [Header("Tracer")]
    [SerializeField] public TracerType tracerType;
    [SerializeField] public float tracerLasts;

    [Header("Aiming")]
    [SerializeField] public bool canAim;
    [HideInInspector]
    [Range(1, 120)] public float scopeFov;

    [Header("Shotgun")]
    [SerializeField] public bool isShotgun;
    [HideInInspector] public int pellets;
    [HideInInspector] public float spread;
    [HideInInspector] public Vector2[] spreadPatterns;

    private void Awake()
    {
        currentAmmo = maxAmmo;
        tickFireRate = (1f / fireRate) / (1f / NetworkManager.ServerTickRate);
    }

    public void CreateGaussianDistribution()
    {
        Debug.Log($"Recalculated spread for {this.name}");
        spreadPatterns = new Vector2[pellets];
        GaussianDistribution gaussianDistribution = new GaussianDistribution();

        for (int i = 0; i < pellets; i++)
        {
            spreadPatterns[i] = new Vector2(gaussianDistribution.Next(0f, 1, -1, 1), gaussianDistribution.Next(0f, 1, -1, 1));
        }
    }
}

#if (UNITY_EDITOR)
[CustomEditor(typeof(Guns))]
public class MyScriptableObjectEditor : Editor
{
    private SerializedProperty shotgunSpreadPatternsProperty;
    private void OnEnable()
    {
        shotgunSpreadPatternsProperty = serializedObject.FindProperty("spreadPatterns");
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        Guns scriptableObject = (Guns)target;

        if (scriptableObject.canAim) scriptableObject.scopeFov = EditorGUILayout.Slider("Scope FOV", scriptableObject.scopeFov, 1, 120);

        if (scriptableObject.isShotgun)
        {
            scriptableObject.pellets = EditorGUILayout.IntField("Pellets", scriptableObject.pellets);
            scriptableObject.spread = EditorGUILayout.FloatField("Spread", scriptableObject.spread);
            EditorGUILayout.PropertyField(shotgunSpreadPatternsProperty, true);
            if (GUILayout.Button("RecalculateSpread")) scriptableObject.CreateGaussianDistribution();
        }
    }
}

#endif
