using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class FollowUI : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("メインカメラ")]
    [SerializeField] private Transform cameraTransform;
    [Tooltip("追従対象外")]
    [SerializeField] private GameObject[] notFollowObject;

    [Header("揺れの設定")]
    [Tooltip("カメラの回転に対するUIの反応感度")]
    [SerializeField] private float swaySensitivity = 5.0f;
    [Tooltip("揺れの最大幅")]
    [SerializeField] private float maxOffset = 100.0f;

    [Header("バネ")]
    [Tooltip("バネの硬さ：高いほど素早く中央に戻る")]
    [SerializeField] private float stiffness = 150.0f;
    [Tooltip("減衰：低いとビヨンビヨン、高いとピタッと止まる")]
    [SerializeField] private float damping = 12.0f;

    private RectTransform rectTransform;
    private float lastYaw;
    private float currentOffset;
    private float velocity;

    private float originalX;

    public bool followON = false;

    public bool isPaused = false;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        // 未指定なら自動でMainCameraを探す
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    void OnEnable()
    {
        // 有効化した瞬間に大きな隙間（ワープ）による大ジャンプが起きないよう初期化
        if (cameraTransform != null)
        {
            lastYaw = cameraTransform.eulerAngles.y;
        }
        currentOffset = 0f;
        velocity = 0f;

        originalX = rectTransform.anchoredPosition.x;
    }

    void Update()
    {
        if (isPaused) return;
        if (cameraTransform == null) return;

        Vector2 anchoredPos = rectTransform.anchoredPosition;

        if (!followON)
        {
            if (Mathf.Abs(currentOffset) > 0.01f)
            {
                currentOffset = Mathf.Lerp(currentOffset, 0f, Time.deltaTime * 10f);
                anchoredPos = rectTransform.anchoredPosition;
                anchoredPos.x = originalX + currentOffset;
                rectTransform.anchoredPosition = anchoredPos;
            }
            else
            {
                currentOffset = 0f;
                velocity = 0f;
                anchoredPos = rectTransform.anchoredPosition;
                anchoredPos.x = originalX;
                rectTransform.anchoredPosition = anchoredPos;
            }

            // ★超重要：OFFの間もカメラの現在の角度を記録し続ける
            // これをしないと、OFF中にカメラを回して、ONにした瞬間に「大ワープ」が起きます
            lastYaw = cameraTransform.eulerAngles.y;
            return;
        }

        // カメラの水平回転（Yaw）の変化量を計算
        float currentYaw = cameraTransform.eulerAngles.y;
        float deltaYaw = Mathf.DeltaAngle(lastYaw, currentYaw);
        lastYaw = currentYaw;

        // カメラの動きに応じた慣性力を速度に直接加える
        // 左に旋回（deltaYawがマイナス）したとき、UIが右（プラス方向）にズレるように符号を反転
        float impulse = -deltaYaw * swaySensitivity;
        velocity += impulse;

        // フレームレートが極端に落ちた際の物理の破綻を防ぐため、1フレームの時間を制限
        float dt = Mathf.Min(Time.deltaTime, 0.03f);

        // 物理演算（バネ・ダンパー方程式）
        // F = -k * x - c * v （バネ引き戻し力 ＋ 抵抗力）
        float springForce = -stiffness * currentOffset;
        float dampingForce = -damping * velocity;
        float acceleration = springForce + dampingForce;

        // 速度と位置を更新
        velocity += acceleration * dt;
        currentOffset += velocity * dt;

        // 最大移動幅（maxOffset）でクランプし、壁にぶつかったら速度を0にする
        if (Mathf.Abs(currentOffset) > maxOffset)
        {
            currentOffset = Mathf.Clamp(currentOffset, -maxOffset, maxOffset);
            velocity = 0f;
        }

        // 実際にUIの座標（X軸のみ）に適用
        anchoredPos.x = originalX + currentOffset;
        rectTransform.anchoredPosition = anchoredPos;
    }

    public void ResumeFollow()
    {
        // 今の位置を新しい基準にしてから再開する
        originalX = rectTransform.anchoredPosition.x;
        currentOffset = 0f;
        velocity = 0f;
        lastYaw = cameraTransform != null ? cameraTransform.eulerAngles.y : lastYaw;
        isPaused = false;
    }
}