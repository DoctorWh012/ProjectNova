using TMPro;
using UnityEngine;

public class GunSpawn : MonoBehaviour
{
    public int gunIndex { get; private set; }

    [Header("Components")]
    [SerializeField] GameObject[] pickableGuns;
    [SerializeField] Guns[] pickableGunsSettings;
    [SerializeField] Transform gunHolder;
    [SerializeField] TextMeshProUGUI idText;

    [Header("Settings")]
    [SerializeField] public int gunSpawnDelay;
    [SerializeField] private int gunSpawnDelayAfterPickUp;

    private bool gunAvailable;
    public int gunSpawnerIndex;

    void Start()
    {
        idText.SetText($"#{gunSpawnerIndex}");
    }

    public void StartGunSpawnTimer(int inWait)
    {
        InvokeRepeating("SpawnNewGun", inWait, gunSpawnDelay);
    }

    public void DespawnGun()
    {
        pickableGuns[gunIndex].SetActive(false);

        if (!NetworkManager.Singleton.Server.IsRunning) return;
        gunAvailable = false;
        CancelInvoke("SpawnNewGun");
        StartGunSpawnTimer(gunSpawnDelayAfterPickUp);

        GunSpawnManager.Instance.SendGunDespawnMessage(gunSpawnerIndex);
    }

    //Server Gun Spawn
    private void SpawnNewGun()
    {
        pickableGuns[gunIndex].SetActive(false);

        gunIndex = UnityEngine.Random.Range(0, pickableGuns.Length);
        pickableGuns[gunIndex].SetActive(true);
        gunAvailable = true;
        GunSpawnManager.Instance.SendGunSpawnMessage(gunSpawnerIndex, gunIndex);
    }

    //Client Gun Spawn
    public void SpawnNewGun(int index)
    {
        pickableGuns[gunIndex].SetActive(false);

        gunIndex = index;
        pickableGuns[gunIndex].SetActive(true);
    }

    public void PickUpTheGun(Player player)
    {
        if (!gunAvailable) return;
        if (NetworkManager.Singleton.Server.IsRunning)
        {
            player.gunShoot.PickUpGun(((int)pickableGunsSettings[gunIndex].slot), gunIndex);
            DespawnGun();
        }
    }
}
