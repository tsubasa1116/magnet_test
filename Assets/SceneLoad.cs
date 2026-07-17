using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneLoad
{
    // ロード画面あり：従来の呼び出しは黒フェードになる
    public static void LoadWithLoadingScreen(string nextSceneName)
    {
        Transition(nextSceneName, true, FadeType.Black);
    }

    // ロード画面あり：色を指定
    public static void LoadWithLoadingScreen(string nextSceneName, FadeType fadeType)
    {
        Transition(nextSceneName, true, fadeType);
    }

    // ロード画面なし：色を指定して直接遷移
    public static void LoadDirect(string nextSceneName, FadeType fadeType)
    {
        Transition(nextSceneName, false, fadeType);
    }

    private static void Transition(
        string nextSceneName,
        bool useLoadingScene,
        FadeType fadeType)
    {
        SceneFadeOut fadeOut = Object.FindFirstObjectByType<SceneFadeOut>();

        if (fadeOut != null)
        {
            fadeOut.Transition(nextSceneName, useLoadingScene, fadeType);
            return;
        }

        // フェードUIがないシーン向けの保険
        if (useLoadingScene)
        {
            PlayerPrefs.SetString("NextScene", nextSceneName);
            PlayerPrefs.SetInt("FadeType", (int)fadeType);
            PlayerPrefs.Save();
            SceneManager.LoadSceneAsync("LoadingScene");
        }
        else
        {
            SceneManager.LoadSceneAsync(nextSceneName);
        }
    }
}