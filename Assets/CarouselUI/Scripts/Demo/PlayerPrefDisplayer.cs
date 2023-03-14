using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace CarouselUI.Demo
{
    public class PlayerPrefDisplayer : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private PreferenceEnum _preferenceToDisplay;
        [SerializeField] private TextMeshProUGUI _displayText;

        private Coroutine fadeOut;

        private void OnEnable()
        {
            RefreshValue();
        }

        public void RefreshValue()
        {
            _displayText.text = $"{PlayerPrefs.GetInt(_preferenceToDisplay.ToString())}";

            if(fadeOut != null)
            {
                StopCoroutine(fadeOut);
            }

            fadeOut = StartCoroutine(FadeOut());
        }

        private IEnumerator FadeOut()
        {
            _canvasGroup.alpha = 1;

            yield return new WaitForSeconds(3);

            _canvasGroup.alpha = 0;

            yield break;
        }
    }
}