using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

public class enemy_Boss : MonoBehaviour
{
    [Header("参照設定")]
    public Transform bossMesh;     // ボス全体のメッシュ
    public Transform targetPlayer;

    [Header("調整パラメータ")]
    public float armSpeed = 10.0f;
    public float retrunSpeed = 30.0f;
    public float moveSpeed = 10.0f;
    public float targetDistance = 10.0f;
    public float stopDistance = 5.0f;

    private enum ArmState
    {
        Idle,
        Flying,
        Fall,
        Hit,
        Returning
    }

    private enum BossState
    {
        Idle,
        Move,
        SmashNormal,
        SmashBig,
        Rush,
        Punch,
        Summon,
        Down
    }

    private BossState bossState = BossState.Idle;
    private ArmState armState = ArmState.Idle;
    private Vector3 targetPosition;
    private float attackTimer = 0.0f;

    public Transform rBos;
    public Transform target;
    public float aimSpeed = 10.0f;

    private bool startAttack = false;
    [SerializeField] private Animator anim;

    [Header("ロボット腕の追尾設定")]
    public Transform armBone_R;
    public Transform armBone_L;

    public bool isTracking = false; // 追尾中かどうか

    public float transitionSpeed = 7f;

    private Vector3 currentOffset = Vector3.zero;
    private Vector3 lockedOffset = Vector3.zero;

    private Vector3 punchPos;
    private Quaternion punchRot;
    private Vector3 nowPunchPos;

    [Header("ロケットパンチ")]
    public Vector3 punchRotationOffset = new Vector3(-30, 120, 0);
    public Vector3 fallRotationOffset = new Vector3(-30, 120, 30);
    public float lockOffFrame = 1.5f;      // 追尾解除フレーム
    public float fallFrame = 2.0f;         // 着弾開始フレーム
    public float retrunFrame = 3.0f;       // 引き戻し開始フレーム
    public int attackDamage = 5;           // ダメージ量
    public float punchHitRadius = 1.4f;    // 当たり判定の半径
    public float minFlyingHeight = 1.0f;   // 飛行中の最低高度
    public float fallGroundOffset = 0.6f;  // 着弾時に地面からどれくらい浮かすか
    public bool isLeftArmDetached = false; // 左腕が分離しているかどうか

    [SerializeField] private LayerMask groundLayer; // 地面のレイヤー
    [SerializeField] private PunchArm sepaArm;      // 左腕分離用スクリプト

    private bool hasSmashHit = false; // 叩きつけの多段ヒット防止フラグ

    [Header("行動パターン")]
    public bool isStartAction = false;  // ボスの行動開始
    public float actionInterval = 6.0f; // 行動間隔（秒）
    private float actionTimer = 0.0f;   // 行動タイマー

    [SerializeField] private BossState[] addActions; // 行動パターンのリスト

    [Header("アニメーションスキップ")]
    [Tooltip("パンチのステート名")]
    [SerializeField] private string punchStateName = "Punch_v3";
    [Tooltip("パンチアニメーションスキップ")]
    [SerializeField] private float punchAnimReturnTime = 4.0f;

    void Start()
    {
        anim = GetComponent<Animator>();
        anim.SetBool("Idol", true);
    }

    public void SeparateArm()
    {
        if (sepaArm != null)
        {
            sepaArm.DetachArm();
            isLeftArmDetached = true;
        }
    }
    private Vector3 baseAnimPos_L;
    private Quaternion baseAnimRot_L;
    private bool isFirstFrameHit = false;

