using System;
using Riptide;
using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [Header("Connect")]
    [SerializeField] private GameObject[] connectUI;

    private void Update()
    {
        if (NetworkManager.Singleton.Client.Connection != null) DisableUi();
    }

    public void DisableUi()
    {
        foreach (GameObject obj in connectUI)
        {
            obj.SetActive(false);
        }

        this.enabled = false;
    }
}
