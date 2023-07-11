using Riptide;
using UnityEngine;

public class MultiplayerGunShoot : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] GunShoot gunShoot;

    [Header("Keybinds")]
    [SerializeField] private KeyCode shootBTN;
    [SerializeField] private KeyCode aimBTN;
    [SerializeField] private KeyCode reloadKey;
    [SerializeField] private KeyCode primaryGun;
    [SerializeField] private KeyCode secondaryGun;
    [SerializeField] private KeyCode meleeWeapon;

    private bool shooting;
    private float timer;

    // Update is called once per frame
    void Update()
    {
        GetInput();

        timer += Time.deltaTime;
        while (timer >= GameManager.Singleton.minTimeBetweenTicks)
        {
            timer -= GameManager.Singleton.minTimeBetweenTicks;

            if (shooting) gunShoot.FireTick();
            if (!NetworkManager.Singleton.Server.IsRunning) SendShootMessage(shooting);
        }
    }

    private void GetInput()
    {
        if (!UIManager.Instance.focused)
        {
            shooting = false;
            return;
        }

        shooting = Input.GetKey(shootBTN);
        if (Input.GetKeyDown(aimBTN)) gunShoot.AimDownSight(true);
        if (Input.GetKeyUp(aimBTN)) gunShoot.AimDownSight(false);

        GunSwitchInput(primaryGun, 0);
        GunSwitchInput(secondaryGun, 1);
        GunSwitchInput(meleeWeapon, 2);

        if (Input.GetKeyDown(reloadKey))
        {
            gunShoot.StartGunReload();
            if (!NetworkManager.Singleton.Server.IsRunning) SendReload();
        }
    }

    private void GunSwitchInput(KeyCode keybind, int index)
    {
        if (Input.GetKeyDown(keybind)) SendSlotSwitch(index);

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

    public void SendSlotSwitch(int index)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ClientToServerId.slotChange);
        message.AddInt(index);
        NetworkManager.Singleton.Client.Send(message);
    }
}
