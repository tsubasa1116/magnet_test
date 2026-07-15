using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class Loading : MonoBehaviour
{
    [Header("UI参照")]
    [SerializeField] private CanvasGroup fadeCanvasGroup; 
    [SerializeField] private Image loadingBar; // 進捗用ロードバー
    [SerializeField] private GameObject magnet;     // 回転するマグネット

    [SerializeField] private CanvasGroup loadingText;  // "Loading..."のテキスト
    [SerializeField] public AnimationCurve blinkCurve; // 点滅のアニメーションカーブ
    [SerializeField] public float blinkSpeed = 2.0f;   // 点滅の速度

    [SerializeField] private Sprite[] effectSprites; // エフェクト用のスプライト配列
    [SerializeField] private Image effectMagnet;     // エフェクト用のImageコンポーネント
    [SerializeField] private float effectFps = 30.0f;

    [Header("設定")]
    [SerializeField] private float fadeDuration = 2.0f;  // フェードの時間
    [SerializeField] private float rotationSpeed = 100.0f; // マグネットの回転速度

    void Start()
    {
        StartCoroutine(LoadSceneSequence());

        StartCoroutine(AnimationEffect());
    }

    void Update()
    {
        magnet.transform.Rotate(0f, 0f, -rotationSpeed * Time.deltaTime);

        float t = Mathf.PingPong(Time.time * blinkSpeed, 1.0f);
        loadingText.alpha = blinkCurve.Evaluate(t);
    }

    private IEnumerator LoadSceneSequence()
    {
        // 目的のシーン名をPlayerPrefsから取得
        string targetScene = PlayerPrefs.GetString("NextScene", "TakeTakeScene");

        // 非同期読み込み開始
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(targetScene);
        asyncLoad.allowSceneActivation = false; // 読み込み完了しても勝手に遷移させない

        float visualProgress = 0f;
        float minLoadTime = 2.0f;

        // 読み込み進捗の更新
        while (asyncLoad.progress < 0.9f || visualProgress < 1f)
        {
            // 実際のロード進捗 (0.0 ～ 1.0に正規化)
            float targetProgress = asyncLoad.progress / 0.9f;

            // 見た目の進捗を、指定した時間（minLoadTime）をかけて実際の進捗に滑らかに近づける
            visualProgress = Mathf.MoveTowards(visualProgress, targetProgress, Time.deltaTime / minLoadTime);

            if (loadingBar != null)
            {
                loadingBar.fillAmount = visualProgress;
            }
            yield return null;
        }

        // 読み込み完了（バーを100%にする）
        if (loadingBar != null) loadingBar.fillAmount = 1f;

        yield return new WaitForSeconds(0.5f);

        // ==========================================
        // 修正ポイント：UIの親玉（Canvas）を絶対に消さない
        // ==========================================

        // Canvasの「一番上の親オブジェクト」を取得
        GameObject canvasRoot = fadeCanvasGroup.transform.root.gameObject;

        // UI全体を次のシーンへ持ち越す
        DontDestroyOnLoad(canvasRoot);

        // もしこのスクリプトがCanvasとは別のオブジェクトに付いている時のため、自分自身も持ち越す
        DontDestroyOnLoad(this.transform.root.gameObject);

        // 新しいシーンへの切り替えを許可
        asyncLoad.allowSceneActivation = true;

        // 新しいシーンの準備が「完全に」終わるまで待機
        yield return new WaitUntil(() => asyncLoad.isDone);

        // 新しいシーンが裏に準備された状態で、ゆっくりフェードアウト（0.5秒など）
        yield return StartCoroutine(FadeCanvasGroup(1f, 0f));

        // フェードアウトが完了したら、持ち越したUIを削除してスッキリさせる
        Destroy(canvasRoot);

        // スクリプトが付いているオブジェクトが別なら、それも削除
        if (this.transform.root.gameObject != canvasRoot)
        {
            Destroy(this.transform.root.gameObject);
        }
    }

    // CanvasGroupを使ったフェード処理
    private IEnumerator FadeCanvasGroup(float startAlpha, float endAlpha)
    {
        if (fadeCanvasGroup == null) yield break;

        float elapsed = 0.0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / fadeDuration);
            yield return null;
        }
        fadeCanvasGroup.alpha = endAlpha;
    }

    private IEnumerator AnimationEffect()
    {
        float waitTime = 1.0f / effectFps;
        int currentIndex = 0;

        while (true)
        {
            effectMagnet.sprite = effectSprites[currentIndex];
            currentIndex = (currentIndex + 1) % effectSprites.Length;
            yield return new WaitForSeconds(waitTime);
        }
    }
}