using UnityEngine;
using UnityEngine.UI;

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
    [HideInInspector] public int currentAmmo;
    public Gunslot slot;
    public WeaponType weaponType;
    public GameObject gunModel;
    public Sprite gunIcon;
    public bool canAim;
    [Range(1, 120)] public float scopeFov;
    public int damage;
    public int range;
    public float fireRate;
    public float reloadTime;
    public int reloadSpins;
    public int maxAmmo;
    public int recoilForce;
    public int maxRecoilDistance;

    [Space(10)]
    [Header("If is shotgun")]
    public bool isShotgun;
    public int pellets;
    public float spread;


    private void Awake()
    {
        currentAmmo = maxAmmo;
    }
}