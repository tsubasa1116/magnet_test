#if UNITY_EDITOR // エディター上だけで作動する
using UnityEngine;
using UnityEditor;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public static class UltimatePlayModeStopper
{
    // ゲーム再生時に自動でバックグラウンド起動してくれる
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        var go = new GameObject("UltimatePlayModeStopper");
        go.hideFlags = HideFlags.HideAndDontSave; // ヒエラルキーから隠す
        Object.DontDestroyOnLoad(go);             // シーン遷移しても消えないようにする
        go.AddComponent<InputWatcher>();
    }

    private class InputWatcher : MonoBehaviour
    {
        void Update()
        {
            bool trigger = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                trigger = Keyboard.current.f11Key.wasPressedThisFrame;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            if (!trigger)
            {
                trigger = Input.GetKeyDown(KeyCode.F11);
            }
#endif
            if (trigger)
            {
                EditorApplication.isPlaying = false;
            }
        }
    }
}
#endif