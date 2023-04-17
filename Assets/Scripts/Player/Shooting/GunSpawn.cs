using TMPro;
using UnityEngine;

public class GunSpawn : MonoBehaviour
{
    public int gunIndex { get; private set; }

    [Header("Components")]
    [SerializeField] Guns[] pickableGuns;
    [SerializeField] Transform gunHolder;
    [SerializeField] TextMeshProUGUI idText;

    [Header("Settings")]
    [SerializeField] public int gunSpawnDelay;
    [SerializeField] private int gunSpawnDelayAfterPickUp;

    private GameObject gunDisplay;
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
        Destroy(gunDisplay);

        if (!NetworkManager.Singleton.Server.IsRunning) return;
        CancelInvoke("SpawnNewGun");
        StartGunSpawnTimer(gunSpawnDelayAfterPickUp);

        GunSpawnManager.Instance.SendGunDespawnMessage(gunSpawnerIndex);
    }

    //Server Gun Spawn
    private void SpawnNewGun()
    {
        gunIndex = UnityEngine.Random.Range(0, pickableGuns.Length);

        if (gunDisplay != null) Destroy(gunDisplay);

        gunDisplay = Instantiate(pickableGuns[gunIndex].gunModel);
        gunDisplay.transform.SetParent(gunHolder);
        gunDisplay.transform.localPosition = Vector3.zero;
        OutlineTheGun();

        GunSpawnManager.Instance.SendGunSpawnMessage(gunSpawnerIndex, gunIndex);
    }

    //Client Gun Spawn
    public void SpawnNewGun(int index)
    {
        if (gunDisplay != null) Destroy(gunDisplay);

        gunIndex = index;
        gunDisplay = Instantiate(pickableGuns[index].gunModel);
        gunDisplay.transform.SetParent(gunHolder);
        gunDisplay.transform.localPosition = Vector3.zero;
        OutlineTheGun();
    }

    public void PickUpTheGun(Player player)
    {
        if (gunDisplay == null) return;
        if (NetworkManager.Singleton.Server.IsRunning) player.GunShoot.PickUpGun(((int)pickableGuns[gunIndex].slot), gunIndex);
        DespawnGun();
    }

    private void OutlineTheGun()
    {
        Outline outline = gunDisplay.AddComponent<Outline>();
        outline.OutlineWidth = 15;
        outline.OutlineMode = Outline.Mode.OutlineVisible;
    }
}
