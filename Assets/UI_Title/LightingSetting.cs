using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class LightingSetting : MonoBehaviour
{
    [SerializeField] private float[] lightingAlpha = { 0.8f, 0.6f, 0.3f, 0.1f, 0.0f };

    void Awake()
    {
        int lightingIndex = PlayerPrefs.GetInt("ConfigSlider_2", 0);
        lightingIndex = Mathf.Clamp(lightingIndex, 0, lightingAlpha.Length - 1);

        GetComponent<CanvasGroup>().alpha = lightingAlpha[lightingIndex];
    }
}