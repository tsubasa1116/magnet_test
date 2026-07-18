using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class pauseManager : MonoBehaviour
{
    private enum PauseState
    {
        None,
        MenuSelect, // 3項目を選ぶメインメニュー状態
        Option,  // オプション設定状態
        Checkpoint, // チェックポイント確認状態
        Title, // タイトル確認状態
    }
    private PauseState currentState = PauseState.None;

    [Header("参照")]
    [SerializeField] private FollowUI[] follow;

    [Header("ポーズUI")]
    [SerializeField] private GameObject pauseUI;
    [SerializeField] public CanvasGroup lightingLayer;
    [SerializeField] private float[] lightingAlpha;

    [Header("画面切り替え用UI")]
    [SerializeField] private GameObject suggestMenuUI;
    [SerializeField] private GameObject optionMenuUI;
    [SerializeField] private GameObject ingameUI;
    [SerializeField] private GameObject idleCursor;

    [Header("メインメニュー（3項目）")]
    [SerializeField] private Image[] suggestImage;
    [SerializeField] private Image[] suggestText;
    [SerializeField] private Image[] configImage;
    [SerializeField] private Sprite[] normalSprite;
    [SerializeField] private Sprite[] selectSprite;
    [SerializeField] public Vector2 startText = new(-1250, -168);
    [SerializeField] public Vector2[] newText = { new(-595, -168), new(-665, -168), new(-702, -168) };
    [SerializeField] public float moveTextSpeed = 4.0f;
    private int suggestIndex = 0;
    private bool skipTextSlide = false;

    [Header("背景（左）")]
    [SerializeField] public RectTransform suggestBack;
    [SerializeField] public Vector2 startPos = new(-1300, 0);
    [SerializeField] public Vector2 newPos = new(-562, 0);
    [SerializeField] public float moveSpeed = 7.0f;
    [SerializeField] public float waitOpen = 0.7f;

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

    [Header("音量（5段階）")]
    [SerializeField] private float[] bgmVolumeTable = { 0f, 0.25f, 0.5f, 0.75f, 1f };
    [SerializeField] private float[] seVolumeTable = { 0f, 0.25f, 0.5f, 0.75f, 1f };

    [Header("チェックポイントチェック")]
    [SerializeField] private GameObject CPcheckUI;
    [SerializeField] private Image[] suggestImageCP;
    [SerializeField] private Sprite[] normalSpriteCP;
    [SerializeField] private Sprite[] selectSpriteCP;
    private int suggestIndexCP = 0;

    [Header("タイトルチェック")]
    [SerializeField] private GameObject TCcheckUI;
    [SerializeField] private Image[] suggestImageTC;
    [SerializeField] private Sprite[] normalSpriteTC;
    [SerializeField] private Sprite[] selectSpriteTC;
    private int suggestIndexTC = 0;

    private bool isAnim = false;

    private bool isPause = false;
    private bool isOpen = false;

    void Start()
    {
        LoadSettings();
        UpdateAllSlider();
        UpdateBGMVolume();
        UpdateSEVolume();
        UpdateLighting();
        AudioManager.Instance.PlayBGM("Field");
    }


    void Update()
    {
        if (isPause)
        {
            // moveSpeedはピクセル単位の距離を1秒で移動する速度だから、直観的に操作できるように×1000する
            suggestBack.anchoredPosition = Vector2.MoveTowards(suggestBack.anchoredPosition,
                                           newPos, Time.unscaledDeltaTime * (moveSpeed * 1000));

            if (currentState == PauseState.MenuSelect)
            {
                if (suggestIndex < suggestText.Length && suggestText[suggestIndex] != null)
                {
                    suggestText[suggestIndex].rectTransform.anchoredPosition =
                        Vector2.MoveTowards(suggestText[suggestIndex].rectTransform.anchoredPosition,
                                            newText[suggestIndex], Time.unscaledDeltaTime * (moveTextSpeed * 1000));
                }
            }

            // UIが開ききってからキー操作を受け付ける
            if (isOpen)
            {
                if (currentState == PauseState.MenuSelect)
                {
                    // メインメニューのカーソル操作
                    if (Gamepad.current?.dpad.up.wasPressedThisFrame == true) SuggestCursor(-1);
                    if (Gamepad.current?.dpad.down.wasPressedThisFrame == true) SuggestCursor(1);
                    if (Input.GetKeyDown(KeyCode.UpArrow)) SuggestCursor(-1);
                    if (Input.GetKeyDown(KeyCode.DownArrow)) SuggestCursor(1);

                    // 決定キー（Enter等）で処理を実行
                    if (Input.GetKeyDown(KeyCode.Return) || Gamepad.current?.buttonEast.wasPressedThisFrame == true)
                    {
                        EnterMenu();
                    }
                }
                else if (currentState == PauseState.Option)
                {
                    float currentX = cursorMain.anchoredPosition.x;
                    currentX = Mathf.Lerp(currentX, 0.0f, Time.unscaledDeltaTime * maskSpeed);
                    cursorMain.anchoredPosition = new Vector2(currentX, 0.0f);

                    if (Gamepad.current?.dpad.up.wasPressedThisFrame == true) MoveCursor(-1);
                    if (Gamepad.current?.dpad.down.wasPressedThisFrame == true) MoveCursor(1);
                    if (Input.GetKeyDown(KeyCode.UpArrow)) MoveCursor(-1);
                    if (Input.GetKeyDown(KeyCode.DownArrow)) MoveCursor(1);

                    if (Gamepad.current?.dpad.left.wasPressedThisFrame == true) MoveSlider(-1);
                    if (Gamepad.current?.dpad.right.wasPressedThisFrame == true) MoveSlider(1);
                    if (Input.GetKeyDown(KeyCode.LeftArrow)) MoveSlider(-1);
                    if (Input.GetKeyDown(KeyCode.RightArrow)) MoveSlider(1);

                }
                else if (currentState == PauseState.Checkpoint)
                {
                    skipTextSlide = true;

                    // メインメニューのカーソル操作
                    if (Gamepad.current?.dpad.up.wasPressedThisFrame == true) CPCursor(-1);
                    if (Gamepad.current?.dpad.down.wasPressedThisFrame == true) CPCursor(1);
                    if (Input.GetKeyDown(KeyCode.UpArrow)) CPCursor(-1);
                    if (Input.GetKeyDown(KeyCode.DownArrow)) CPCursor(1);

                    if (Input.GetKeyDown(KeyCode.Return) || Gamepad.current?.buttonEast.wasPressedThisFrame == true)
                    {
                        EnterMenu();
                    }
                }
                else if (currentState == PauseState.Title)
                {
                    skipTextSlide = true;

                    // メインメニューのカーソル操作
                    if (Gamepad.current?.dpad.up.wasPressedThisFrame == true) TCCursor(-1);
                    if (Gamepad.current?.dpad.down.wasPressedThisFrame == true) TCCursor(1);
                    if (Input.GetKeyDown(KeyCode.UpArrow)) TCCursor(-1);
                    if (Input.GetKeyDown(KeyCode.DownArrow)) TCCursor(1);

                    if (Input.GetKeyDown(KeyCode.Return) || Gamepad.current?.buttonEast.wasPressedThisFrame == true)
                    {
                        EnterMenu();
                    }
                }
            }
        }

        bool pausePressed = Input.GetKeyDown(KeyCode.P) || Gamepad.current?.startButton.wasPressedThisFrame == true;
        bool backPressed = pausePressed || Gamepad.current?.buttonSouth.wasPressedThisFrame == true;

        if (isPause)
        {
            if (backPressed)
            {
                if (currentState == PauseState.MenuSelect)
                {
                    ResumeGame(); // メインメニューからゲーム再開
                }
                else if (currentState == PauseState.Option)
                {
                    UISE.Instance.CancelUI();
                    skipTextSlide = true;
                    ChangeState(PauseState.MenuSelect); // オプションからメインメニューへ戻る
                }
                else if (currentState == PauseState.Checkpoint)
                {
                    UISE.Instance.CancelUI();
                    ChangeState(PauseState.MenuSelect); // オプションからメインメニューへ戻る
                }
                else if (currentState == PauseState.Title)
                {
                    UISE.Instance.CancelUI();
                    ChangeState(PauseState.MenuSelect); // オプションからメインメニューへ戻る
                }
            }
        }
        else
        {
            if (pausePressed)
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
            idleCursor.SetActive(false);
            CPcheckUI.SetActive(false);
            TCcheckUI.SetActive(false);
            UpdateSelectMenu(); // 画像の選択状態を更新
        }
        else if (newState == PauseState.Option)
        {
            // suggestMenuUI.SetActive(false);
            optionMenuUI.SetActive(true);
            idleCursor.SetActive(true);
            cursorMain.anchoredPosition = new Vector2(-770, 0);

            UpdateAllSlider();
        }
        else if (newState == PauseState.Checkpoint)
        {
            suggestIndexCP = 1;
            CPcheckUI.SetActive(true);
            skipTextSlide = true;
            UpdateSelectMenu();
        }
        else if (newState == PauseState.Title)
        {
            suggestIndexTC = 1;
            TCcheckUI.SetActive(true);
            skipTextSlide = true;
            UpdateSelectMenu();
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
                    suggestText[i].rectTransform.anchoredPosition = newText[suggestIndex];
                    skipTextSlide = false;
                }
                else
                {
                    suggestText[i].rectTransform.anchoredPosition = startText;
                }
                suggestImage[i].sprite = selectSprite[i];
                suggestImage[i].SetNativeSize();
                suggestText[i].gameObject.SetActive(true);
                configImage[i].gameObject.SetActive(true);
            }
            else
            {
                suggestImage[i].sprite = normalSprite[i];
                suggestImage[i].SetNativeSize();
                suggestText[i].gameObject.SetActive(false);
                configImage[i].gameObject.SetActive(false);
            }
        }
        if (suggestIndex == 0)
        {
            optionMenuUI.SetActive(true);
            idleCursor.SetActive(false);
        }
        else
        {
            optionMenuUI.SetActive(false);
        }

        for (int i = 0; i < suggestImageCP.Length; i++)
        {
            if (i == suggestIndexCP)
            {
                suggestImageCP[i].sprite = selectSpriteCP[i];
                suggestImageCP[i].SetNativeSize();
            }
            else
            {
                suggestImageCP[i].sprite = normalSpriteCP[i];
                suggestImageCP[i].SetNativeSize();
            }
        }

        for (int i = 0; i < suggestImageTC.Length; i++)
        {
            if (i == suggestIndexTC)
            {
                suggestImageTC[i].sprite = selectSpriteTC[i];
                suggestImageTC[i].SetNativeSize();
            }
            else
            {
                suggestImageTC[i].sprite = normalSpriteTC[i];
                suggestImageTC[i].SetNativeSize();
            }
        }
    }

    // メインメニューのカーソル移動
    void SuggestCursor(int direction)
    {
        suggestIndex += direction;
        UISE.Instance.CursorUI();

        // ループ処理
        if (suggestIndex < 0) suggestIndex = suggestImage.Length - 1;
        if (suggestIndex >= suggestImage.Length) suggestIndex = 0;

        UpdateSelectMenu();
    }

    void CPCursor(int direction)
    {
        suggestIndexCP += direction;
        UISE.Instance.CursorUI();

        // ループ処理
        if (suggestIndexCP < 0) suggestIndexCP = suggestImageCP.Length - 1;
        if (suggestIndexCP >= suggestImageCP.Length) suggestIndexCP = 0;

        UpdateSelectMenu();
    }

    void TCCursor(int direction)
    {
        suggestIndexTC += direction;
        UISE.Instance.CursorUI();

        // ループ処理
        if (suggestIndexTC < 0) suggestIndexTC = suggestImageTC.Length - 1;
        if (suggestIndexTC >= suggestImageTC.Length) suggestIndexTC = 0;

        UpdateSelectMenu();
    }

    // メインメニューの決定処理
    void EnterMenu()
    {
        switch (currentState)
        {
            case PauseState.MenuSelect:
                switch (suggestIndex)
                {
                    case 0: // オプション
                        UISE.Instance.EnterUI();
                        ChangeState(PauseState.Option);
                        break;
                    case 1: // チェックポイント
                        UISE.Instance.EnterUI();
                        ChangeState(PauseState.Checkpoint);
                        break;
                    case 2: // タイトルに戻る
                        UISE.Instance.EnterUI();
                        ChangeState(PauseState.Title);
                        break;
                }
                break;
            case PauseState.Checkpoint:
                switch (suggestIndexCP)
                {
                    case 0:
                        UISE.Instance.StartUI();
                        Debug.Log("チェック");
                        break;
                    case 1:
                        UISE.Instance.EnterUI();
                        ChangeState(PauseState.MenuSelect);
                        break;
                }
                break;

            case PauseState.Title:
                switch (suggestIndexTC)
                {
                    case 0:
                        UISE.Instance.EnterUI();
                        ResumeGame();
                        SceneLoad.LoadWithLoadingScreen("TitleScene", FadeType.Black);
                        break;
                    case 1:
                        UISE.Instance.EnterUI();
                        ChangeState(PauseState.MenuSelect);
                        break;
                }
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
            //ChangeState(PauseState.MenuSelect);
        }
    }

    void MoveCursor(int direction)
    {
        bool isCombo = (Time.unscaledTime - lastInputTime) < inputComboCnt;
        lastInputTime = Time.unscaledTime;

        UISE.Instance.CursorUI();
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
        UISE.Instance.CursorUI();


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

            SaveSettings();

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

        if (cursorIndex == 0)
        {
            UpdateBGMVolume();
        }
        else if (cursorIndex == 1)
        {
            UpdateSEVolume();
        }
        else if (cursorIndex == 2)
        {
            UpdateLighting();
        }

        SaveSettings();
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
            for (int i = 0; i < follow.Length; i++)
            {
                followOn.SetActive(true);
                followOff.SetActive(false);
                follow[i].followON = true;
            }
        }
        else // OFF
        {
            for (int i = 0; i < follow.Length; i++)
            {
                followOn.SetActive(false);
                followOff.SetActive(true);
                follow[i].followON = false;
            }
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

        isAnim = false;
    }

    private void UpdateLighting()
    {
        if (lightingLayer == null) return;

        int targetIndex = 2;

        if (targetIndex < sliderSetting.Length)
        {
            int currentIndex = sliderSetting[targetIndex].sliderIndex;

            lightingLayer.alpha = lightingAlpha[currentIndex];
        }
    }

    // BGM音量（0番目のスライダー）
    private void UpdateBGMVolume()
    {
        const int bgmSliderIndex = 0;

        if (AudioManager.Instance == null ||
            bgmSliderIndex >= sliderSetting.Length ||
            bgmVolumeTable == null ||
            bgmVolumeTable.Length == 0)
        {
            return;
        }

        int index = sliderSetting[bgmSliderIndex].sliderIndex;
        index = Mathf.Clamp(index, 0, bgmVolumeTable.Length - 1);

        AudioManager.Instance.SetBGMVolume(bgmVolumeTable[index]);
    }

    // SE音量（1番目のスライダー）
    private void UpdateSEVolume()
    {
        const int seSliderIndex = 1;

        if (AudioManager.Instance == null ||
            seSliderIndex >= sliderSetting.Length ||
            seVolumeTable == null ||
            seVolumeTable.Length == 0)
        {
            return;
        }

        int index = sliderSetting[seSliderIndex].sliderIndex;
        index = Mathf.Clamp(index, 0, seVolumeTable.Length - 1);

        AudioManager.Instance.SetSEVolume(seVolumeTable[index]);
    }

    void SaveSettings()
    {
        for (int i = 0; i < sliderSetting.Length; i++)
        {
            PlayerPrefs.SetInt("ConfigSlider_" + i, sliderSetting[i].sliderIndex);
        }
        PlayerPrefs.Save();
    }

    void LoadSettings()
    {
        for (int i = 0; i < sliderSetting.Length; i++)
        {
            sliderSetting[i].sliderIndex = PlayerPrefs.GetInt("ConfigSlider_" + i, 0);
        }
    }

}