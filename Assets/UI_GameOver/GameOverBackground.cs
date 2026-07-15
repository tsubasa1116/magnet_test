using UnityEngine;
using UnityEngine.UI;

public class GameOverBackground : MonoBehaviour
{
    [SerializeField] private RawImage backgroundImage;
    [SerializeField] private CanvasGroup blackOverlay;

    void Start()
    {
        backgroundImage.texture = GameOverTransition.LastFrame;

        // 遷移元のフェード終了位置と同じ濃さ
        blackOverlay.alpha = 0.94f;
    }
}