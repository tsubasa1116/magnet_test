using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Android;
using UnityEngine.UIElements;

public class enemy_Boss : MonoBehaviour
{
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
  
    [Header("ボス全体に関する参照設定")]
    [SerializeField] private Transform bossMesh;     // ボス全体のメッシュ
    [SerializeField] private Transform bossMain;     // ボス全体のメッシュ
    [SerializeField] private Transform targetPlayer; // プレイヤーのTransformをInspectorで設定
    [SerializeField] private Transform target;　 // プレイヤーのTransformをInspectorで設定
    [SerializeField] private Animator anim;
    [SerializeField] private enemy_HPBer hpBarScript;
    [SerializeField] private Transform barrier;

    [Header("ボスパラメータ")]
    public float moveSpeed = 10.0f;      // ボスの移動速度
    public float targetDistance = 10.0f; // プレイヤーとの距離がこの値以上の時に追尾する
    public float stopDistance = 5.0f;    // プレイヤーとの距離がこの値以下の時に停止する
    public float maxHP = 100.0f;         // 最大HP
    public float currentHP;              // 現在のHP
    public float barrierMaxHP = 100.0f;  // バリアの最大HP
    public float currentBarrierHP;       // 現在のバリアHP
    public float takenDamage = 5.0f;     // 受けるダメージ量
    public float revivTime = 5.0f;       // ダウンから復活するまでの時間
    public float aimSpeed = 10.0f;　     // ボスの回転速度
    public float invincibleTime = 0.5f;  // 無敵時間

    private bool isLookPlayer = true; // プレイヤーを向くかどうかのフラグ
    private float attackTimer = 0.0f; // 攻撃の経過時間を計測するタイマー
    
    private bool startAttack = false; // 攻撃開始フラグ
    private bool isDown = false;      // ダウン中かどうかのフラグ
    private bool isInvincible = false;// 無敵状態かどうかのフラグ
    private bool isWaitRevive = false;

    [Header("腕の設定")]
    public Transform armBone_R;
    public Transform armBone_L;
    [SerializeField] private GameObject impactEffect;

    public bool isTracking = false; // 追尾中かどうか

    public float armSpeed = 10.0f;       // 腕の移動速度
    public float retrunSpeed = 30.0f;    // 腕の戻る速度
    public float transitionSpeed = 7.0f; // 腕の追尾の補間速度
    public float trackingOffset = 2.0f;  // 腕が追尾するときにプレイヤーの手前で止まる距離

    private Vector3 currentOffset = Vector3.zero;
    private Vector3 lockedOffset = Vector3.zero;

    private bool isHitR = false;
    private bool isFirstFrameHitR = false;
    private Vector3 baseAnimPos_R;
    private Quaternion baseAnimRot_R;
    private Vector3 nowSmashPosR;
    private Quaternion nowSmashRotR;

    [Header("衝撃波のエフェクト設定")]
    [SerializeField] private GameObject waveEffect;
    [SerializeField] private float[] smashEffectTimes = new float[] { 2.0f, 2.7f, 3.5f };
    [SerializeField] private float effectPosY = 1.1f;

    private int smashEffectCnt = 0; // エフェクトを発生させた回数のカウント

    [Header("ロケットパンチ")]
    [SerializeField] private GameObject rocketEffect;
    public Vector3 punchRotationOffset = new Vector3(-30, 120, 0);
    public Vector3 fallRotationOffset = new Vector3(-30, 120, 30);
    public Vector3 rocketEffectRotOffset = new Vector3(-30, 120, 30);
    public float lockOffFrame = 1.5f;      // 追尾解除フレーム
    public float fallFrame = 2.0f;         // 着弾開始フレーム
    public float retrunFrame = 3.0f;       // 引き戻し開始フレーム
    public int attackDamage = 5;           // ダメージ量
    public float punchHitRadius = 1.4f;    // 当たり判定の半径
    public float minFlyingHeight = 1.0f;   // 飛行中の最低高度
    public float fallGroundOffset = 0.6f;  // 着弾時に地面からどれくらい浮かすか


