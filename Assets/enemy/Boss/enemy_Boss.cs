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
    public float maxHP = 100.0f;
    public float takenDamage = 5.0f;
    public float revivTime = 5.0f;

    private bool isLookPlayer = true;

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
    private bool isDown = false;
    [SerializeField] private Animator anim;

    [Header("ロボット腕の追尾設定")]
    public Transform armBone_R;
    public Transform armBone_L;

    public bool isTracking = false; // 追尾中かどうか

    public float transitionSpeed = 7f;

    private Vector3 currentOffset = Vector3.zero;
    private Vector3 lockedOffset = Vector3.zero;
    public float  trackingOffset = 2.0f;

    private Vector3 punchPos;
    private Quaternion punchRot;
    private Vector3 nowPunchPos;

    [Header("衝撃波のエフェクト設定")]
    [SerializeField] private GameObject waveEffect;
    [SerializeField] private float[] smashEffectTimes = new float[] { 2.0f, 2.7f, 3.5f };
    [SerializeField] private float effectPosY = 1.1f;

    private int smashEffectCnt = 0; // エフェクトを発生させた回数のカウント

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
    public bool isRightArmDetached = false; // 右腕が分離しているかどうか

    [SerializeField] private LayerMask groundLayer; // 地面のレイヤー
    [SerializeField] private PunchArm sepaArm;      // 左腕分離用スクリプト
    [SerializeField] private PunchArm sepaArmR;

    private bool hasSmashHit = false; // 叩きつけの多段ヒット防止フラグ

    [Header("行動パターン")]
    public bool isStartAction = false;  // ボスの行動開始
    public float actionInterval = 6.0f; // 行動間隔（秒）
    private float actionTimer = 0.0f;   // 行動タイマー
    public bool isSecond = false;       // 2段階目の行動パターンかどうか

    [SerializeField] private BossState[] addActions; // 行動パターンのリスト
    [SerializeField] private BossState[] addActionSecond; // 行動パターンのリスト

    [Header("アニメーションスキップ")]
    [Tooltip("パンチのステート名")]
    [SerializeField] private string punchStateName = "Punch_v3";
    [Tooltip("パンチアニメーションスキップ")]
    [SerializeField] private float punchAnimReturnTime = 4.0f;

    [Header("腕分離・復活設定")]
    public float detachDelay = 1.5f;
    private bool isWaitForDetach = false;
    private float detachTimer = 0.0f;
    private bool isWaitForDetachR = false;
    private float detachTimerR = 0.0f;

    void Start()
    {
        anim = GetComponent<Animator>();
        anim.SetBool("Idol", true);
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

            // 叩きつけ中、左腕は通常通りアニメーションの動きをさせる
            armBone_L.position = rawAnimPos_L;
            armBone_L.rotation = rawAnimRot_L;

            if (!isRightArmDetached)
            {
                armBone_R.position = rawAnimPos_R + currentOffset;
                CheckSmashHit(true); // 分離中はダメージ判定とエフェクトも出さない
            }
        }
        if (bossState == BossState.SmashBig)
        {
            // 叩きつけ(右腕)
            if (isTracking)
            {
                Vector3 targetPos = targetPlayer.position;
                if (trackingOffset > 0f)
                {
                    targetPos = Vector3.MoveTowards(targetPlayer.position, transform.position, trackingOffset);
                }
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

            // 叩きつけ中、左腕は通常通りアニメーションの動きをさせる
            armBone_L.position = rawAnimPos_L;
            armBone_L.rotation = rawAnimRot_L;

            // 右腕には計算したズレを適用
            if (!isRightArmDetached)
            {
                armBone_R.position = rawAnimPos_R + currentOffset;
                CheckSmashHit(false);
            }
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
        else if (bossState == BossState.Down)
        {
            return;
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
            if (isLookPlayer)
            {
            Vector3 direction = target.position - transform.position;
            direction.y = 0;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, aimSpeed * Time.deltaTime);
            }

            if (Input.GetKeyDown(KeyCode.Keypad1) || Input.GetKeyDown(KeyCode.Alpha1))
            {
                bossState = BossState.SmashNormal;
            }

            if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.Alpha2))
            {
                bossState = BossState.SmashBig;
            }

            if (Input.GetKeyDown(KeyCode.Keypad3) || Input.GetKeyDown(KeyCode.Alpha3))
            {
                bossState = BossState.Punch;
            }

            if (Input.GetKeyDown(KeyCode.Keypad8) || Input.GetKeyDown(KeyCode.Alpha8))
            {
                HitToArm();
            }

            if (Input.GetKeyDown(KeyCode.Keypad9) || Input.GetKeyDown(KeyCode.Alpha9))
            {
                HitToArmR();
            }

            if (Input.GetKeyDown(KeyCode.Keypad7) || Input.GetKeyDown(KeyCode.Alpha7))
            {
                bossState = BossState.Down;
            }

            if (Input.GetKeyDown(KeyCode.Keypad5) || Input.GetKeyDown(KeyCode.Alpha5))
            {
                bossState = BossState.Move;
            }

            if (Input.GetKeyDown(KeyCode.M))
            {
                ExecuteDetachArm();
            }
            if (Input.GetKeyDown(KeyCode.N))
            {
                ReviveArm();
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

                isLookPlayer = true;
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
                    if (isSecond)
                    {
                        int rand = Random.Range(0, 2);

                        switch (rand)
                        {
                            case 0:
                                bossState = BossState.SmashBig;
                                break;
                            case 1:
                                bossState = BossState.Rush;
                                break;
                        }
                    }
                    else
                    {
                        bossState = BossState.SmashNormal;
                    }
                    
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

                if (attackTimer >= 1.2f) isLookPlayer = false;

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

                    smashEffectCnt = 0;
                    hasSmashHit = false;

                }

                if (attackTimer >= 0.3f) isTracking = true;  // 追尾ON
                if (attackTimer >= 1.6f) isTracking = false; // 追尾OFF

                if (attackTimer >= 2.0f) isLookPlayer = false;

                attackTimer += Time.deltaTime;

                if (attackTimer >= 5.0f)
                {
                    bossState = BossState.Idle;
                    attackTimer = 0.0f;
                    startAttack = false;
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
                if (attackTimer >= 1.5f) isLookPlayer = false;
                attackTimer += Time.deltaTime;
                if (attackTimer >= 3.3f)
                {
                    bossState = BossState.Idle;
                    attackTimer = 0.0f;
                    startAttack = false;
                }
                break;
            case BossState.Down:
                if (!isDown)
                {
                    anim.SetBool("Move", false);
                    anim.SetBool("Idol", false);
                    anim.SetTrigger("Down");
                    anim.SetBool("isDown", true);

                    isLookPlayer = false;
                    isDown = true;
                }

                attackTimer += Time.deltaTime;

                if (attackTimer >= revivTime)
                {
                    anim.SetTrigger("Reviv");
                    bossState = BossState.Idle;
                    attackTimer = 0.0f;
                    isDown = false;
                    isLookPlayer = true;
                }

                break;
        }

        if (isWaitForDetach)
        {
            detachTimer += Time.deltaTime;
            if (detachTimer >= detachDelay)
            {
                ExecuteDetachArm();
                isWaitForDetach = false;
                anim.SetBool("Idol", true);
            }
        }

        if (isWaitForDetachR)
        {
            detachTimerR += Time.deltaTime;
            if (detachTimerR >= detachDelay)
            {
                ExecuteDetachArmR();
                isWaitForDetachR = false;
                anim.SetBool("Idol", true);
            }
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

    private void CheckSmashHit(bool N)
    {
        // 1. ダメージ判定
        if (!hasSmashHit)
        {
            Collider[] hitColliders = Physics.OverlapSphere(armBone_R.position, punchHitRadius);
            foreach (var hit in hitColliders)
            {
                if (hit.transform.root == transform.root) continue;

                var player = hit.GetComponent<Controller>();
                if (player != null)
                {
                    player.TakeDamage(attackDamage);
                    hasSmashHit = true; // 一旦ダメージ判定をオフにする
                    break;
                }
            }
        }

        // 2. エフェクト＆ログ発生処理
        if (N)
        {
            // 通常叩きつけ(SmashNormal)の処理
            if (attackTimer >= 1.7f && smashEffectCnt == 0)
            {
                Debug.Log("Smash_N");
                smashEffectCnt = 1;
            }
        }
        else
        {
            // 大叩きつけ(SmashBig)の処理：3回のエフェクトを時間差で発生
            if (smashEffectCnt < smashEffectTimes.Length)
            {
                // 現在のカウントに対応する発生時間を超えたかチェック
                if (attackTimer >= smashEffectTimes[smashEffectCnt])
                {
                    // 右手の真下の地面の高さを計算
                    Vector3 effectPos = new Vector3(armBone_R.position.x, transform.position.y + effectPosY, armBone_R.position.z);

                    // 計算した位置にエフェクトを発生
                    Instantiate(waveEffect, effectPos, Quaternion.identity);
                    Debug.Log($"Smash_B - {smashEffectCnt + 1}回目着弾！");

                    // 次のパンチのために判定復活
                    hasSmashHit = false;

                    // カウントを進めて、次のパンチのタイミング待ちにする
                    smashEffectCnt++;
                }
            }
        }
    }

    private void NextAction()
    {
        if (addActions == null || addActions.Length == 0) return;

        if (isStartAction)
        {
            if (!isSecond)
            {
                // リストの中からランダムで1つ選ぶ
                int randomIndex = Random.Range(0, addActions.Length);
                BossState nextState = addActions[randomIndex];

                // 選んだステートに切り替える
                bossState = nextState;
            }
            else
            {
                int randIdxSec = Random.Range(0, addActionSecond.Length);
                BossState nextStateSec = addActionSecond[randIdxSec];

                // 選んだステートに切り替える
                bossState = nextStateSec;
            }

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

            isWaitForDetach = true;
            detachTimer = 0.0f;
        }
    }

    public void HitToArmR()
    {
        // 右腕が分離しておらず、かつスマッシュ攻撃(通常・大)中の場合のみ処理を開始
        if (!isRightArmDetached && (bossState == BossState.SmashNormal || bossState == BossState.SmashBig))
        {
            anim.SetTrigger("Hit_R");

            // 分離待機フラグをオン
            isWaitForDetachR = true;
            detachTimerR = 0.0f;
        }
    }


    // 腕分離
    private void ExecuteDetachArm()
    {
        isLeftArmDetached = true;

        // 本体の左腕ボーンのスケールを0にして「見えなくする」
        armBone_L.localScale = Vector3.zero;

        // 分離用のダミー腕オブジェクトを出現・物理挙動させる
        if (sepaArm != null)
        {
            sepaArm.gameObject.SetActive(true);

            // 位置を合わせる
            sepaArm.transform.position = armBone_L.position;

            // PunchArm側の分離処理
            sepaArm.DetachArm();
        }
    }


    private void ExecuteDetachArmR()
    {
        isRightArmDetached = true;

        // 本体の左腕ボーンのスケールを0にして「見えなくする」
        armBone_R.localScale = Vector3.zero;

        // 分離用のダミー腕オブジェクトを出現・物理挙動させる
        if (sepaArmR != null)
        {
            sepaArmR.gameObject.SetActive(true);

            // 位置を合わせる
            sepaArmR.transform.position = armBone_R.position;

            // PunchArm側の分離処理
            sepaArmR.DetachArm();
        }
    }

    // 腕復活
    public void ReviveArm()
    {
        if (!isLeftArmDetached) return;

        isLeftArmDetached = false;

        // 本体の左腕ボーンのスケールを戻して見えるようにする
        armBone_L.localScale = Vector3.one;

        // 分離用のダミー腕を非表示にする
        if (sepaArm != null)
        {
            sepaArm.gameObject.SetActive(false);
        }

        // ステートを元に戻す
        armState = ArmState.Idle;
        // 復活モーション
    }

    public void ReviveArmR()
    {
        if (!isRightArmDetached) return;

        isRightArmDetached = false;

        // 本体の右腕ボーンのスケールを戻して見えるようにする
        armBone_R.localScale = Vector3.one;

        // 分離用のダミー腕を非表示にする
        if (sepaArmR != null)
        {
            sepaArmR.gameObject.SetActive(false);
        }

    }


}