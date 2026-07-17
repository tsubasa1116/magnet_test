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
        blackFade.alpha = 0f;
        whiteFade.alpha = 0f;
    }

    public void Transition(string nextSceneName, bool useLoadingScene, FadeType fadeType)
    {
        StartCoroutine(TransitionRoutine(nextSceneName, useLoadingScene, fadeType));
    }

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

        // LoadingScene側でも最後のフェード色を合わせるために保存
        PlayerPrefs.SetInt("FadeType", (int)fadeType);

        if (useLoadingScene)
        {
            PlayerPrefs.SetString("NextScene", nextSceneName);
            PlayerPrefs.Save();

            SceneManager.LoadSceneAsync("LoadingScene");
        }
        else
        {
            SceneManager.LoadSceneAsync(nextSceneName);
        }
    }
}