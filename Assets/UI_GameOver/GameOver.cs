using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class GameOver : MonoBehaviour
{
    [Header("選択ボタン")]
    [SerializeField] public GameObject checkpointBackUI; // チェックポイントからやり直すボタン
    [SerializeField] public GameObject titleBackUI;      // タイトルへ戻るボタン
    [SerializeField] private GameObject blinkBackUI;     // 後ろでちかちか光る背景
    [SerializeField] private RectTransform blinkBackRect;

    [SerializeField] public AnimationCurve sizeCurve;
    [SerializeField] public float sizeSpeed = 2.0f;

    [Header("テキスト画像")]
    [SerializeField] private GameObject gameover;  // 「ゲームオーバー」UI
    [SerializeField] private GameObject character; // 倒れたキャラクターUI

    [Header("カーソル設定")]
    [SerializeField] private float[] cursorPosY; // 各ボタンのY座標 
    [SerializeField] private int cursorIndex = 0;

    private bool isInputOk = false;

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

        // カーソルの初期位置
        if (blinkBackRect != null && cursorPosY.Length > 0)
        {
            Vector2 pos = blinkBackRect.anchoredPosition;
            pos.y = cursorPosY[cursorIndex];
            blinkBackRect.anchoredPosition = pos;
        }
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

        if (!isInputOk) return;

        // カーソル移動
        if (Gamepad.current?.dpad.up.wasPressedThisFrame == true)   MoveCursor(-1);
        if (Gamepad.current?.dpad.down.wasPressedThisFrame == true) MoveCursor(1);
        if (Input.GetKeyDown(KeyCode.UpArrow))   MoveCursor(-1);
        if (Input.GetKeyDown(KeyCode.DownArrow)) MoveCursor(1);

        if (Input.GetKeyDown(KeyCode.Return) || (Gamepad.current?.buttonEast.wasPressedThisFrame == true))
        {
            if (cursorIndex == 0)
            {
                UISE.Instance.EnterUI();
                SceneLoad.LoadWithLoadingScreen("SampleScene", FadeType.Black);
            }
            else if (cursorIndex == 1)
            {
                UISE.Instance.StartUI();
                SceneLoad.LoadWithLoadingScreen("TitleScene", FadeType.Black);
            }
        }

    }

    // カーソル移動処理
    void MoveCursor(int direction)
    {
        cursorIndex += direction;
        UISE.Instance.CursorUI();
        // インデックスが範囲外になったらループさせる
        if (cursorIndex < 0) cursorIndex = cursorPosY.Length - 1;
        if (cursorIndex >= cursorPosY.Length) cursorIndex = 0;

        // カーソル（blinkBackUI）のY座標を更新
        if (blinkBackRect != null)
        {
            Vector2 pos = blinkBackRect.anchoredPosition;
            pos.y = cursorPosY[cursorIndex];
            blinkBackRect.anchoredPosition = pos;
        }
    }

    private IEnumerator GameoverAnimationSequence()
    {
        //「ゲームオーバー」UIをフェードで表示
        // 倒れたキャラクターUIを表示
        StartCoroutine(FadeIn(gameover, 1.5f));
        yield return StartCoroutine(FadeIn(character, 1.5f));
        yield return new WaitForSeconds(1.0f);

        // 選択ボタンセットをフェードで表示
        checkpointBackUI.SetActive(true);
        titleBackUI.SetActive(true);
        blinkBackUI.SetActive(true);
        StartCoroutine(FadeIn(checkpointBackUI, 0.8f));
        StartCoroutine(FadeIn(titleBackUI, 0.8f));
        yield return StartCoroutine(FadeIn(blinkBackUI, 0.8f));

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
}
