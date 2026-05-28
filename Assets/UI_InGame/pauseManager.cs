using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static pauseManager;

public class pauseManager : MonoBehaviour
{
    private enum PauseState
    {
        None,
        MenuSelect, // 3項目を選ぶメインメニュー状態
        Option  // オプション設定状態
    }
    private PauseState currentState = PauseState.None;

    [Header("ポーズUI")]
    [SerializeField] private GameObject pauseUI;

    [Header("画面切り替え用UI")]
    [SerializeField] private GameObject suggestMenuUI;
    [SerializeField] private GameObject optionMenuUI;
    [SerializeField] private GameObject ingameUI;

    [Header("メインメニュー（3項目）")]
    [SerializeField] private Image[] suggestImage;
    [SerializeField] private Image[] suggestText;
    [SerializeField] private Image[] configImage;
    [SerializeField] private Sprite[] normalSprite;
    [SerializeField] private Sprite[] selectSprite;
    [SerializeField] public Vector2 startText = new(-1250, -168);
    [SerializeField] public Vector2 newText   = new(-595, -168);
    [SerializeField] public float moveTextSpeed = 4.0f;
    private int suggestIndex = 0;
    private bool skipTextSlide = false;

    [Header("背景（左）")]
    [SerializeField] public RectTransform suggestBack;
    [SerializeField] public Vector2 startPos = new(-1300, 0);
    [SerializeField] public Vector2 newPos   = new(-562, 0);
    [SerializeField] public float moveSpeed = 7.0f;
    [SerializeField] public float waitOpen  = 0.7f;

    [Header("カーソル")]
    [SerializeField] public RectTransform cursor;
    [SerializeField] public RectTransform cursorMain;
    [SerializeField] public float[] cursorPosY;
    [SerializeField] private int cursorIndex = 0;
    [SerializeField] public float maskSpeed = 25.0f;

    [SerializeField] private float inputComboCnt = 0.2f;
    [SerializeField] private float lastInputTime = 0.0f;

    [System.Serializable]
    public class SliderSetting
    {
        public string sliderName;
        public RectTransform handle; // 各つまみ
        public int sliderIndex = 0;    // 各インデックス
    }

    [Header("設定スライダー")]
    [SerializeField] public SliderSetting[] sliderSetting;
    [SerializeField] public float[] sliderPosX;
    [SerializeField] private GameObject followOn;  // ON
    [SerializeField] private GameObject followOff; // OFF

    [SerializeField] private float arrowMove = 30.0f;      // 動くピクセル
    [SerializeField] private float arrowMoveTime = 0.1f; // 往路にかかる時間

    private bool isAnim = false;

    private bool isPause = false;
    private bool isOpen = false;

