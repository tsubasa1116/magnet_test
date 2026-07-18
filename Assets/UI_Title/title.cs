using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class title : MonoBehaviour
{
    [Header("タイトルUI管理")]
    [SerializeField] public GameObject pressUI;
    [SerializeField] public GameObject titleMenuUI;
    [SerializeField] public GameObject titleLogoUI;
    [SerializeField] public GameObject infoUI;
    [SerializeField] public GameObject optionMenuUI;
    [SerializeField] public GameObject idleCursor;
    [SerializeField] public RectTransform cursorOP;
    [SerializeField] public RectTransform cursorMainOP;
    [SerializeField] bool isOption = false;
    [SerializeField] bool isOpen = false;

    [Header("カーソル")]
    [SerializeField] public RectTransform cursor;
    [SerializeField] public RectTransform cursorMain;
    [SerializeField] public float[] cursorPosY;
    [SerializeField] private int cursorIndex = 0;
    [SerializeField] public float maskSpeed = 25.0f;

    [SerializeField] private float inputComboCnt = 0.2f;
    [SerializeField] private float lastInputTime = 0.0f;

    [Header("PressAnyButton点滅")]
    [SerializeField] public CanvasGroup pressButton; // 点滅させるため、Imageより楽
    [SerializeField] public float blinkSpeed = 2.0f;
    [SerializeField] public AnimationCurve blinkCurve;

    [Header("スコア表示")]
    [SerializeField] public RectTransform score;
    [SerializeField] public CanvasGroup scoreText;
    [SerializeField] public bool isFadeIn = false;
    [SerializeField] public Vector2 startPos = new(1000, -261);
    [SerializeField] public Vector2 newPos   = new(630, -261);
    [SerializeField] public float fadeSpeed = 2.0f;
    [SerializeField] public float lerpSpeed = 10.0f;
    [SerializeField] public float waitFade  = 3.0f;
    [SerializeField] private Sprite[] scoreSprites;

    [System.Serializable]
    public class SliderSetting
    {
        public string sliderName;
        public RectTransform handle;
        public int sliderIndex = 0;
    }

    [Header("設定スライダー")]
    [SerializeField] public SliderSetting[] sliderSetting;
    [SerializeField] public float[] sliderPosX;
    [SerializeField] private GameObject followOn;
    [SerializeField] private GameObject followOff;

    [SerializeField] private float arrowMove = 30.0f;
    [SerializeField] private float arrowMoveTime = 0.1f;
    private bool isAnim = false;

    // オプション画面専用のカーソル位置
    [Header("オプションカーソル")]
    [SerializeField] public float[] optionCursorPosY;
    [SerializeField] private int optionCursorIndex = 0;

    [Header("ライティング")]
    [SerializeField] private CanvasGroup lightingLayer;
    [SerializeField] private float[] lightingAlpha;

    private bool isInputOk = false;

    void Start()
    {
        LoadSettings();
        UpdateAllSlider();
        UpdateLighting();

        InitUI();
        StartCoroutine(TitleAnimationSequence());

        // 前回のスコアを計算
        int rank = GameResultManager.CalculateScoreRank();

        if (scoreSprites != null && scoreSprites.Length > rank && scoreText != null)
        {
            Image targetImage = scoreText.GetComponent<Image>();

            if (targetImage != null)
            {
                targetImage.sprite = scoreSprites[rank];
            }
            else
            {
                // Image targetImage = scoreText.GetComponentInChildren<Image>();
                Debug.LogWarning("scoreTextにImageコンポーネントが見つかりません！");
            }
        }
    }

    private void InitUI()
    {
        SetCanvasGroupAlpha(pressUI, 0);
        pressButton.alpha = 0;
        SetCanvasGroupAlpha(titleLogoUI, 0);
        SetCanvasGroupAlpha(infoUI, 0);
    }

    private void SetCanvasGroupAlpha(GameObject target, float alpha)
    {
        CanvasGroup cg = target.GetComponent<CanvasGroup>();
        if (cg == null) cg = target.AddComponent<CanvasGroup>();
        cg.alpha = alpha;
    }

    void Update()
    {
        if (isInputOk)
        {
            float t = Mathf.PingPong(Time.time * blinkSpeed, 1.0f);
            pressButton.alpha = blinkCurve.Evaluate(t);
        }

        if (!isOpen && (Input.GetKeyDown(KeyCode.Return) || AnyGamepadButtonPressed()))
        {
            OpenMenu();
            return;
        }

        if (isOpen)
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

            if (isOption)
            {
                float currentOpX = cursorMainOP.anchoredPosition.x;
                currentOpX = Mathf.Lerp(currentOpX, 0.0f, Time.deltaTime * maskSpeed);
                cursorMainOP.anchoredPosition = new Vector2(currentOpX, 0.0f);

                // オプション画面中の操作
                if (Gamepad.current?.dpad.up.wasPressedThisFrame == true) MoveOptionCursor(-1);
                if (Gamepad.current?.dpad.down.wasPressedThisFrame == true) MoveOptionCursor(1);
                if (Input.GetKeyDown(KeyCode.UpArrow)) MoveOptionCursor(-1);
                if (Input.GetKeyDown(KeyCode.DownArrow)) MoveOptionCursor(1);

                if (Gamepad.current?.dpad.left.wasPressedThisFrame == true) MoveSlider(-1);
                if (Gamepad.current?.dpad.right.wasPressedThisFrame == true) MoveSlider(1);
                if (Input.GetKeyDown(KeyCode.LeftArrow)) MoveSlider(-1);
                if (Input.GetKeyDown(KeyCode.RightArrow)) MoveSlider(1);

                if (Gamepad.current?.startButton.wasPressedThisFrame == true ||
                    Gamepad.current?.buttonSouth.wasPressedThisFrame == true) CloseOption();
                if (Input.GetKeyDown(KeyCode.Escape)) CloseOption();
            }
            else
            {
                // メインメニュー中の操作
                if (Gamepad.current?.dpad.up.wasPressedThisFrame == true)   MoveCursor(-1);
                if (Gamepad.current?.dpad.down.wasPressedThisFrame == true) MoveCursor(1);
                if (Input.GetKeyDown(KeyCode.UpArrow)) MoveCursor(-1);
                if (Input.GetKeyDown(KeyCode.DownArrow)) MoveCursor(1);

                if (Input.GetKeyDown(KeyCode.Return) ||
                   (Gamepad.current?.buttonEast.wasPressedThisFrame == true))
                {
                    if (cursorIndex == 0)
                    {
                        SceneLoad.LoadWithLoadingScreen("YokoyamaScene", FadeType.Black);
                    }
                    else if (cursorIndex == 1)
                    {
                        isOption = true;
                        optionMenuUI.SetActive(true);
                        idleCursor.SetActive(true);

                        titleLogoUI.SetActive(false);
                        score.gameObject.SetActive(false);
                        
                        cursorMainOP.anchoredPosition = new Vector2(-770, 0);

                        UpdateAllSlider();
                    }
                    else if (cursorIndex == 2)
                    {
                        UnityEditor.EditorApplication.isPlaying = false; // エディタ上で停止
                        Application.Quit();
                    }
                }
            }
        }
    }

    private IEnumerator TitleAnimationSequence()
    {
        yield return new WaitForSeconds(1.0f);

        // タイトルロゴをフェードで表示
        yield return StartCoroutine(FadeIn(titleLogoUI, 2.5f));
        yield return new WaitForSeconds(0.3f);

        // プレスボタンをフェードで表示
        StartCoroutine(FadeIn(pressUI, 1.5f));
        yield return StartCoroutine(FadeIn(infoUI, 1.5f));
        yield return new WaitForSeconds(1.0f);

        isInputOk = true;
    }

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


    void UpdateLighting()
    {
        const int lightingSliderIndex = 2; // 3番目のスライダーが明るさの場合

        if (lightingLayer == null ||
            lightingSliderIndex >= sliderSetting.Length ||
            lightingAlpha == null ||
            lightingAlpha.Length == 0)
        {
            return;
        }

        int index = sliderSetting[lightingSliderIndex].sliderIndex;
        index = Mathf.Clamp(index, 0, lightingAlpha.Length - 1);

        lightingLayer.alpha = lightingAlpha[index];
    }

    // メニューを開く
    void OpenMenu()
    {
        isOpen = true;
        isOption = false;
        pressUI.SetActive(false);
        infoUI.SetActive(false);
        titleMenuUI.SetActive(true);

        isFadeIn = false;
        StartCoroutine(FadeWait(waitFade)); // 1秒待ってからフェードイン
        score.anchoredPosition = startPos; // スコアの初期位置
    }

    void CloseOption()
    {
        isOption = false;
        optionMenuUI.SetActive(false);
        idleCursor.SetActive(false);

        // ロゴとスコアを再表示
        titleLogoUI.SetActive(true);
        score.gameObject.SetActive(true);

        // カーソルをメインメニュー側に戻す
        cursorMain.anchoredPosition = new Vector2(-427, 0);

        // メインメニューのカーソル位置を再適用
        Vector3 pos = cursor.anchoredPosition;
        pos.y = cursorPosY[cursorIndex];
        cursor.anchoredPosition = pos;
    }

    void MoveOptionCursor(int direction)
    {
        bool isCombo = (Time.unscaledTime - lastInputTime) < inputComboCnt;
        lastInputTime = Time.unscaledTime;

        optionCursorIndex += direction;
        if (optionCursorIndex < 0) optionCursorIndex = optionCursorPosY.Length - 1;
        if (optionCursorIndex >= optionCursorPosY.Length) optionCursorIndex = 0;

        Vector3 pos = cursorOP.anchoredPosition;
        pos.y = optionCursorPosY[optionCursorIndex];
        cursorOP.anchoredPosition = pos;

        if (!isCombo) cursorMainOP.anchoredPosition = new Vector2(-770, 0);
    }

    void MoveSlider(int direction)
    {
        if (optionCursorIndex < 0 || optionCursorIndex >= sliderSetting.Length) return;

        SliderSetting currentSlider = sliderSetting[optionCursorIndex];

        if (optionCursorIndex == 3)
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

            if (direction > 0) currentSlider.sliderIndex = 1;
            if (direction < 0) currentSlider.sliderIndex = 0;

            if (onoff != currentSlider.sliderIndex)
            {
                UpdateFollow(currentSlider.sliderIndex);
            }

            SaveSettings(); // 設定を保存
            return;
        }

        currentSlider.sliderIndex += direction;
        if (currentSlider.sliderIndex < 0) currentSlider.sliderIndex = 0;
        if (currentSlider.sliderIndex >= sliderPosX.Length) currentSlider.sliderIndex = sliderPosX.Length - 1;

        Vector3 pos = currentSlider.handle.anchoredPosition;
        pos.x = sliderPosX[currentSlider.sliderIndex];
        currentSlider.handle.anchoredPosition = pos;

        if (optionCursorIndex == 2)
        {
            UpdateLighting();
        }

        SaveSettings(); // 設定を保存
    }

    // カーソル移動
    void MoveCursor(int direction)
    {
        bool isCombo = (Time.time - lastInputTime) < inputComboCnt;
        lastInputTime = Time.time;

        // カーソルの位置調節
        cursorIndex += direction;
        if(cursorIndex < 0) cursorIndex = cursorPosY.Length - 1;
        if(cursorIndex >= cursorPosY.Length) cursorIndex = 0;

        Vector3 pos = cursor.anchoredPosition;
        pos.y = cursorPosY[cursorIndex];
        cursor.anchoredPosition = pos;

        if(!isCombo) cursorMain.anchoredPosition = new Vector2(-427, 0);
    }

    private IEnumerator FadeWait(float wait)
    {
        yield return new WaitForSeconds(wait);
        isFadeIn = true;
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

    private bool AnyGamepadButtonPressed()
    {
        var gp = Gamepad.current;
        if (gp == null) return false;

        foreach (var control in gp.allControls)
        {
            if (control is UnityEngine.InputSystem.Controls.ButtonControl button
                && button.wasPressedThisFrame)
            {
                return true;
            }
        }
        return false;
    }
}
