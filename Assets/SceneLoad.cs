using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneLoad
{
    // 追加：ロード画面を経由しない直接遷移中かどうかを判定するフラグ
    public static bool isDirectTransition = false;

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
        // ロード画面を使わない場合、フラグを立てる
        if (!useLoadingScene)
        {
            isDirectTransition = true;
        }

        SceneFadeOut fadeOut = Object.FindFirstObjectByType<SceneFadeOut>();

        if (fadeOut != null)
        {
            fadeOut.Transition(nextSceneName, useLoadingScene, fadeType);
            return;
        }

        // フェードUIがないシーン向けの保険
        PlayerPrefs.SetInt("FadeType", (int)fadeType); // 直接遷移時でも色を引き継げるように外に出す

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
}