    [SerializeField] private LayerMask groundLayer; // 地面のレイヤー
    [SerializeField] private PunchArm sepaArm;      // 左腕分離用スクリプト
    [SerializeField] private PunchArm sepaArmR;

    private bool hasSmashHit = false; // 叩きつけの多段ヒット防止フラグ
    
    private Vector3 targetPosition; // ロケットパンチのターゲット位置
    
    private Vector3 baseAnimPos_L;
    private Quaternion baseAnimRot_L;
    private bool isFirstFrameHit = false;
    private Vector3 punchPos;
    private Quaternion punchRot;
    private Vector3 nowPunchPos;
    
    private Vector3 fallPoint;
    private bool hasFallPoint;

    [Header("分離した腕用")]
    [SerializeField] private float autoReviveTime = 8.0f; // 分離してから消えるまでの合計時間
    [SerializeField] private float blinkDuration = 2.0f;  // 消える何秒前から点滅を開始するか
    [SerializeField] private float blinkInterval = 0.1f;  // 点滅のチカチカする間隔

    // 途中で吸収された時に止めるための変数
    private Coroutine leftArmTimerCoroutine = null;
    private Coroutine rightArmTimerCoroutine = null;

    [Header("行動パターン")]
    public bool isStartAction = false;  // ボスの行動開始
    public float actionInterval = 6.0f; // 行動間隔（秒）
    private float actionTimer = 0.0f;   // 行動タイマー
    public bool isSecond = false;       // 2段階目の行動パターンかどうか

    [SerializeField] private BossState[] addActions; // 行動パターンのリスト
    [SerializeField] private BossState[] addActionSecond; // 行動パターンのリスト

    private bool firstSummon = false;      // 1段階目の初回召喚フラグ
    private bool secondSummon = false;     // 2段階目の初回召喚フラグ
    private bool checkStartAction = false; // isStartActionの変更検知用

    [Header("アニメーションスキップ")]
    [Tooltip("パンチのステート名")]
    [SerializeField] private string punchStateName = "Punch_v3";
    [Tooltip("パンチアニメーションスキップ")]
    [SerializeField] private float punchAnimReturnTime = 4.0f;

    [Header("腕分離・復活設定")]
    public float detachDelay = 1.5f;
    public float reviveArmTime = 0.4f;
    public bool isLeftArmDetached = false; // 左腕が分離しているかどうか
    public bool isRightArmDetached = false; // 右腕が分離しているかどうか

    private bool isWaitForDetach = false;
    private float detachTimer = 0.0f;
    private bool isWaitForDetachR = false;
    private float detachTimerR = 0.0f;

    [Header("召喚設定")]
    [SerializeField] private GameObject[] summonPrefabs; // 3種類の敵をセット
    [SerializeField] private GameObject summonEffect;
    public float summonRadius = 5.0f;                    // ボスを中心とした召喚半径
    public int summonCount = 3;                          // 一度に召喚する数

    // =========================================
    // 初期化処理
    // =========================================
    void Start()
    {
        currentHP = maxHP;
        currentBarrierHP = barrierMaxHP;

        anim = GetComponent<Animator>();
        anim.SetBool("Idol", true);
    }

    // =========================================
    // ステート用更新処理
    // =========================================
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

