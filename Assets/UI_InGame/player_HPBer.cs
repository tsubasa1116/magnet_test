using UnityEngine;
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

    [Header("Player Tracking")]
    [SerializeField] private Controller player;

    [Header("HP_パラメータ")]
    public  float maxHP = 100.0f;
    private float currentHP;
    private float displayHP;
    private float backDisplayHP;
    private float waitTimer = 0f;

    [Header("Shake_パラメータ")]
    [SerializeField] private RectTransform shakeTarget; // 揺らす対象（指定しなければこのスクリプトがついているオブジェクト）
    [SerializeField] private float shakeDuration = 0.3f; // 揺れる時間
    [SerializeField] private float shakeMagnitude = 10f; // 揺れの強さ
    private float shakeTimer = 0f;
    private Vector2 initialPosition;

    [Range(0, 100)] public float testHP = 100f;

    private float hpVel = 0.0f;
    private float startHP; // アニメーション開始時のHP
    private float easingTimer = 1.0f; // 01まで進むタイマー（1以上で停止）


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(player != null)
        {
            maxHP = player.hp;
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
            if (currentHP > player.hp)
            {
                shakeTimer = shakeDuration;
                waitTimer = backWait;
            }
            currentHP = player.hp;
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
    }
}
