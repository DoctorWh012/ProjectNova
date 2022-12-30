using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.EventSystems;

public class ButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Components")]
    [SerializeField] private TextMeshProUGUI text;

    [Header("Settings")]
    [SerializeField] private float nonHoverFontSize = 24;
    [SerializeField] private float onHoverFontSize = 30;
    [SerializeField] private float stringLerpTime = 0.1f;

    public void OnPointerEnter(PointerEventData eventData)
    {
        StartCoroutine(LerpStringFontSize(onHoverFontSize));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        StartCoroutine(LerpStringFontSize(nonHoverFontSize));
    }

    private IEnumerator LerpStringFontSize(float desiredStringFontSize)
    {
        float startingFontSize = text.fontSize;
        for (float t = 0f; t < 1f; t += Time.deltaTime / stringLerpTime)
        {
            text.fontSize = Mathf.Lerp(startingFontSize, desiredStringFontSize, t);
            yield return null;
        }
        text.fontSize = desiredStringFontSize;
    }
}
