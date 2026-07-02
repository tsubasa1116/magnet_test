using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class player_HPBer : MonoBehaviour
{
    [Header("HP_UI")]
    [SerializeField] private Image HPBer;
    [SerializeField] private Image backHPBer;
    [SerializeField] private RectTransform nowPoint;
    [SerializeField] private float smoothSpeed = 20.0f;
    [SerializeField] private float backSpeed = 10.0f;
    [SerializeField] private float backWait = 1.0f;

    [Header("HP_Numbers")]
    [SerializeField] private Sprite[] numberSprite;
    [SerializeField] private Image[] digitImage;
    [SerializeField] private bool zeroPadding = false;

    [Header("Player Tracking")]
    [SerializeField] private PlayerHealth player;

    [Header("HP_パラメータ")]
    public float maxHP = 100.0f;
    private float currentHP;
    private float displayHP;
    private float backDisplayHP;
    private float waitTimer = 0f;

    [Header("Shake_パラメータ")]
    [SerializeField] private RectTransform shakeTarget;  // 揺らす対象（指定しなければこのスクリプトがついているオブジェクト）
    [SerializeField] private float shakeDuration = 0.3f; // 揺れる時間
    [SerializeField] private float shakeMagnitude = 10f; // 揺れの強さ
    private float shakeTimer = 0f;
    private Vector2 initialPosition;

    [Header("心電図")]
    [SerializeField] private Image ecgImage;               // 心電図を表示するUI Image
    [SerializeField] private Image ecgImageSub;
    [SerializeField] private Sprite[] ecgSprite01;           // 9x5で分割したスプライト配列（計45枚）
    [SerializeField] private Sprite[] ecgSprite02;
    [SerializeField] private Sprite[] ecgSprite03;
    [SerializeField] private float baseFps = 15.0f;           // 通常時（HP満タン）の1秒あたりのフレーム数
    [SerializeField] private bool speedUpOnLowHP = true;   // HP低下時にアニメーションを加速させるか
    [SerializeField] private float maxSpeedUp = 3.0f; // HPが0に近いときの最大加速倍率

    private float ecgTimer = 0f;
    private int ecgFrameIndex = 0;

    [Range(0, 100)] public float testHP = 100f;

    private float hpVel = 0.0f;
    private float startHP; // アニメーション開始時のHP
    private float easingTimer = 1.0f; // 01まで進むタイマー（1以上で停止）


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (player != null)
        {
            maxHP = player.Hp;
        }
        currentHP = maxHP;
        displayHP = maxHP;
        backDisplayHP = maxHP;
        startHP = maxHP;

        if (shakeTarget == null)
        {
            shakeTarget = GetComponent<RectTransform>();
        }
        if (shakeTarget != null)
        {
            initialPosition = shakeTarget.anchoredPosition;
        }

        UpdateHPBar();
    }

    void Update()
    {
        // イージング用
        {
            //if (player != null)
            //{
            //    if (player.hp != currentHP)
            //    {
            //        startHP = displayHP;   // 現在の表示位置からスタート
            //        currentHP = player.hp; // 新しい目標値を設定
            //        easingTimer = 0f;      // タイマーを0にリセットしてアニメ開始
            //    }
            //}

            //// タイマーが1になるまでアニメーションを計算
            //if (easingTimer < 1.0f)
            //{
            //    //easingTimer += Time.deltaTime * smoothSpeed; // パターン4 2
            //    easingTimer += Time.deltaTime / smoothSpeed;   // パターン5 0.25
            //    float t = Mathf.Clamp01(easingTimer);

            //    //float rate = t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f; // パターン4

            //    float rate = 1f - Mathf.Pow(1f - t, 3f); // パターン5

            //    // 計算したイージングの割合（rate）を使ってHPを補間
            //    displayHP = Mathf.Lerp(startHP, currentHP, rate);
            //    UpdateHPBar();
            //}
        }

        if (player != null)
        {
            // HPが減った場合にシェイクを開始する
            if (currentHP > player.Hp)
            {
                shakeTimer = shakeDuration;
                waitTimer = backWait;
            }
            currentHP = player.Hp;
        }

        //currentHP = testHP;
        //displayHP = Mathf.Lerp(displayHP, currentHP, Time.deltaTime * smoothSpeed);        // 線形補間 5
        //displayHP = Mathf.SmoothDamp(displayHP, currentHP, ref hpVel, smoothSpeed);        // スムーズダンピング 7
        displayHP = Mathf.MoveTowards(displayHP, currentHP, Time.deltaTime * smoothSpeed); // 一定速度 20
        if (waitTimer > 0f)
        {
            waitTimer -= Time.deltaTime;
        }
        else
        {
            backDisplayHP = Mathf.MoveTowards(backDisplayHP, currentHP, Time.deltaTime * backSpeed);
        }
        UpdateHPBar();

        HandleShake();
        HandleECGAnimation();
    }

    void HandleShake()
    {
        if (shakeTarget == null) return;

        if (shakeTimer > 0)
        {
            shakeTimer -= Time.deltaTime;

            // 時間経過に合わせて揺れを徐々に小さくする
            float currentMagnitude = shakeMagnitude * (shakeTimer / shakeDuration);

            float offsetX = Random.Range(-1f, 1f) * currentMagnitude;
            float offsetY = Random.Range(-1f, 1f) * currentMagnitude;

            shakeTarget.anchoredPosition = initialPosition + new Vector2(offsetX, offsetY);
        }
        else if (shakeTarget.anchoredPosition != initialPosition)
        {
            // シェイクが終わったら元の位置に戻す
            shakeTarget.anchoredPosition = initialPosition;
        }
    }

    void UpdateHPBar()
    {
        if (HPBer != null)
        {
            HPBer.fillAmount = displayHP / maxHP;
        }

        if (backHPBer != null)
        {
            backHPBer.fillAmount = backDisplayHP / maxHP;
        }

        if (nowPoint != null)
        {
            float hpPer = displayHP / maxHP;
            float targeetAngle = (1.0f - hpPer) * 360.0f;
            nowPoint.localRotation = Quaternion.Euler(0, 0, -targeetAngle);
        }

        UpdateHPNumber();
    }

    void UpdateHPNumber()
    {
        // 表示用HPを整数にする（0未満にならないようにMathf.Maxを使用、四捨五入や切り上げなどはお好みで変更可）
        int hpInt = Mathf.Max(0, (int)displayHP);

        // 1の位のImageから順番に処理
        for (int i = 0; i < digitImage.Length; i++)
        {
            if (digitImage[i] == null) continue;

            // 1番下の桁を取得する (100なら、100/10＝あまり0)
            int digit = hpInt % 10;
            hpInt /= 10; // 次の桁を計算するために10で割る

            // スプライト割り当て
            digitImage[i].sprite = numberSprite[digit];

            // 0埋め
            if (!zeroPadding && displayHP < Mathf.Pow(10, i) && i > 0)
            {
                digitImage[i].enabled = false;
            }
            else
            {
                digitImage[i].enabled = true;
            }
        }

    }

    void HandleECGAnimation()
    {
        if (ecgImage == null || ecgSprite01 == null || ecgSprite01.Length == 0 ||
            ecgSprite02 == null || ecgSprite02.Length == 0 || ecgSprite03 == null || ecgSprite03.Length == 0) return;
        
        float speedByLowHP = 1.0f;

        // HPが低いときにアニメーションを加速させる計算
        if (speedUpOnLowHP && maxHP > 0)
        {
            // 0~1にしてバグ防止
            float hpRatio = Mathf.Clamp01(displayHP / maxHP);
            // HP100 ＝ 1倍、HP0 ＝ maxSpeedMultiplier倍
            speedByLowHP = Mathf.Lerp(maxSpeedUp, 1.0f, hpRatio);
        }
        // タイマーを進める
        ecgTimer += Time.deltaTime * speedByLowHP;
        // 1フレームあたりの時間
        float timePerFrame = 1.0f / baseFps;
        // タイマーが1フレームの時間を超えたらコマを進める
        if (ecgTimer >= timePerFrame)
        {
            int framesToAdvance = Mathf.FloorToInt(ecgTimer / timePerFrame);

            Sprite[] targetSprite;

            if (currentHP <= 100 && currentHP >= 50)
            {
                targetSprite = ecgSprite01;
            }
            else if (currentHP < 50 && currentHP >= 20)
            {
                targetSprite = ecgSprite02;
            }
            else
            {
                targetSprite = ecgSprite03;
            }

            
            // 次のフレームインデックスを計算
            ecgFrameIndex = (ecgFrameIndex + framesToAdvance) % targetSprite.Length;
            // 余った時間をタイマーに残す
            ecgTimer %= timePerFrame;
            if (targetSprite[ecgFrameIndex] != null)
            {
                ecgImage.sprite = targetSprite[ecgFrameIndex];

                int prevFrameIndex = (ecgFrameIndex - 1 + targetSprite.Length) % targetSprite.Length;
                if (targetSprite[prevFrameIndex] != null && ecgImageSub != null)
                {
                    ecgImageSub.sprite = targetSprite[prevFrameIndex];
                }
            }
        }
    }
}
