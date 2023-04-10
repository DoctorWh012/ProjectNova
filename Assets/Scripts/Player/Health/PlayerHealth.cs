using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Player player;
    [SerializeField] private PlayerShooting playerShooting;
    [SerializeField] private MultiplayerGunShoot gunController;
    [SerializeField] private HeadBobController headBob;
    [SerializeField] private GameObject[] playerModels;

    public void Die()
    {
        DisableEnableModels(false);
        if (!player.IsLocal) return;
        player.Movement.FreezePlayerMovement(true);
        gunController.enabled = false;
        headBob.enabled = false;
    }

    public void Respawn()
    {
        DisableEnableModels(true);
        if (!player.IsLocal) return;
        player.Movement.FreezePlayerMovement(false);
        gunController.enabled = true;
        headBob.enabled = true;
    }

    private void DisableEnableModels(bool status)
    {
        foreach (GameObject model in playerModels) { model.SetActive(status); }
        if (!status) { playerShooting.DisableAllGunMeshes(); playerShooting.DisableAllMeleeMesh(); return; }
        playerShooting.EnableActiveGunMesh(playerShooting.activeGun);
    }
}