        if (isFirstFrameHitR)
        {
            baseAnimPos_R = rawAnimPos_R;
            baseAnimRot_R = rawAnimRot_R;
            isFirstFrameHitR = false;
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
                if(isHitR)
                {
                    Vector3 animPosDeltaR = rawAnimPos_R - baseAnimPos_R;
                    Quaternion animRotDeltaR = rawAnimRot_R * Quaternion.Inverse(baseAnimRot_R);

                    armBone_R.position = nowSmashPosR + animPosDeltaR;
                    armBone_R.rotation = animRotDeltaR * nowSmashRotR;
                }
                else
                {
                    armBone_R.position = rawAnimPos_R + currentOffset;
                    CheckSmashHit(true); // 分離中はダメージ判定とエフェクトも出さない
                }
            }
        }
        else if (bossState == BossState.SmashBig)
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
                if (isHitR)
                {
                    // Hit中はアニメーションの差分を計算して適用
                    Vector3 animPosDeltaR = rawAnimPos_R - baseAnimPos_R;
                    Quaternion animRotDeltaR = rawAnimRot_R * Quaternion.Inverse(baseAnimRot_R);

                    armBone_R.position = nowSmashPosR + animPosDeltaR;
                    armBone_R.rotation = animRotDeltaR * nowSmashRotR;
                }
                else
                {
                    armBone_R.position = rawAnimPos_R + currentOffset;
                    CheckSmashHit(false);
                }
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

    [SerializeField] private GameOverTransition gameOverTransition;
    [SerializeField] private Mission mission;

    // =========================================
    // 更新処理
    // =========================================
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

            if (Input.GetKeyDown(KeyCode.Keypad4) || Input.GetKeyDown(KeyCode.Alpha4))
            {
                bossState = BossState.Summon;
            }

            if (Input.GetKeyDown(KeyCode.Keypad6) || Input.GetKeyDown(KeyCode.Alpha6))
            {
                bossState = BossState.Rush;
            }

            if (Input.GetKeyDown(KeyCode.H))
            {
                SceneLoad.LoadDirect("ResultScene", FadeType.White);
                GameManager.Instance.EndGame();

                GameResultManager.SetResultData(
                    GameManager.Instance.TotalKillCount,
                    Mathf.FloorToInt(GameManager.Instance.ElapsedTime)
                );
            }

            if (Input.GetKeyDown(KeyCode.J))
            {
                gameOverTransition.GoToGameOver();
            }

            if (Input.GetKeyDown(KeyCode.G))
            {
                mission.SetMission(0);
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                mission.ClearMission(0);
            }

            if (Input.GetKeyDown(KeyCode.V))
            {
                mission.SetMission(1);
            }

            if (Input.GetKeyDown(KeyCode.B))
            {
                mission.ClearMission(1);
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

        if (isStartAction && !checkStartAction)
        {
            actionTimer = actionInterval;
            checkStartAction = true;
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

                //isLookPlayer = true;
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

                    smashEffectCnt = 0;
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
                    attackTimer = 0.0f;
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

                attackTimer += Time.deltaTime;

                if (attackTimer >= 1.0f)
                {
                    bossState = BossState.Idle;
                    attackTimer = 0.0f;
                    startAttack = false;
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

                    barrier.gameObject.SetActive(false);

                    isLookPlayer = false;
                    isDown = true;

                    attackTimer = 0.0f;
                }

                attackTimer += Time.deltaTime;

                if (attackTimer >= revivTime)
                {
                    anim.SetBool("isDown", false);
                    anim.SetTrigger("Reviv");
                    bossState = BossState.Idle;

                    isWaitRevive = true;

                    attackTimer = 0.0f;
                    isDown = false;
                    startAttack = false;
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

                bossState = BossState.Idle;
                startAttack = false;
                armState = ArmState.Idle;
            }
        }

        if (isWaitForDetachR)
        {
            detachTimerR += Time.deltaTime;
            if (detachTimerR >= detachDelay)
            {
                ExecuteDetachArmR();
                isWaitForDetachR = false;

                bossState = BossState.Idle;
                startAttack = false;
                armState = ArmState.Idle;
            }
        }
    }

    // =========================================
    // プレイヤー追尾（アニメーションイベント追加用）
    // =========================================
    public void eventLookPlayer()
    {
        isLookPlayer = true;
        if (isWaitRevive)
        {
            barrier.gameObject.SetActive(true);
            isWaitRevive = false;
        }
    }

    // ==============================
    // ロケットパンチエフェクト遅延生成
    // ==============================
    private IEnumerator RocketEffectDelay(float delayTime)
    {
        // 指定した時間（秒）だけ待機
        yield return new WaitForSeconds(delayTime);

        Quaternion baseRot = armBone_L.rotation;

        // 待機した後の（追尾が進んだ）腕の位置と回転を取得して生成
        Quaternion effectRot = baseRot * Quaternion.Euler(rocketEffectRotOffset);
        Vector3 effectPos = armBone_L.position;

        Instantiate(rocketEffect, effectPos, effectRot);
    }

    // ============================
    // ロケットパンチ発射処理
    // ============================
    public void FirePunch()
    {
        bossState = BossState.Punch;
        armState = ArmState.Flying;

        // 飛んでいく直前の、腕の初期位置と回転を記憶
        punchPos = armBone_L.position;
        punchRot = armBone_L.rotation;

        StartCoroutine(RocketEffectDelay(0.1f));

        attackTimer = 0.0f;
        isTracking = true;

        Debug.Log("パンチ発射");
    }

    // ============================
    // ロケットパンチの着弾位置計算
    // ============================
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

    // ============================
    // ロケットパンチのヒット判定
    // ============================
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
            var player = hit.GetComponent<PlayerHealth>();
            if (player != null)
            {
                // ダメージを与える
                player.TakeDamage(attackDamage, transform.position);

                armState = ArmState.Returning;
                attackTimer = retrunFrame;

                isInvincible = true; // 無敵状態にする

                // アニメーションを「引き戻し開始フレーム」へ強制ジャンプ
                anim.PlayInFixedTime(punchStateName, 0, punchAnimReturnTime);

                break;
            }
        }

        StartCoroutine(InvincibleCooltime());
    }

    // =========================================
    // 叩きつけ攻撃のヒット判定とエフェクト発生処理
    // =========================================
    private void CheckSmashHit(bool N)
    {
        // 1. ダメージ判定
        if (!hasSmashHit)
        {
            Collider[] hitColliders = Physics.OverlapSphere(armBone_R.position, punchHitRadius);
            foreach (var hit in hitColliders)
            {
                if (hit.transform.root == transform.root) continue;

                var player = hit.GetComponent<PlayerHealth>();
                if (player != null)
                {
                    player.TakeDamage(attackDamage, transform.position);
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
                Vector3 effectPos = new Vector3(armBone_R.position.x, transform.position.y + 0.05f, armBone_R.position.z);
                Instantiate(impactEffect, effectPos, Quaternion.identity);
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
                    Vector3 effectPosI = new Vector3(armBone_R.position.x, transform.position.y + 0.05f, armBone_R.position.z);

                    // 計算した位置にエフェクトを発生
                    Instantiate(impactEffect, effectPosI, Quaternion.identity);
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

    // ============================
    // 行動パターンの切り替え
    // ============================
    private void NextAction()
    {
        if (isStartAction)
        {
            if (!isSecond && currentHP <= 50.0f)
            {
                isSecond = true;
            }

            if (!isSecond)
            {

                if (!firstSummon)
                {
                    bossState = BossState.Summon;
                    firstSummon = true;
                }
                else
                {
                    if (addActions == null || addActions.Length == 0) return;

                    // リストの中からランダムで1つ選ぶ
                    int randomIndex = Random.Range(0, addActions.Length);
                    BossState nextState = addActions[randomIndex];

                    // 選んだステートに切り替える
                    bossState = nextState;
                }
            }
            else
            {
                if (!secondSummon)
                {
                    // 2段階目の最初の行動は確定で召喚
                    bossState = BossState.Summon;
                    secondSummon = true;
                }
                else
                {
                    if (addActionSecond == null || addActionSecond.Length == 0) return;

                    int randIdxSec = Random.Range(0, addActionSecond.Length);
                    BossState nextStateSec = addActionSecond[randIdxSec];

                    // 選んだステートに切り替える
                    bossState = nextStateSec;
                }
            }

            Debug.Log("10秒経過");
        }
    }

    public string attackTagN = "N_Pole";
    public string attackTagS = "S_Pole";

    // ============================
    // 左腕にヒットして分離する処理
    // ============================
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

    // ============================
    // 右腕にヒットして分離する処理
    // ============================
    public void HitToArmR()
    {
        // 右腕が分離しておらず、かつスマッシュ攻撃(通常・大)中の場合のみ処理を開始
        if (!isRightArmDetached && (bossState == BossState.SmashNormal || bossState == BossState.SmashBig))
        {
            anim.SetTrigger("Hit_R");

            isHitR = true;
            isFirstFrameHitR = true;
            nowSmashPosR = armBone_R.position;
            nowSmashRotR = armBone_R.rotation;

            // 分離待機フラグをオン
            isWaitForDetachR = true;
            detachTimerR = 0.0f;
        }
    }

    // ====================
    // 左腕分離
    // ====================
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
            sepaArm.transform.localScale = bossMain.localScale;

            sepaArm.bossScript = this;
            sepaArm.isLeft = true;

            // PunchArm側の分離処理
            sepaArm.DetachArm();

            if (leftArmTimerCoroutine != null) StopCoroutine(leftArmTimerCoroutine);
            leftArmTimerCoroutine = StartCoroutine(ArmAutoReviveCoroutine(sepaArm.gameObject, true));
        }
    }

    // ====================
    // 右腕分離
    // ====================
    private void ExecuteDetachArmR()
    {
        isHitR = false;
        isRightArmDetached = true;

        // 本体の左腕ボーンのスケールを0にして「見えなくする」
        armBone_R.localScale = Vector3.zero;

        // 分離用のダミー腕オブジェクトを出現・物理挙動させる
        if (sepaArmR != null)
        {
            sepaArmR.gameObject.SetActive(true);

            // 位置を合わせる
            sepaArmR.transform.position = armBone_R.position;
            sepaArmR.transform.localScale = bossMain.localScale;

            sepaArmR.bossScript = this;
            sepaArmR.isLeft = false;

            // PunchArm側の分離処理
            sepaArmR.DetachArm();

            if (rightArmTimerCoroutine != null) StopCoroutine(rightArmTimerCoroutine);
            rightArmTimerCoroutine = StartCoroutine(ArmAutoReviveCoroutine(sepaArmR.gameObject, false));
        }
    }

    // ====================
    // 左腕復活
    // ====================
    public void ReviveArm()
    {
        if (!isLeftArmDetached) return;

        // 吸収などで復活したらタイマーを停止する
        if (leftArmTimerCoroutine != null)
        {
            StopCoroutine(leftArmTimerCoroutine);
            leftArmTimerCoroutine = null;
        }

        // 途中で点滅がストップして非表示になってた時用の保険
        if (sepaArm != null)
        {
            foreach (var r in sepaArm.GetComponentsInChildren<Renderer>()) r.enabled = true;
        }

        isLeftArmDetached = false;

        // 本体の左腕ボーンのスケールを戻して見えるようにする
        armBone_L.localScale = Vector3.one;

        // 分離用のダミー腕を非表示にする
        if (sepaArm != null)
        {
            sepaArm.gameObject.SetActive(false);

            sepaArm.ResetArm();

        }

        armState = ArmState.Idle;

        StartCoroutine(ScaleUpAnimCoroutine(armBone_L, reviveArmTime));
    }

    // ====================
    // 右腕復活
    // ====================
    public void ReviveArmR()
    {
        if (!isRightArmDetached) return;

        // 吸収などで復活したらタイマーを停止する
        if (rightArmTimerCoroutine != null)
        {
            StopCoroutine(rightArmTimerCoroutine);
            rightArmTimerCoroutine = null;
        }

        // 途中で点滅がストップして非表示になってた時用の保険
        if (sepaArmR != null)
        {
            foreach (var r in sepaArmR.GetComponentsInChildren<Renderer>()) r.enabled = true;
        }

        isRightArmDetached = false;

        // 本体の右腕ボーンのスケールを戻して見えるようにする
        armBone_R.localScale = Vector3.one;

        // 分離用のダミー腕を非表示にする
        if (sepaArmR != null)
        {
            sepaArmR.gameObject.SetActive(false);

            sepaArmR.ResetArm();
        }

        armState = ArmState.Idle;

        StartCoroutine(ScaleUpAnimCoroutine(armBone_R, reviveArmTime));
    }

    // =========================================
    // 分離した腕の自動復活＆点滅コルーチン
    // =========================================
    private IEnumerator ArmAutoReviveCoroutine(GameObject armObj, bool isLeft)
    {
        // 点滅が始まるまでの時間を計算して待つ
        float waitTime = Mathf.Max(0, autoReviveTime - blinkDuration);
        yield return new WaitForSeconds(waitTime);

        // 腕オブジェクトに含まれるすべてのRendererを取得すゆ
        Renderer[] renderers = armObj.GetComponentsInChildren<Renderer>();

        float elapsed = 0f;
        bool isVisible = true;

        // 指定した点滅時間が経過するまでチカチカさせる
        while (elapsed < blinkDuration)
        {
            isVisible = !isVisible; // ON-OFFを反転
            foreach (var r in renderers)
            {
                r.enabled = isVisible;
            }

            yield return new WaitForSeconds(blinkInterval);
            elapsed += blinkInterval;
        }

        // 見えなくなったRendererを戻す
        foreach (var r in renderers)
        {
            r.enabled = true;
        }

        // 時間切れになったら腕を復活
        if (isLeft)
        {
            ReviveArm();
        }
        else
        {
            ReviveArmR();
        }
    }

    // ==========================================
    // プレイヤーが腕を吸収した時にタイマーを止める処理
    // ==========================================
    public void CancelArmTimer(bool isLeft)
    {
        if (isLeft)
        {
            if (leftArmTimerCoroutine != null)
            {
                StopCoroutine(leftArmTimerCoroutine);
                leftArmTimerCoroutine = null;
            }

            // 点滅途中で吸収された場合、透明のままになるのを防ぐ
            if (sepaArm != null)
            {
                foreach (var r in sepaArm.GetComponentsInChildren<Renderer>()) r.enabled = true;
            }
        }
        else
        {
            if (rightArmTimerCoroutine != null)
            {
                StopCoroutine(rightArmTimerCoroutine);
                rightArmTimerCoroutine = null;
            }

            // 点滅途中で吸収された場合、透明のままになるのを防ぐ
            if (sepaArmR != null)
            {
                foreach (var r in sepaArmR.GetComponentsInChildren<Renderer>()) r.enabled = true;
            }
        }
    }

    // ==========================================
    // プレイヤーが腕を発射（または着弾）した後にタイマーを再開する処理
    // ==========================================
    public void RestartArmTimer(bool isLeft)
    {
        if (isLeft)
        {
            // 念のため古いタイマーが残っていたら止める
            if (leftArmTimerCoroutine != null) StopCoroutine(leftArmTimerCoroutine);

            if (sepaArm != null)
            {
                // 左腕のタイマーを0秒から再スタート
                leftArmTimerCoroutine = StartCoroutine(ArmAutoReviveCoroutine(sepaArm.gameObject, true));
            }
        }
        else
        {
            // 念のため古いタイマーが残っていたら止める
            if (rightArmTimerCoroutine != null) StopCoroutine(rightArmTimerCoroutine);

            if (sepaArmR != null)
            {
                // 右腕のタイマーを0秒から再スタート
                rightArmTimerCoroutine = StartCoroutine(ArmAutoReviveCoroutine(sepaArmR.gameObject, false));
            }
        }
    }

    // ====================
    // 敵召喚処理
    // ====================
    public void EventSummonEnemies()
    {
        if (summonPrefabs == null || summonPrefabs.Length == 0) return;

        for (int i = 0; i < summonCount; i++)
        {
            // ボス周辺のランダムな位置（XZ平面）を計算
            Vector2 randomCircle = Random.insideUnitCircle * summonRadius;

            // ボスの現在位置を基準にオフセットを加算
            Vector3 spawnPos = new Vector3(
                transform.position.x + randomCircle.x,
                transform.position.y,
                transform.position.z + randomCircle.y
            );

            // 3種類の敵からランダムに1つ選択
            int randomIndex = Random.Range(0, summonPrefabs.Length);
            GameObject prefab = summonPrefabs[randomIndex];

            // 敵を生成
            GameObject enemy = Instantiate(prefab, spawnPos, Quaternion.identity);

            Instantiate(summonEffect, spawnPos, Quaternion.identity);

            if (targetPlayer != null)
            {
                // 1種類目の敵スクリプトを持っているかチェック
                if (enemy.TryGetComponent(out enemy enemyNormal))
                {
                    enemyNormal.SetTarget(targetPlayer);
                }
                // 持っていなければ2種類目をチェック
                else if (enemy.TryGetComponent(out enemy_bomb enemyBomb))
                {
                    enemyBomb.SetTarget(targetPlayer);
                }
                // 持っていなければ3種類目をチェック
                else if (enemy.TryGetComponent(out enemy_Sky enemySky))
                {
                    enemySky.SetTarget(targetPlayer);
                }
            }
        }
    }

    // ==========================
    // 腕スケールアップ用コルーチン
    // ==========================
    private IEnumerator ScaleUpAnimCoroutine(Transform targetBone, float duration)
    {
        float time = 0f;
        targetBone.localScale = Vector3.zero;

        while (time < duration)
        {
            time += Time.deltaTime;

            // 進行度 (0-1.0)
            float t = time / duration;

            // イーズアウト（徐々にゆっくりになる）
            t = 1.0f - Mathf.Pow(1.0f - t, 3.0f);

            targetBone.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, t);
            yield return null; // 次のフレームまで待機
        }

        // 最後に確実に元のサイズに戻す
        targetBone.localScale = Vector3.one;
    }

    // ==========================
    // ボス被ダメ判定処理
    // ==========================
    public void TakeDamage(float damage, float multiplier)
    {
        if (!isDown) return;
        if (isInvincible) return;

        isInvincible = true;

        int finalDamage = Mathf.RoundToInt(damage * multiplier);

        // HPを減らす
        currentHP -= finalDamage;
        currentHP = Mathf.Clamp(currentHP, 0.0f, maxHP);

        // HPバーのUIを更新させる
        if (hpBarScript != null)
        {
            hpBarScript.SyncHP(currentHP, maxHP);
        }

        StartCoroutine(InvincibleCooltime());

        // HPが0以下になったらダウン状態へ移行
        if (currentHP <= 0 && bossState != BossState.Down)
        {
            isDown = false;
            bossState = BossState.Down;
        }
    }

    // ==========================
    // 無敵時間用コルーチン
    // ==========================
    private IEnumerator InvincibleCooltime()
    {
        yield return new WaitForSeconds(invincibleTime); // 0.2秒待つ
        isInvincible = false;                  // スイッチをOFFに戻す
    }

    // ===================================
    // バリアが攻撃された時に呼ばれる関数
    // ===================================
    public void TakeBarrierDamage(float damage)
    {
        if (isDown) return;

        // バリアのHPを減らす
        currentBarrierHP -= damage;
        Debug.Log($"バリアに {damage} のダメージ！ (残りバリア: {currentBarrierHP}/{barrierMaxHP})");

        // バリアのHPが0以下になったらダウンさせる
        if (currentBarrierHP <= 0.0f)
        {
            bossState = BossState.Down;
            currentBarrierHP = barrierMaxHP; // バリアHP全回復
        }
        else
        {
            // バリアが削れていくときの演出（色変化？エフェクト？）
        }
    }
}