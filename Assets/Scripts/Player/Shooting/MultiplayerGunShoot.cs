using Riptide;
using UnityEngine;

public class MultiplayerGunShoot : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] Player player;
    [SerializeField] GunShoot gunShoot;
    // [SerializeField] PlayerShooting playerShooting;

    [Header("Keybinds")]
    [SerializeField] private KeyCode shootBTN;
    [SerializeField] private KeyCode aimBTN;
    [SerializeField] private KeyCode reloadKey;
    [SerializeField] private KeyCode primaryGun;
    [SerializeField] private KeyCode secondaryGun;
    [SerializeField] private KeyCode meleeWeapon;

    private bool shooting;
    private bool aiming;
    private float timer;

    private void Start()
    {
        SendGunSettings(0);
    }

    // Update is called once per frame
    void Update()
    {
        GetInput();

        gunShoot.AimDownSight(aiming);

        timer += Time.deltaTime;
        while (timer >= GameManager.Singleton.minTimeBetweenTicks)
        {
            timer -= GameManager.Singleton.minTimeBetweenTicks;

            if (shooting) gunShoot.FireTick();
            if (!NetworkManager.Singleton.Server.IsRunning) SendShootMessage(shooting);
        }
    }

    private void GunSwitchInput(KeyCode keybind, int index)
    {
        if (Input.GetKeyDown(keybind))
        {
            gunShoot.SwitchGun(index, true);
            if (GameManager.Singleton.networking) SendGunSettings(index);
        }
    }

    private void GetInput()
    {
        if (!UIManager.Instance.focused)
        {
            shooting = false;
            aiming = false;
            return;
        }

        shooting = Input.GetKey(shootBTN);
        aiming = Input.GetKey(aimBTN);

        GunSwitchInput(primaryGun, 0);
        GunSwitchInput(secondaryGun, 1);
        GunSwitchInput(meleeWeapon, 2);

        if (Input.GetKeyDown(reloadKey))
        {
            player.GunShoot.StartGunReload();
            if (!NetworkManager.Singleton.Server.IsRunning) SendReload();
        }
    }

    private void SendShootMessage(bool isShooting)
    {
        if (!GameManager.Singleton.networking) return;
        Message message = Message.Create(MessageSendMode.Reliable, ClientToServerId.gunInput);
        message.AddBool(isShooting);
        message.AddUShort(GameManager.Singleton.serverTick);
        NetworkManager.Singleton.Client.Send(message);
    }

    private void SendReload()
    {
        if (!GameManager.Singleton.networking) return;
        Message message = Message.Create(MessageSendMode.Reliable, ClientToServerId.gunReload);
        NetworkManager.Singleton.Client.Send(message);
    }

    public void SendGunSettings(int index)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ClientToServerId.gunChange);
        message.AddInt(index);
        NetworkManager.Singleton.Client.Send(message);
    }
}
