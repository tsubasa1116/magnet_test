using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverTransition : MonoBehaviour
{
    public static Texture2D LastFrame { get; private set; }

    [Header("フェード")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeDuration = 0.7f;

    [Range(0f, 1f)]
    [SerializeField] private float finalBlackAlpha = 0.55f;

    [Header("遷移先")]
    [SerializeField] private string gameOverSceneName = "GameOverScene";

    private bool isTransitioning;

    void Awake()
    {
        fadeCanvasGroup.alpha = 0f;
    }

    public void GoToGameOver()
    {
        if (isTransitioning) return;

        StartCoroutine(GameOverRoutine());
    }

    private IEnumerator GameOverRoutine()
    {
        isTransitioning = true;

        // 1. 死亡画面を黒半透明までフェード
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            fadeCanvasGroup.alpha = Mathf.Lerp(
                0f,
                finalBlackAlpha,
                elapsed / fadeDuration
            );

            yield return null;
        }

        fadeCanvasGroup.alpha = finalBlackAlpha;

        // 2. 黒フェード完了後の画面を撮影
        yield return new WaitForEndOfFrame();

        if (LastFrame != null)
        {
            Destroy(LastFrame);
        }

        Texture2D captured = ScreenCapture.CaptureScreenshotAsTexture();

        // 撮影画像を必ず不透明にして、背後の白と混ざらないようにする
        Color32[] pixels = captured.GetPixels32();

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i].a = 255;
        }

        captured.SetPixels32(pixels);
        captured.Apply();

        LastFrame = captured;

        // 3. 通常のシーン遷移
        SceneManager.LoadScene(gameOverSceneName);
    }
}