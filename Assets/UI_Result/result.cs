using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class result : MonoBehaviour
{
    [Header("タイトルへ戻るボタン")]
    [SerializeField] public GameObject titleBackUI;     // タイトルへ戻るボタン
    [SerializeField] private GameObject titleBackBackUI; // ↑の後ろでちかちか光る背景
    
    [SerializeField] public AnimationCurve sizeCurve;
    [SerializeField] public float sizeSpeed = 2.0f;
    
    [Header("スコア表示")]
    [SerializeField] private Sprite[] scoreSprite; // C~Sのスコア画像
    [SerializeField] private Image scoreImage;     // スコア表示用のImageコンポーネント
    [SerializeField] private AnimationCurve scoreCurve;

    [Header("テキスト画像")]
    [SerializeField] private GameObject gameclear; // 「ゲームクリア」UI
    [SerializeField] private Image record;    // 「戦績」UI
    [SerializeField] private Image destroy;   // 「撃破数」UI
    [SerializeField] private Image cleartime; // 「クリアタイム」UI

    [Header("最終的な戦績表示")]
    [SerializeField] private Sprite[] resultNumber; // 撃破数、クリアタイム用の数字連番画像

    [SerializeField] private RectTransform shakeTarget;

    [Header("数値表示用のImage配列")]
    [SerializeField] private Image[] destroyDigits; // 左から順に入れる
    [SerializeField] private Image[] timeDigits;    // 時10, 時1, 分10, 分1, 秒10, 秒1 の順に入れる

    private bool isInputOk = false;

    private int destroyCnt;
    private int timeSeconds;

    void Start()
    {
        InitUI();

        // 静的クラスから実際のゲームデータを取得
        destroyCnt = GameResultManager.DestroyCount;
        timeSeconds = GameResultManager.ClearTimeSeconds;

        // テスト用
        if (destroyCnt == 0 && timeSeconds == 0)
        {
            destroyCnt = 0;
            timeSeconds = 25;

            GameResultManager.SetResultData(destroyCnt, timeSeconds);
        }

        // あらかじめUI（数字やスコア）にデータを反映させておく
        InResultValue();

        StartCoroutine(ResultAnimationSequence());
    }

    private void InitUI()
    {
        SetCanvasGroupAlpha(gameclear, 0);
        record.gameObject.SetActive(false);
        destroy.gameObject.SetActive(false);
        cleartime.gameObject.SetActive(false);

        scoreImage.gameObject.SetActive(false);

        titleBackUI.SetActive(false);
        titleBackBackUI.SetActive(false);
        SetCanvasGroupAlpha(titleBackUI, 0);
        SetCanvasGroupAlpha(titleBackBackUI, 0);
    }

    private void SetCanvasGroupAlpha(GameObject target, float alpha)
    {
        CanvasGroup cg = target.GetComponent<CanvasGroup>();
        if (cg == null) cg = target.AddComponent<CanvasGroup>();
        cg.alpha = alpha;
    }

    void Update()
    {
        float t = Mathf.PingPong(Time.time * sizeSpeed, 1.0f);

        float scaleValue = sizeCurve.Evaluate(t);

        titleBackBackUI.transform.localScale = new Vector3(scaleValue, scaleValue, 1.0f);

        if (!isInputOk) return;

        if (Input.GetKeyDown(KeyCode.Return) || (Gamepad.current?.buttonEast.wasPressedThisFrame == true))
        {
            SceneLoad.LoadWithLoadingScreen("TitleScene", FadeType.White);
        }
    }

    private void InResultValue()
    {
        // 撃破数の数値をImage配列に反映（最大99想定）
        int d10 = (destroyCnt / 10) % 10;
        int d1 = destroyCnt % 10;
        if (destroyDigits.Length >= 2)
        {
            destroyDigits[0].sprite = resultNumber[d10];
            destroyDigits[1].sprite = resultNumber[d1];
        }

        // クリアタイム（秒）を時・分・秒に変換
        int h = timeSeconds / 3600;
        int m = (timeSeconds % 3600) / 60;
        int s = timeSeconds % 60;

        int h10 = (h / 10) % 10; int h1 = h % 10;
        int m10 = (m / 10) % 10; int m1 = m % 10;
        int s10 = (s / 10) % 10; int s1 = s % 10;

        if (timeDigits.Length >= 6)
        {
            timeDigits[0].sprite = resultNumber[h10];
            timeDigits[1].sprite = resultNumber[h1];
            timeDigits[2].sprite = resultNumber[m10];
            timeDigits[3].sprite = resultNumber[m1];
            timeDigits[4].sprite = resultNumber[s10];
            timeDigits[5].sprite = resultNumber[s1];
        }

        // スコアの計算と画像のセット
        int scoreIndex = GameResultManager.CalculateScoreRank();
        if (scoreIndex < scoreSprite.Length)
        {
            scoreImage.sprite = scoreSprite[scoreIndex];
        }
    }


    private IEnumerator ResultAnimationSequence()
    {
        // 1. 「ゲームクリア」UIをフェードで表示
        yield return StartCoroutine(FadeIn(gameclear, 1.0f));
        yield return new WaitForSeconds(0.3f);

        // 2. 「戦績」UIを表示
        record.gameObject.SetActive(true);
        yield return new WaitForSeconds(1.0f);

        // 3. 「撃破数」UIを表示（！）同時に揺らす
        destroy.gameObject.SetActive(true);
        yield return StartCoroutine(ShakeUI(0.3f, 15.0f));
        yield return new WaitForSeconds(1.0f);

        // 4. 「クリアタイム」UIを表示（！）同時に揺らす
        cleartime.gameObject.SetActive(true);
        yield return StartCoroutine(ShakeUI(0.3f, 15.0f));
        yield return new WaitForSeconds(1.0f);

        // 5. スコアのイメージを大きいサイズから元のサイズにイーズ表示
        scoreImage.gameObject.SetActive(true);
        yield return StartCoroutine(ScaleInScore(0.6f));
        yield return new WaitForSeconds(0.5f);

        // 6. タイトルに戻るボタンセット二つをフェードで表示
        titleBackUI.SetActive(true);
        titleBackBackUI.SetActive(true);
        StartCoroutine(FadeIn(titleBackUI, 0.8f));
        yield return StartCoroutine(FadeIn(titleBackBackUI, 0.8f));

        isInputOk = true;
    }

    // ==================================
    // フェードイン処理
    // ==================================
    private IEnumerator FadeIn(GameObject target, float duration)
    {
        CanvasGroup cg = target.GetComponent<CanvasGroup>();
        if (cg == null) cg = target.AddComponent<CanvasGroup>();

        float elapsed = 0.0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }
        cg.alpha = 1.0f;
    }

    // ==================================
    // シェイク処理
    // ==================================
    private IEnumerator ShakeUI(float duration, float magnitude)
    {
        if (shakeTarget == null) yield break;

        Vector3 originalPos = shakeTarget.anchoredPosition;
        float elapsed = 0.0f;

        // 揺れの速さ
        float shakeSpeed = 50.0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            // ガクガクっと揺らす
            float x = Mathf.Sign(Mathf.Sin(elapsed * shakeSpeed)) * magnitude;

            shakeTarget.anchoredPosition = new Vector3(originalPos.x + x, originalPos.y, originalPos.z);
            yield return null;
        }

        // 最後に確実に元の位置に戻す
        shakeTarget.anchoredPosition = originalPos;
    }

    // ==================================
    // スコアのイージング登場処理
    // ==================================
    private IEnumerator ScaleInScore(float duration)
    {
        float elapsed = 0.0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float scale = scoreCurve.Evaluate(t);
            scoreImage.transform.localScale = new Vector3(scale, scale, 1.0f);
            yield return null;
        }
        scoreImage.transform.localScale = Vector3.one;
    }

}
