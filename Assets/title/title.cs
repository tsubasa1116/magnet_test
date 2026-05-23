using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class title : MonoBehaviour
{
    [Header("タイトルUI管理")]
    [SerializeField] public GameObject pressUI;
    [SerializeField] public GameObject titleMenuUI;
    [SerializeField] bool isOpen = false;

    [Header("カーソル")]
    [SerializeField] public RectTransform cursor;
    [SerializeField] public RectTransform cursorMain;
    [SerializeField] public float[] cursorPosY;
    [SerializeField] private int cursorIndex = 0;
    [SerializeField] public float maskSpeed = 25.0f;

    [Header("PressAnyButton点滅")]
    [SerializeField] public CanvasGroup pressButton; // 点滅させるため、Imageより楽
    [SerializeField] public float blinkSpeed = 2.0f;

    [Header("スコア表示")]
    [SerializeField] public RectTransform score;
    [SerializeField] public CanvasGroup scoreText;
    [SerializeField] public bool isFadeIn = false;
    [SerializeField] public Vector2 startPos = new(1000, -261);
    [SerializeField] public Vector2 newPos   = new(630, -261);
    [SerializeField] public float fadeSpeed = 2.0f;
    [SerializeField] public float lerpSpeed = 10.0f;
    [SerializeField] public float waitFade  = 3.0f;


    void Update()
    {
        pressButton.alpha = 0.6f + 0.5f * Mathf.Sin(Time.time * blinkSpeed); // 点滅

        if (!isOpen && Input.GetKeyDown(KeyCode.Return))
        {
            OpenMenu();
            return;
        }
        if(isOpen)
        {
            // カーソルを左からぬるっ
            float currentX = cursorMain.anchoredPosition.x; 
            currentX = Mathf.Lerp(currentX, 0.0f, Time.deltaTime * maskSpeed);
            cursorMain.anchoredPosition = new Vector2(currentX, 0.0f);

            // スコア群を画面外からぬるっ
            score.anchoredPosition = Vector2.Lerp(score.anchoredPosition, newPos, Time.deltaTime * lerpSpeed);

            // スコアテキストをフェードイン
            if (isFadeIn)
            {
                if (scoreText.alpha < 1.0f) scoreText.alpha += Time.deltaTime * fadeSpeed;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))   MoveCursor(-1);
            if (Input.GetKeyDown(KeyCode.DownArrow)) MoveCursor(1);
        }
    }

    // メニューを開く
    void OpenMenu()
    {
        isOpen = true;
        pressUI.SetActive(false);
        titleMenuUI.SetActive(true);

        isFadeIn = false;
        StartCoroutine(FadeWait(waitFade)); // 1秒待ってからフェードイン
        score.anchoredPosition = startPos; // スコアの初期位置
    }

    // カーソル移動
    void MoveCursor(int direction)
    {
        // カーソルの位置調節
        cursorIndex += direction;
        if(cursorIndex < 0) cursorIndex = cursorPosY.Length - 1;
        if(cursorIndex >= cursorPosY.Length) cursorIndex = 0;

        Vector3 pos = cursor.anchoredPosition;
        pos.y = cursorPosY[cursorIndex];
        cursor.anchoredPosition = pos;

        cursorMain.anchoredPosition = new Vector2(-427, 0);
    }

    private IEnumerator FadeWait(float wait)
    {
        yield return new WaitForSeconds(wait);
        isFadeIn = true;
    }
}