    void LateUpdate()
    {
        if (armBone_R == null || armBone_L == null) return;

        // 毎フレームのアニメーション自体の位置と回転を取得（左右）
        Vector3 rawAnimPos_R = armBone_R.position;
        Quaternion rawAnimRot_R = armBone_R.rotation;

        Vector3 rawAnimPos_L = armBone_L.position;
        Quaternion rawAnimRot_L = armBone_L.rotation;

        if (isFirstFrameHit)
        {
            baseAnimPos_L = rawAnimPos_L;
            baseAnimRot_L = rawAnimRot_L;
            isFirstFrameHit = false;
        }

        if (bossState == BossState.SmashNormal)
        {
            // 叩きつけ(右腕)
            if (isTracking)
            {
                Vector3 targetPos = targetPlayer.position;
                Vector3 targetOffset = new Vector3(targetPos.x - rawAnimPos_R.x, 0, targetPos.z - rawAnimPos_R.z);
                currentOffset = Vector3.Lerp(currentOffset, targetOffset, transitionSpeed * Time.deltaTime);
                lockedOffset = currentOffset;
            }
            else
            {
                currentOffset = Vector3.Lerp(currentOffset, Vector3.zero, transitionSpeed * Time.deltaTime);
                if (lockedOffset != Vector3.zero) currentOffset = lockedOffset;
            }

            // 右腕には計算したズレを適用
            armBone_R.position = rawAnimPos_R + currentOffset;

            CheckSmashHit();

            // 叩きつけ中、左腕は通常通りアニメーションの動きをさせる
            armBone_L.position = rawAnimPos_L;
            armBone_L.rotation = rawAnimRot_L;
        }
        else if (bossState == BossState.Punch)
        {
            // ロケットパンチ(左腕)
            if (armState == ArmState.Flying)
            {
                if (isTracking)
                {
                    // プレイヤーの少し上を狙う
                    Vector3 aimPoint = targetPlayer.position + Vector3.up * 0.8f;

                    // 空中で止まらないように、ターゲット位置をプレイヤーの奥に延長
                    Vector3 directionToPlayer = (aimPoint - punchPos).normalized;
                    targetPosition = aimPoint + directionToPlayer * 20.0f;
                }

                punchPos = Vector3.MoveTowards(punchPos, targetPosition, armSpeed * Time.deltaTime);

                Vector3 rayOrigin = punchPos + Vector3.up * 5.0f;
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit flyHit, 10.0f, groundLayer))
                {
                    // 地面の高さ ＋ 最低限確保したい高さ
                    float minHeight = flyHit.point.y + minFlyingHeight;
                    if (punchPos.y < minHeight)
                    {
                        // それより下に行こうとしていたら、高さを最低高度に補正する
                        punchPos.y = minHeight;
                    }
                }

                Vector3 dir = targetPosition - punchPos;
                if (dir != Vector3.zero)
                {
                    // ターゲットの方向を向くベースの回転（Z軸が前を向く）
                    Quaternion baseRotation = Quaternion.LookRotation(dir);

                    // インスペクターで設定したボーンのズレを直すための補正回転
                    Quaternion offsetRotation = Quaternion.Euler(punchRotationOffset);

                    // 2つを掛け合わせる
                    Quaternion targetRotation = baseRotation * offsetRotation;

                    // スムーズに回転させる
                    punchRot = Quaternion.Slerp(punchRot, targetRotation, aimSpeed * Time.deltaTime);
                }

                if (!isLeftArmDetached)
                {
                    armBone_L.position = punchPos;
                    armBone_L.rotation = punchRot;
                }
                CheckPunchHit();
            }
            else if (armState == ArmState.Fall)
            {
                // 位置：着弾点へ
                punchPos = Vector3.MoveTowards(punchPos, fallPoint, armSpeed * Time.deltaTime);
                // 回転：着弾点方向（＝だいたい真下）
                Vector3 dir = fallPoint - punchPos;
                if (dir.sqrMagnitude > 0.001f)
                {
                    Quaternion baseRot = Quaternion.LookRotation(dir.normalized);
                    // 飛行用 (-30,120,0) とは別に、刺し用オフセットを用意すると調整しやすい
                    Quaternion fallOffset = Quaternion.Euler(fallRotationOffset); // まず同じ値で試す
                    punchRot = Quaternion.Slerp(punchRot, baseRot * fallOffset, aimSpeed * Time.deltaTime);
                }
                if (!isLeftArmDetached)
                {
                    armBone_L.position = punchPos;
                    armBone_L.rotation = punchRot;
                }
                // 着弾完了 → Returning
                //if (Vector3.Distance(punchPos, fallPoint) < 0.05f)
                //    armState = ArmState.Returning;
            }
            else if (armState == ArmState.Returning)
            {
                punchPos = Vector3.MoveTowards(punchPos, rawAnimPos_L, retrunSpeed * Time.deltaTime);
                punchRot = Quaternion.Slerp(punchRot, rawAnimRot_L, aimSpeed * Time.deltaTime);

                if (Vector3.Distance(punchPos, rawAnimPos_L) < 0.05f)
                {
                    armState = ArmState.Idle;
                    bossState = BossState.Idle;
                    startAttack = false;
                    attackTimer = 0.0f;
                    anim.SetBool("Idol", true);
                }
                if (!isLeftArmDetached)
                {
                    armBone_L.position = punchPos;
                    armBone_L.rotation = punchRot;
                }
                CheckPunchHit();
            }
            else if (armState == ArmState.Hit)
            {
                Vector3 animPosDelta = rawAnimPos_L - baseAnimPos_L;
                Quaternion animRotDelta = rawAnimRot_L * Quaternion.Inverse(baseAnimRot_L);

                if (!isLeftArmDetached)
                {
                    armBone_L.position = nowPunchPos + animPosDelta;
                    armBone_L.rotation = animRotDelta * punchRot;
                }
            }

