using UnityEngine;
using UnityEngine.UI;

public class player_HPBer : MonoBehaviour
{
    [Header("HP_UI")]
    [SerializeField] private Image HPBer;
    [SerializeField] private RectTransform nowPoint;
    [SerializeField] private float smoothSpeed = 5.0f;

    [Header("Player Tracking")]
    [SerializeField] private Controller player;

    [Header("HP_パラメータ")]
    public  float maxHP = 100.0f;
    private float currentHP;
    private float displayHP;

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
        startHP = maxHP;
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
            currentHP = player.hp;
        }

        //currentHP = testHP;
        //displayHP = Mathf.Lerp(displayHP, currentHP, Time.deltaTime * smoothSpeed);        // 線形補間 5
        //displayHP = Mathf.SmoothDamp(displayHP, currentHP, ref hpVel, smoothSpeed);        // スムーズダンピング 7
        displayHP = Mathf.MoveTowards(displayHP, currentHP, Time.deltaTime * smoothSpeed); // 一定速度 20
        UpdateHPBar();
    }

    void UpdateHPBar()
    {
        float hpPer = displayHP / maxHP;
        if (HPBer != null)
        {
            HPBer.fillAmount = hpPer;
        }

        if (nowPoint != null)
        {
            float targeetAngle = (1.0f - hpPer) * 360.0f;

            nowPoint.localRotation = Quaternion.Euler(0, 0, -targeetAngle);
        }
    }
}
