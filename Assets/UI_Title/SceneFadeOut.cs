using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum FadeType
{
    Black,
    White
}


public class SceneFadeOut : MonoBehaviour
{
    [SerializeField] private CanvasGroup blackFade;
    [SerializeField] private CanvasGroup whiteFade;
    [SerializeField] private float fadeDuration = 0.5f;

    void Awake()
    {
        // 直接遷移で新しいシーンに来た場合は、1フレーム目のちらつきを防止するため
        // Startより前のAwake時点で画面を真っ暗（または真っ白）にしておく
        if (SceneLoad.isDirectTransition)
        {
            FadeType fadeType = (FadeType)PlayerPrefs.GetInt("FadeType", (int)FadeType.Black);
            blackFade.alpha = fadeType == FadeType.Black ? 1f : 0f;
            whiteFade.alpha = fadeType == FadeType.White ? 1f : 0f;

            blackFade.blocksRaycasts = fadeType == FadeType.Black;
            whiteFade.blocksRaycasts = fadeType == FadeType.White;
        }
        else
        {
            blackFade.alpha = 0f;
            whiteFade.alpha = 0f;
            blackFade.blocksRaycasts = false;
            whiteFade.blocksRaycasts = false;
        }
    }

    void Start()
    {
        // 直接遷移で読み込まれたシーンなら、フェードイン（アルファ1→0）を開始する
        if (SceneLoad.isDirectTransition)
        {
            SceneLoad.isDirectTransition = false; // フラグをリセット
            FadeType fadeType = (FadeType)PlayerPrefs.GetInt("FadeType", (int)FadeType.Black);
            StartCoroutine(FadeInRoutine(fadeType));
        }
    }

    public void Transition(string nextSceneName, bool useLoadingScene, FadeType fadeType)
    {
        StartCoroutine(TransitionRoutine(nextSceneName, useLoadingScene, fadeType));
    }

    // 画面を覆い隠すフェードアウト（アルファ0→1）
    private IEnumerator TransitionRoutine(
        string nextSceneName,
        bool useLoadingScene,
        FadeType fadeType)
    {
        CanvasGroup fade = fadeType == FadeType.Black ? blackFade : whiteFade;
        fade.blocksRaycasts = true;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            fade.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
            yield return null;
        }

        fade.alpha = 1f; // 確実に1にする

        // LoadingScene側や次のシーンでフェード色を合わせるために保存
        PlayerPrefs.SetInt("FadeType", (int)fadeType);

        if (useLoadingScene)
        {
            PlayerPrefs.SetString("NextScene", nextSceneName);
            PlayerPrefs.Save();
            SceneManager.LoadSceneAsync("LoadingScene");
        }
        else
        {
            PlayerPrefs.Save();
            SceneManager.LoadSceneAsync(nextSceneName);
        }
    }

    // 新しいシーンを開けた時に画面を表示するフェードイン（アルファ1→0）
    private IEnumerator FadeInRoutine(FadeType fadeType)
    {
        CanvasGroup fade = fadeType == FadeType.Black ? blackFade : whiteFade;
        fade.blocksRaycasts = true; // フェード中はクリック無効化

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            fade.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }

        fade.alpha = 0f;
        fade.blocksRaycasts = false; // 終わったらクリックを有効化
    }
}