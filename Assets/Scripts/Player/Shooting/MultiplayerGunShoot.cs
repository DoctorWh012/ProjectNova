using System.Collections;
using System.Collections.Generic;
using Riptide;
using UnityEngine;

public class MultiplayerGunShoot : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] PlayerShooting playerShooting;

    [Header("Keybinds")]
    [SerializeField] private KeyCode shootBTN;
    [SerializeField] private KeyCode aimBTN;
    [SerializeField] private KeyCode reloadKey;
    [SerializeField] private KeyCode primaryGun;
    [SerializeField] private KeyCode secondaryGun;
    [SerializeField] private KeyCode meleeWeapon;

    // Update is called once per frame
    void Update()
    {
        if (!UIManager.Instance.focused) return;
        if (Input.GetKeyDown(shootBTN))
        {
            SendShootMessage(true);
        }
        if (Input.GetKeyUp(shootBTN))
        {
            SendShootMessage(false);
        }

        if (Input.GetKeyDown(aimBTN))
        {
            playerShooting.AimDownSight(true);
        }
        if (Input.GetKeyUp(aimBTN))
        {
            playerShooting.AimDownSight(false);
        }


        if (playerShooting.isReloading) return;

        GetInput(primaryGun, 0);
        GetInput(secondaryGun, 1);
        GetInput(meleeWeapon, 2);

        if (Input.GetKeyDown(reloadKey))
        {
            SendReload();
        }
    }

    private void GetInput(KeyCode keybind, int index)
    {
        if (Input.GetKeyDown(keybind)) playerShooting.SendGunSettings(index);
    }

    private void SendShootMessage(bool isShooting)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ClientToServerId.gunInput);
        message.AddBool(isShooting);
        NetworkManager.Singleton.Client.Send(message);
    }

    private void SendReload()
    {
        StartCoroutine(WaitBeforeReload());
        Message message = Message.Create(MessageSendMode.Reliable, ClientToServerId.gunReload);
        NetworkManager.Singleton.Client.Send(message);
    }

    [MessageHandler((ushort)ServerToClientId.gunReload)]
    private static void Reload(Message message)
    {
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            player.playerShooting.DoTheSpin(message.GetInt(), message.GetFloat());
        }
    }

    private IEnumerator WaitBeforeReload()
    {
        yield return new WaitForSeconds(0.2f);
    }
}