    void Update()
    {
        if (isPause)
        {
            // moveSpeedはピクセル単位の距離を1秒で移動する速度だから、直観的に操作できるように×1000する
            suggestBack.anchoredPosition = Vector2.MoveTowards(suggestBack.anchoredPosition,
                                           newPos, Time.unscaledDeltaTime * (moveSpeed * 1000));

            if (suggestIndex < suggestText.Length && suggestText[suggestIndex] != null)
            {
                suggestText[suggestIndex].rectTransform.anchoredPosition = 
                    Vector2.MoveTowards(suggestText[suggestIndex].rectTransform.anchoredPosition,
                                        newText,Time.unscaledDeltaTime * (moveTextSpeed * 1000));
            }

            // UIが開ききってからキー操作を受け付ける
            if (isOpen)
            {
                if (currentState == PauseState.MenuSelect)
                {
                    // メインメニューのカーソル操作
                    if (Input.GetKeyDown(KeyCode.UpArrow)) SuggestCursor(-1);
                    if (Input.GetKeyDown(KeyCode.DownArrow)) SuggestCursor(1);

                    // 決定キー（Enter等）で処理を実行
                    if (Input.GetKeyDown(KeyCode.Return))
                    {
                        EnterMenu();
                    }
                }
                else if (currentState == PauseState.Option)
                {
                    float currentX = cursorMain.anchoredPosition.x;
                    currentX = Mathf.Lerp(currentX, 0.0f, Time.unscaledDeltaTime * maskSpeed);
                    cursorMain.anchoredPosition = new Vector2(currentX, 0.0f);

                    if (Input.GetKeyDown(KeyCode.UpArrow))   MoveCursor(-1);
                    if (Input.GetKeyDown(KeyCode.DownArrow)) MoveCursor(1);

                    if (Input.GetKeyDown(KeyCode.LeftArrow))   MoveSlider(-1);
                    if (Input.GetKeyDown(KeyCode.RightArrow)) MoveSlider(1);

                }
            }
        }
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (isPause)
            {
                if (currentState == PauseState.MenuSelect)
                {
                    ResumeGame(); // メインメニューからゲーム再開
                }
                else if (currentState == PauseState.Option)
                {
                    skipTextSlide = true;
                    ChangeState(PauseState.MenuSelect); // オプションからメインメニューへ戻る
                }
            }
            else
            {
                PauseGame();
            }
        }
    }

    // 状態切り替え
    private void ChangeState(PauseState newState)
    {
        currentState = newState;

        if (newState == PauseState.MenuSelect)
        {
            suggestMenuUI.SetActive(true);
            optionMenuUI.SetActive(false);
            UpdateSelectMenu(); // 画像の選択状態を更新
        }
        else if (newState == PauseState.Option)
        {
            // suggestMenuUI.SetActive(false);
            optionMenuUI.SetActive(true);
            cursorMain.anchoredPosition = new Vector2(-770, 0);

            UpdateAllSlider();
        }
    }

    // メインメニューの画像切り替え
    void UpdateSelectMenu()
    {
        for (int i = 0; i < suggestImage.Length; i++)
        {
            if (i == suggestIndex)
            {
                if (skipTextSlide)
                {
                    suggestText[i].rectTransform.anchoredPosition = newText;
                    skipTextSlide = false;
                }
                else
                {
                    suggestText[i].rectTransform.anchoredPosition = startText;
                }
                suggestImage[i].sprite = selectSprite[i];
                suggestText[i].gameObject.SetActive(true);
                configImage[i].gameObject.SetActive(true);
            }
            else
            {
                suggestImage[i].sprite = normalSprite[i];
                suggestText[i].gameObject.SetActive(false);
                configImage[i].gameObject.SetActive(false);
            }
        }
    }

    // メインメニューのカーソル移動
    void SuggestCursor(int direction)
    {
        suggestIndex += direction;

        // ループ処理
        if (suggestIndex < 0) suggestIndex = suggestImage.Length - 1;
        if (suggestIndex >= suggestImage.Length) suggestIndex = 0;

        UpdateSelectMenu();
    }

    // メインメニューの決定処理
    void EnterMenu()
    {
        switch (suggestIndex)
        {
            case 0: // オプション
                ChangeState(PauseState.Option);
                break;
            case 1: // チェックポイント
                Debug.Log("チェックポイント");
                break;
            case 2: // タイトルに戻る
                ResumeGame();
                SceneManager.LoadScene("TitleScene");
                break;
        }
    }

    // 一時停止
    public void PauseGame()
    {
        Time.timeScale = 0f;     // ゲーム内の時間を止める

        isPause = true;
        suggestIndex = 0;

        ChangeState(PauseState.MenuSelect);

        ingameUI.gameObject.SetActive(false);
        suggestBack.gameObject.SetActive(true);
        suggestBack.anchoredPosition = startPos;
        StartCoroutine(openWait(waitOpen));
    }

    // 再開
    public void ResumeGame()
    {
        pauseUI.SetActive(false); // ポーズUI非表示
        suggestBack.gameObject.SetActive(false);
        ingameUI.gameObject.SetActive(true);
        Time.timeScale = 1f;      // ゲーム内の時間を戻す

        isPause = false;
        isOpen = false;
        currentState = PauseState.None;
    }

    private IEnumerator openWait(float wait)
    {
        yield return new WaitForSecondsRealtime(wait);
        if (isPause)
        {
            isOpen = true;
            pauseUI.SetActive(true); // ポーズUI表示
            ChangeState(PauseState.MenuSelect);
        }
    }

    void MoveCursor(int direction)
    {
        bool isCombo = (Time.unscaledTime - lastInputTime) < inputComboCnt;
        lastInputTime = Time.unscaledTime;

        // カーソルの位置調節
        cursorIndex += direction;
        if (cursorIndex < 0) cursorIndex = cursorPosY.Length - 1;
        if (cursorIndex >= cursorPosY.Length) cursorIndex = 0;

        Vector3 pos = cursor.anchoredPosition;
        pos.y = cursorPosY[cursorIndex];
        cursor.anchoredPosition = pos;

        if (!isCombo) cursorMain.anchoredPosition = new Vector2(-770, 0);
    }

    void MoveSlider(int direction)
    {
        // 配列の範囲外なら何もしない
        if (cursorIndex < 0 || cursorIndex >= sliderSetting.Length) return;

        // 現在選んでいる縦カーソル（cursorIndex）と同じスライダーを取得
        SliderSetting currentSlider = sliderSetting[cursorIndex];

        if (cursorIndex == 3)
        {
            if (isAnim) return;

            int onoff = currentSlider.sliderIndex;
            float moveEdge = arrowMove;

            if ((direction > 0 && onoff == 1) || (direction < 0 && onoff == 0))
            {
                moveEdge *= 0.3f;
            }
            if (currentSlider.handle != null)
            {
                StartCoroutine(ArrowAnimation(currentSlider.handle, new Vector2(moveEdge * direction, 0)));
            }

            if (direction > 0) currentSlider.sliderIndex = 1; // 右キー
            if (direction < 0) currentSlider.sliderIndex = 0; // 左キー

            if (onoff != currentSlider.sliderIndex)
            {
                UpdateFollow(currentSlider.sliderIndex);
            }
            return;
        }

        // 選択されたスライダーの数値を増減
        currentSlider.sliderIndex += direction;
        if (currentSlider.sliderIndex < 0) currentSlider.sliderIndex = 0;
        if (currentSlider.sliderIndex >= sliderPosX.Length) currentSlider.sliderIndex = sliderPosX.Length - 1;

        // そのスライダーの見た目（座標）を更新
        Vector3 pos = currentSlider.handle.anchoredPosition;
        pos.x = sliderPosX[currentSlider.sliderIndex];
        currentSlider.handle.anchoredPosition = pos;
    }

    // すべてのスライダーの見た目を現在のインデックス位置に合わせる関数
    void UpdateAllSlider()
    {
        for (int i = 0; i < sliderSetting.Length; i++)
        {
            if (i == 3)
            {
                UpdateFollow(sliderSetting[i].sliderIndex);
                continue;
            }

            if (sliderSetting[i].handle != null)
            {
                Vector3 pos = sliderSetting[i].handle.anchoredPosition;
                pos.x = sliderPosX[sliderSetting[i].sliderIndex];
                sliderSetting[i].handle.anchoredPosition = pos;
            }
        }
    }
    void UpdateFollow(int index)
    {
        if (followOn == null || followOff == null) return;

        if (index == 1) // ON
        {
            followOn.SetActive(true);
            followOff.SetActive(false);
        }
        else // OFF
        {
            followOn.SetActive(false);
            followOff.SetActive(true);
        }
    }

    // 動かして戻す
    private IEnumerator ArrowAnimation(RectTransform target, Vector2 moveDir)
    {
        if (target == null) yield break;
        isAnim = true; // アニメーション開始フラグ

        Vector2 startAnchoredPos = target.anchoredPosition;     // 初期位置
        Vector2 targetAnchoredPos = startAnchoredPos + moveDir; // 動く目標位置

        // 行く
        float elapsedTime = 0f;
        while (elapsedTime < arrowMoveTime)
        {
            target.anchoredPosition = Vector2.Lerp(startAnchoredPos, targetAnchoredPos, elapsedTime / arrowMoveTime);
            elapsedTime += Time.unscaledDeltaTime;
            yield return null; // 1フレーム待つ
        }
        target.anchoredPosition = targetAnchoredPos; // 目標位置に合わせる

        // 戻る
        float elapsedTime02 = 0f; // elapsedTimeをリセット
        while (elapsedTime02 < arrowMoveTime)
        {
            target.anchoredPosition = Vector2.Lerp(targetAnchoredPos, startAnchoredPos, elapsedTime02 / arrowMoveTime);
            elapsedTime02 += Time.unscaledDeltaTime;
            yield return null;
        }
        target.anchoredPosition = startAnchoredPos; // 元に戻す

        isAnim= false;
    }

}