using UnityEngine;

public class UISE : MonoBehaviour
{
    private static UISE instance;

    // クリップ未設定の無音フォールバックとして自動生成された個体か
    private bool isAutoCreated;

    // シーンにUISEが無い経路(エディタでゲームシーンから直接プレイ→タイトルへ等)でも
    // UISE.Instance.EnterUI() の呼び出しがNullReferenceで落ちないよう、
    // 未生成なら無音のフォールバックを自動生成して返す。
    // 本物(クリップ設定済み)がシーンから現れたらAwakeで置き換わる。
    public static UISE Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("UISE (auto fallback)");
                UISE se = go.AddComponent<UISE>(); // AddComponent時にAwakeが走りinstanceが入る
                se.isAutoCreated = true;
            }
            return instance;
        }
    }

    [Header("UI SE")]
    [SerializeField] private AudioClip startSE;
    [SerializeField] private AudioClip enterSE;
    [SerializeField] private AudioClip cursorSE;
    [SerializeField] private AudioClip cancelSE;

    [SerializeField] private AudioSource audioSource; // OutputにSEグループを割り当てる

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            // 既に居るのが無音フォールバックで、自分が本物なら置き換える
            if (instance.isAutoCreated && !isAutoCreated)
            {
                Destroy(instance.gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    public void StartUI() => PlaySE(startSE);
    public void EnterUI() => PlaySE(enterSE);
    public void CursorUI() => PlaySE(cursorSE);
    public void CancelUI() => PlaySE(cancelSE);

    private void PlaySE(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }
}
