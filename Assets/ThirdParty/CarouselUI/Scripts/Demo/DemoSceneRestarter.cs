using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarouselUI.Demo
{
    public class DemoSceneRestarter : MonoBehaviour
    {
        [SerializeField] private PreferenceTracker _preferenceTracker;
        [SerializeField] private GameObject _uiCanvas;

        public async void ResetScene()
        {
            _uiCanvas.SetActive(false);

            _preferenceTracker.ResetAllPeferenceToZero(); //RESETS ALL TO ZERO

            await Task.Delay(250);

            _uiCanvas.SetActive(true); //RESETS SHOWN VALUES
        }

        public void EndGame()
        {
            Debug.LogError("Quit Game");
            Application.Quit();
        }

    }
}