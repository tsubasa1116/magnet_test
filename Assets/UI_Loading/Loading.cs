using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class Loading : MonoBehaviour
{
    [Header("UI参照")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;  // フェード用CanvasGroup
    [SerializeField] private Image loadingBar;             // 進捗用ロードバー
    [SerializeField] private Image loadingBarBack;             // 進捗用ロードバー
    [SerializeField] private GameObject magnet;            // 回転するマグネット
    [SerializeField] private Image loadingText;            // Loading...(背景)
    [SerializeField] private CanvasGroup loadingTextBlink; // Loading...(点滅)

    [Header("背景食切り替え用設定")]
    [SerializeField] private Image backgroundImage;      // ロード画面の背景のImageコンポーネント
    [SerializeField] private Sprite blackFadeBgSprite;   // 黒背景
    [SerializeField] private Sprite whiteFadeBgSprite;   // 白背景
    [SerializeField] private Sprite blackText;      // 黒Loading...(背景)
    [SerializeField] private Sprite whiteText;      // 白Loading...(背景)
    [SerializeField] private Sprite blackTextBlink; // 黒Loading...(点滅)
    [SerializeField] private Sprite whiteTextBlink; // 白Loading...(点滅)

    [SerializeField] public AnimationCurve blinkCurve; // 点滅のアニメーションカーブ
    [SerializeField] public float blinkSpeed = 2.0f;   // 点滅の速度

    [SerializeField] private Sprite[] effectSprites; // エフェクト用のスプライト配列
    [SerializeField] private Image effectMagnet;     // エフェクト用のImageコンポーネント
    [SerializeField] private float effectFps = 30.0f;

    [Header("設定")]
    [SerializeField] private float fadeDuration = 2.0f;    // フェードの時間
    [SerializeField] private float rotationSpeed = 100.0f; // マグネットの回転速度

    private Coroutine uiCoroutine;

    // =========================================
    // ライフサイクル
    // =========================================
    void Awake()
    {
        // 画面が見える前にバーを0にし、CanvasGroupを表示状態にする
        if (loadingBar != null) loadingBar.fillAmount = 0f;
        if (loadingBarBack != null) loadingBarBack.gameObject.SetActive(true);
        if (fadeCanvasGroup != null) fadeCanvasGroup.alpha = 1f;

        Canvas canvas = fadeCanvasGroup.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.sortingOrder = 999;
        }

        // フェードの色に合わせて背景と文字の画像を切り替える
        SetLoadColorByFade();
    }

    // =========================================
    // ロードの画面色設定
    // =========================================
    private void SetLoadColorByFade()
    {
        // SceneFadeOut側で保存したフェードの種類を取得
        int fadeTypeInt = PlayerPrefs.GetInt("FadeType", (int)FadeType.Black);
        FadeType fadeType = (FadeType)fadeTypeInt;

        // 背景画像
        if (backgroundImage != null)
        {
            backgroundImage.sprite = (fadeType == FadeType.White) ? whiteFadeBgSprite : blackFadeBgSprite;
        }

        // Loading...(背景)
        if (loadingText != null)
        {
            loadingText.sprite = (fadeType == FadeType.White) ? whiteText : blackText;
        }

        // Loading...(点滅)
        if (loadingTextBlink != null)
        {
            // CanvasGroupがついているオブジェクトからImageを探す
            Image textImage = loadingTextBlink.GetComponent<Image>();

            if (textImage != null)
            {
                textImage.sprite = (fadeType == FadeType.White) ? whiteTextBlink : blackTextBlink;
            }
        }
    }

    // =========================================
    // 初期化処理
    // =========================================
    void Start()
    {
        StartCoroutine(LoadSceneSequence());

        StartCoroutine(AnimationEffect());
    }

    // =========================================
    // 更新処理
    // =========================================
    void Update()
    {
        magnet.transform.Rotate(0f, 0f, -rotationSpeed * Time.deltaTime);

        float t = Mathf.PingPong(Time.time * blinkSpeed, 1.0f);
        loadingTextBlink.alpha = blinkCurve.Evaluate(t);
    }

    // =========================================
    // ロード処理
    // =========================================
    private IEnumerator LoadSceneSequence()
    {
        // マグネットが回り始めてからロード処理を走らせる
        yield return new WaitForSeconds(0.2f);

        // 目的のシーン名をPlayerPrefsから取得
        string targetScene = PlayerPrefs.GetString("NextScene", "TakeTakeScene");

        // ThreadPriority.Low＞Unityがロードよりも描画を優先する
        Application.backgroundLoadingPriority = ThreadPriority.Low;

        // 非同期読み込み開始
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(targetScene);
        asyncLoad.allowSceneActivation = false; // 読み込み完了しても勝手に遷移させない

        float visualProgress = 0f;
        float minLoadTime = 1.0f;

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

        // ロードが終わったら優先度を元に戻す
        Application.backgroundLoadingPriority = ThreadPriority.Normal;

        // Canvasの一番上の親オブジェクトを取得
        GameObject canvasRoot = fadeCanvasGroup.transform.root.gameObject;

        // 以降のDontDestroyOnLoadやフェードアウトの処理はそのまま
        DontDestroyOnLoad(canvasRoot);
        DontDestroyOnLoad(this.transform.root.gameObject);

        asyncLoad.allowSceneActivation = true;

        yield return new WaitUntil(() => asyncLoad.isDone);

        // フェードアウト前にローディングUIを消す
        if (uiCoroutine != null) StopCoroutine(uiCoroutine);

        if (loadingBar != null) loadingBar.gameObject.SetActive(false);
        if (loadingBarBack != null) loadingBarBack.gameObject.SetActive(false);
        if (magnet != null) magnet.SetActive(false);
        if (loadingText != null) loadingText.gameObject.SetActive(false);
        if (loadingTextBlink != null) loadingTextBlink.gameObject.SetActive(false);
        if (effectMagnet != null) effectMagnet.gameObject.SetActive(false);

        yield return new WaitForSeconds(0.4f);

        yield return StartCoroutine(FadeCanvasGroup(1f, 0f));

        Destroy(canvasRoot);

        if (this.transform.root.gameObject != canvasRoot)
        {
            Destroy(this.transform.root.gameObject);
        }
    }

    // =========================================
    // フェード処理
    // =========================================
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

    // =========================================
    // エフェクトアニメーション処理
    // =========================================
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