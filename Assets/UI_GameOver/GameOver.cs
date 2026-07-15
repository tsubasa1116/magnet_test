using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;

public class GameOver : MonoBehaviour
{
    [Header("選択ボタン")]
    [SerializeField] public GameObject checkpointBackUI; // チェックポイントからやり直すボタン
    [SerializeField] public GameObject titleBackUI;      // タイトルへ戻るボタン
    [SerializeField] private GameObject blinkBackUI;     // 後ろでちかちか光る背景

    [SerializeField] public AnimationCurve sizeCurve;
    [SerializeField] public float sizeSpeed = 2.0f;

    [Header("テキスト画像")]
    [SerializeField] private GameObject gameover;  // 「ゲームオーバー」UI
    [SerializeField] private GameObject character; // 倒れたキャラクターUI

    void Start()
    {
        InitUI();

        StartCoroutine(GameoverAnimationSequence());
    }

    private void InitUI()
    {
        SetCanvasGroupAlpha(gameover, 0);
        SetCanvasGroupAlpha(character, 0);

        checkpointBackUI.SetActive(false);
        titleBackUI.SetActive(false);
        blinkBackUI.SetActive(false);
        SetCanvasGroupAlpha(checkpointBackUI, 0);
        SetCanvasGroupAlpha(titleBackUI, 0);
        SetCanvasGroupAlpha(blinkBackUI, 0);
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

        blinkBackUI.transform.localScale = new Vector3(scaleValue, scaleValue, 1.0f);

        if (Input.GetKeyDown(KeyCode.Return))
        {
            SceneManager.LoadScene("TitleScene");
        }
    }

    private IEnumerator GameoverAnimationSequence()
    {
        // 1. 「ゲームオーバー」UIをフェードで表示
        yield return StartCoroutine(FadeIn(gameover, 1.0f));
        yield return new WaitForSeconds(0.3f);

        // 2. 倒れたキャラクターUIを表示
        yield return StartCoroutine(FadeIn(character, 1.5f));
        yield return new WaitForSeconds(1.0f);

        // 3. 選択ボタンセットをフェードで表示
        checkpointBackUI.SetActive(true);
        titleBackUI.SetActive(true);
        blinkBackUI.SetActive(true);
        StartCoroutine(FadeIn(checkpointBackUI, 0.8f));
        StartCoroutine(FadeIn(titleBackUI, 0.8f));
        yield return StartCoroutine(FadeIn(blinkBackUI, 0.8f));
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
}