                // ロケットパンチ中、右腕は通常通りアニメーションの動きをさせる
                armBone_R.position = rawAnimPos_R;
            armBone_R.rotation = rawAnimRot_R;
        }
        else
        {
            // 攻撃時以外
            lockedOffset = Vector3.zero;
            currentOffset = Vector3.Lerp(currentOffset, Vector3.zero, transitionSpeed * Time.deltaTime);

            // 右腕は叩きつけのズレをゼロに戻す処理を適用
            armBone_R.position = rawAnimPos_R + currentOffset;
            if (!isLeftArmDetached)
            {
                // 左腕は左腕本来の位置をそのまま適用
                armBone_L.position = rawAnimPos_L;
                armBone_L.rotation = rawAnimRot_L;
            }
        }
    }

    void Update()
    {
        if (target != null && bossMesh != null)
        {
            Vector3 direction = target.position - transform.position;
            direction.y = 0;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, aimSpeed * Time.deltaTime);

            if (Input.GetKeyDown(KeyCode.L))
            {
                bossState = BossState.Punch;
            }

            if (Input.GetKeyDown(KeyCode.O))
            {
                HitToArm();
            }

            if (Input.GetKeyDown(KeyCode.K))
            {
                bossState = BossState.Move;
            }
            if (Input.GetKeyDown(KeyCode.M))
            {
                SeparateArm();
            }
        }

        if (bossState == BossState.Idle)
        {
            actionTimer += Time.deltaTime;

            if (actionTimer >= actionInterval)
            {
                NextAction();
                actionTimer = 0.0f;
            }
        }

        switch (bossState)
        {
            case BossState.Idle:
                anim.SetBool("Move", false);
                anim.SetBool("Idol", true);
                break;
            case BossState.Move:
                float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);

                if (distanceToPlayer > stopDistance)
                {
                    Vector3 moveTarget = targetPlayer.position;
                    moveTarget.y = transform.position.y; // Y軸固定

                    transform.position = Vector3.MoveTowards(transform.position, moveTarget, moveSpeed * Time.deltaTime);

                    anim.SetBool("Move", true);
                    anim.SetBool("Idol", false);
                }
                else
                {
                    bossState = BossState.SmashNormal;
                    //bossState = BossState.Rush;
                }
                break;
            case BossState.SmashNormal:
                if (!startAttack)
                {
                    anim.SetBool("Move", false);
                    anim.SetBool("Idol", false);
                    anim.SetTrigger("Smash_N");
                    startAttack = true;

                    hasSmashHit = false;
                }

                if (attackTimer >= 0.3f) isTracking = true;  // 追尾ON
                if (attackTimer >= 1.1f) isTracking = false; // 追尾OFF

                attackTimer += Time.deltaTime;

                if (attackTimer >= 3.3f)
                {
                    bossState = BossState.Idle;
                    attackTimer = 0.0f;
                    startAttack = false;
                }
                break;
            case BossState.SmashBig:
                if (!startAttack)
                {
                    anim.SetBool("Move", false);
                    anim.SetBool("Idol", false);
                    anim.SetTrigger("Smash_B");
                    startAttack = true;
                }
                break;
            case BossState.Punch:
                if (!startAttack)
                {
                    anim.SetBool("Move", false);
                    anim.SetBool("Idol", false);
                    anim.SetTrigger("Punch");
                    startAttack = true;
                }
                attackTimer += Time.deltaTime;

                if (attackTimer > lockOffFrame)
                {
                    isTracking = false;
                }

                if (attackTimer > fallFrame && armState == ArmState.Flying)
                {
                    armState = ArmState.Fall;
                    CalcImpactPoint();
                }
                if (attackTimer > retrunFrame && armState == ArmState.Fall)
                {
                    armState = ArmState.Returning;
                }
                break;
            case BossState.Summon:
                if (!startAttack)
                {
                    anim.SetBool("Move", false);
                    anim.SetBool("Idol", false);
                    anim.SetTrigger("Summon");
                    startAttack = true;
                }
                break;
            case BossState.Rush:
                if (!startAttack)
                {
                    anim.SetBool("Move", false);
                    anim.SetBool("Idol", false);
                    anim.SetTrigger("Rush");
                    startAttack = true;
                }
                attackTimer += Time.deltaTime;
                if (attackTimer >= 3.3f)
                {
                    bossState = BossState.Idle;
                    attackTimer = 0.0f;
                    startAttack = false;
                }
                break;
            case BossState.Down:
                anim.SetBool("Move", false);
                anim.SetBool("Idol", false);
                // amim.SetBool("Down", true);
                break;
        }
    }

    public void FirePunch()
    {
        bossState = BossState.Punch;
        armState = ArmState.Flying;

        // 飛んでいく直前の、腕の初期位置と回転を記憶
        punchPos = armBone_L.position;
        punchRot = armBone_L.rotation;

        attackTimer = 0.0f;
        isTracking = true;

        Debug.Log("パンチ発射");
    }

    private Vector3 fallPoint;
    private bool hasFallPoint;
    void CalcImpactPoint()
    {
        Vector3 rayStart = punchPos + Vector3.up * 2.0f;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 10.0f, groundLayer))
        {
            fallPoint = hit.point + Vector3.up * fallGroundOffset;

            float minHeight = hit.point.y + minFlyingHeight;
            if (punchPos.y <= minHeight + 0.05f || punchPos.y >= minHeight + 0.05f)
            {
                fallPoint = hit.point + Vector3.up * (fallGroundOffset - 0.5f);
            }
        }
        else
        {
            fallPoint = new Vector3(punchPos.x, 1f, punchPos.z);
        }

        hasFallPoint = true;
    }

    private void CheckPunchHit()
    {
        // 分離した後の腕（物理オブジェクト）になっている場合は判定しない
        if (isLeftArmDetached) return;

        // 腕の現在位置（punchPos）を中心に、指定した半径の球体内にあるコライダーをすべて取得
        Collider[] hitColliders = Physics.OverlapSphere(punchPos, punchHitRadius);
        foreach (var hit in hitColliders)
        {
            // 自分自身（ボス本体）のコライダーは無視する
            if (hit.transform.root == transform.root) continue;

            // 衝突した相手に Controller（プレイヤー）が付いているか確認
            var player = hit.GetComponent<Controller>();
            if (player != null)
            {
                // ダメージを与える
                player.TakeDamage(attackDamage);

                armState = ArmState.Returning;
                attackTimer = retrunFrame;

                // アニメーションを「引き戻し開始フレーム」へ強制ジャンプ
                anim.PlayInFixedTime(punchStateName, 0, punchAnimReturnTime);

                break;
            }
        }
    }

    private void CheckSmashHit()
    {
        // 多段ヒット防止
        if (hasSmashHit) return;

        Collider[] hitColliders = Physics.OverlapSphere(armBone_R.position, punchHitRadius);
        foreach (var hit in hitColliders)
        {
            if (hit.transform.root == transform.root) continue;

            var player = hit.GetComponent<Controller>();
            if (player != null)
            {
                player.TakeDamage(attackDamage);

                hasSmashHit = true;

                break;
            }
        }

        if (attackTimer >= 1.7f) hasSmashHit = true;
    }

    private void NextAction()
    {
        if (addActions == null || addActions.Length == 0) return;

        if (isStartAction)
        {
            // リストの中からランダムで1つ選ぶ
            int randomIndex = Random.Range(0, addActions.Length);
            BossState nextState = addActions[randomIndex];

            // 選んだステートに切り替える
            bossState = nextState;

            Debug.Log("10秒経過");
        }
    }

    public void HitToArm()
    {
        if (!isLeftArmDetached && bossState == BossState.Punch)
        {
            armState = ArmState.Hit;
            nowPunchPos = punchPos; 
            anim.SetTrigger("Hit_L");

            isFirstFrameHit = true; // 初回フレームでアニメーション基準値を取得するためのフラグ
            attackTimer = 0.0f;
        }
    }
}