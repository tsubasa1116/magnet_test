using System.Collections;
using System.Collections.Generic;
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
    public float maxHP = 50.0f;          // 最大HP(半分を切ると2段階目)
    public float currentHP;              // 現在のHP
    public float takenDamage = 5.0f;     // 受けるダメージ量
    public float revivTime = 12.5f;      // ダウン(バリアが割れてコアがむき出し)から復活するまでの時間
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

    [Tooltip("2段階目で近づいた時に、両手連続パンチ(Rush)を使う確率(0〜1)。残りは大叩きつけ。Rushは2回連続では使わない")]
    [SerializeField, Range(0f, 1f)] private float rushChanceSecond = 0.25f;
    private BossState lastSecondAttack = BossState.Idle; // 2段階目で直前に使った攻撃

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

    [Header("召喚した敵が歩くNavMesh")]
    [Tooltip("起動時にボスの足元にNavMeshが無ければ、ボスを中心としたこの範囲だけ実行時に生成する(ボス部屋にNavMeshを焼いていない時の保険)")]
    [SerializeField] private Vector3 arenaNavMeshSize = new Vector3(120.0f, 30.0f, 120.0f);
    [Tooltip("召喚位置からこの距離以内の一番近いNavMesh上に敵を出す")]
    [SerializeField] private float summonNavSearchRadius = 8.0f;

    private NavMeshDataInstance arenaNavMesh; // 実行時に生成したNavMesh(破棄時に外す)

    [Header("被ダメージ(プレイヤーが飛ばした物)")]
    [Tooltip("ボスの手を当てた時の基本ダメージ")]
    [SerializeField] private float handHitDamage = 10.0f;
    [Tooltip("普通のN/Sオブジェクトは、ボスの手の何倍のダメージか")]
    [SerializeField] private float objectDamageRate = 0.3f;
    [Tooltip("吹っ飛ばした敵は、ボスの手の何倍のダメージか")]
    [SerializeField] private float enemyDamageRate = 0.5f;
    [Tooltip("バリアを張っている時に、バリア以外(体・腕)へ当てた時の倍率(1/4)")]
    [SerializeField] private float barrierUpBodyRate = 0.25f;
    [Tooltip("バリアが割れてコアがむき出しの間に当てた時の倍率(大ダメージ)")]
    [SerializeField] private float coreExposedRate = 3.0f;

    [Header("被ダメージ演出(ダメージが大きいほど強くなる)")]
    [Tooltip("このダメージ以上でヒットストップと震えが最大になる")]
    [SerializeField] private float maxFeedbackDamage = 30.0f;
    [Tooltip("ヒットストップの長さ(秒) 最小〜最大")]
    [SerializeField] private float hitStopMin = 0.04f;
    [SerializeField] private float hitStopMax = 0.25f;
    [Tooltip("ボスの震えの大きさ(m) 最小〜最大")]
    [SerializeField] private float shakeMin = 0.05f;
    [SerializeField] private float shakeMax = 0.4f;
    [Tooltip("ボスが震える時間(実時間の秒)")]
    [SerializeField] private float shakeTime = 0.3f;
    [Tooltip("バリアが割れた時の演出の強さ(ダメージ換算)")]
    [SerializeField] private float barrierBreakFeedback = 15.0f;

    // 被弾時の震え(実時間で減衰。ヒットストップ中も震えて見えるように)
    private Vector3 shakeOffset;
    private float shakeAmplitude;
    private float shakeRemaining;

    private static bool goingToResult; // リザルトへの遷移を二重に始めないように

    public bool IsDown => isDown;

    // バリアを張っているか(割れてコアがむき出しの間・復活の途中は false)
    public bool IsBarrierUp => !isDown && barrier != null && barrier.gameObject.activeInHierarchy;

    [Header("SE")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip summonSE;
    [SerializeField] private AudioClip chargeSE;
    [SerializeField] private AudioClip punchSE;


    // =========================================
    // 初期化処理
    // =========================================
    void Start()
    {
        currentHP = maxHP;
        goingToResult = false;

        anim = GetComponent<Animator>();
        anim.SetBool("Idol", true);
        audioSource = GetComponent<AudioSource>();

        SetupLockOnAndHands();
        EnsureArenaNavMesh();
    }

    void OnDestroy()
    {
        if (arenaNavMesh.valid) arenaNavMesh.Remove();
    }

    void OnDisable()
    {
        // 撃破カットシーン等で止められた時に震えのズレを残さない
        RemoveShake();
    }

    // =========================================
    // 注目(ロックオン)部位と、手の磁力(N/S)の設定
    // =========================================
    private void SetupLockOnAndHands()
    {
        if (barrier != null)
        {
            // お腹のバリア(割れている間は非表示になるので、その間は自動で注目対象から外れる)
            LockOnPart barrierPart = LockOnPart.Attach(barrier.gameObject, LockOnPart.Kind.Enemy, transform);

            // バリアが割れると出てくるコア。バリアの球の中心(=コアの位置)に注目点を置く。
            // 親はバリアと同じボーンなので、ダウン中のアニメにも追従する
            GameObject corePoint = new GameObject("CoreLockOnPoint");
            corePoint.transform.SetParent(barrier.parent, false);
            corePoint.transform.localPosition = barrier.localPosition;
            LockOnPart corePart = LockOnPart.Attach(corePoint, LockOnPart.Kind.Enemy, transform,
                () => currentHP > 0f && !barrier.gameObject.activeInHierarchy);

            // バリアに注目中に割れたらコアへ、コアに注目中にバリアが戻ったらバリアへ、注目を引き継ぐ
            barrierPart.Successor = corePart;
            corePart.Successor = barrierPart;
        }

        // 左右の手: 極は分離用の腕のタグ(N_Pole/S_Pole)に合わせる(判別できなければ左N・右S)
        SetupHand(armBone_L, true, HandPole(sepaArm, MagnetState.N), () => !isLeftArmDetached);
        SetupHand(armBone_R, false, HandPole(sepaArmR, MagnetState.S), () => !isRightArmDetached);
    }

    private void SetupHand(Transform bone, bool isLeft, MagnetState pole, System.Func<bool> isAttached)
    {
        if (bone == null) return;

        // 分離して見えなくなっている間は注目させない
        LockOnPart.Kind kind = pole == MagnetState.N ? LockOnPart.Kind.NPole : LockOnPart.Kind.SPole;
        LockOnPart.Attach(bone.gameObject, kind, transform, isAttached);

        BossHandMagnet magnet = bone.GetComponent<BossHandMagnet>();
        if (magnet == null) magnet = bone.gameObject.AddComponent<BossHandMagnet>();
        magnet.Setup(this, isLeft, pole);
    }

    private static MagnetState HandPole(PunchArm arm, MagnetState fallback)
    {
        if (arm == null) return fallback;
        if (arm.CompareTag("N_Pole")) return MagnetState.N;
        if (arm.CompareTag("S_Pole")) return MagnetState.S;
        return fallback;
    }

    // =========================================
    // 召喚した敵用のNavMeshを用意する
    // =========================================
    // ボス部屋にNavMeshが焼かれていないと、召喚した敵のNavMeshAgentが生成に失敗し
    // (Failed to create agent because it is not close enough to the NavMesh)一歩も動けない。
    // 焼いてあればそれを使い、無ければボス周辺の地形コライダーから実行時に生成する。
    private void EnsureArenaNavMesh()
    {
        if (NavMesh.SamplePosition(transform.position, out _, summonNavSearchRadius, NavMesh.AllAreas)) return;

        Bounds bounds = new Bounds(transform.position, arenaNavMeshSize);
        var sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0,
            new List<NavMeshBuildMarkup>(), sources);

        // 床・壁などの動かない地形だけを使う(ボス本体・プレイヤー・敵・投げる物などRigidbody付きは除外)
        sources.RemoveAll(s => s.component is Collider c
            && (c.isTrigger || c.attachedRigidbody != null || c.transform.IsChildOf(transform)));

        NavMeshData data = NavMeshBuilder.BuildNavMeshData(
            NavMesh.GetSettingsByID(0), sources, bounds, Vector3.zero, Quaternion.identity);
        arenaNavMesh = NavMesh.AddNavMeshData(data);

        Debug.LogWarning($"[enemy_Boss] ボス周辺にNavMeshが無いため実行時に生成しました(地形 {sources.Count} 個)。" +
            "ボス部屋を含めてNavMeshをBakeすると、この処理は不要になります");
    }

    void LateUpdate()
    {
        UpdateArms();
        ApplyShake();
    }

    // =========================================
    // ステート用更新処理(腕)
    // =========================================
    private void UpdateArms()
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
        // 前フレームの震えのズレを戻してから、通常の移動・回転を行う
        RemoveShake();

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

            // デバッグ: リザルトへ遷移(本番はボス撃破カットシーンの後に同じ処理が走る)
            if (Input.GetKeyDown(KeyCode.H))
            {
                GoToResult();
            }

            if (Input.GetKeyDown(KeyCode.J))
            {
                gameOverTransition.GoToGameOver();
            }

            if (Input.GetKeyDown(KeyCode.G))
            {
                mission.SetMission(4,3);
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                mission.ClearMission(4);
            }

            if (Input.GetKeyDown(KeyCode.V))
            {
                Nyuxtu3.Instance.ShowUI();
            }

            if (Input.GetKeyDown(KeyCode.B))
            {
                Nyuxtu3.Instance.HideUI();
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
                        // 両手連続パンチ(Rush)は吸着できず反撃の隙が無いので、使う割合を抑え、2回連続では出さない。
                        // それ以外は右手を吸着できる大叩きつけ(SmashBig)
                        bool useRush = lastSecondAttack != BossState.Rush && Random.value < rushChanceSecond;
                        bossState = useRush ? BossState.Rush : BossState.SmashBig;
                        lastSecondAttack = bossState;
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
                audioSource.PlayOneShot(punchSE);
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
                    audioSource.PlayOneShot(punchSE);
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
            if (!isSecond && currentHP <= maxHP * 0.5f)
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

    // ==========================================
    // 攻撃で飛んできた手をプレイヤーの磁力で掴めるか
    // (左手: ロケットパンチで飛んでいる間 / 右手: 叩きつけ中)
    // ==========================================
    public bool CanGrabHand(bool isLeft)
    {
        if (isLeft)
            return !isLeftArmDetached && bossState == BossState.Punch && armState != ArmState.Idle;

        return !isRightArmDetached && (bossState == BossState.SmashNormal || bossState == BossState.SmashBig);
    }

    // ==========================================
    // プレイヤーの磁力で手を引き寄せられた:
    // その場で本体から切り離し、代わりに掴ませる分離用の腕(NSオブジェクト)を返す
    // ==========================================
    public Rigidbody DetachHandForMagnet(bool isLeft)
    {
        if (!CanGrabHand(isLeft)) return null;

        PunchArm arm = isLeft ? sepaArm : sepaArmR;
        if (arm == null) return null;

        // 物を当てた後の分離待ちが残っていても、後から二重に分離させない
        if (isLeft)
        {
            isWaitForDetach = false;
            ExecuteDetachArm();
        }
        else
        {
            isWaitForDetachR = false;
            ExecuteDetachArmR();
        }

        // 手を奪われたので攻撃を打ち切り、怯んでから待機へ戻る
        anim.SetTrigger(isLeft ? "Hit_L" : "Hit_R");
        bossState = BossState.Idle;
        armState = ArmState.Idle;
        startAttack = false;
        isTracking = false;
        attackTimer = 0.0f;

        return arm.GetComponent<Rigidbody>();
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

            // NavMeshの上に出さないとNavMeshAgentが生成に失敗して動けないので、一番近いNavMesh上へ寄せる
            if (NavMesh.SamplePosition(spawnPos, out NavMeshHit navHit, summonNavSearchRadius, NavMesh.AllAreas))
            {
                spawnPos = navHit.position;
            }
            else
            {
                Debug.LogWarning($"[enemy_Boss] 召喚位置 {spawnPos} の近くにNavMeshが無いため、召喚した敵は動けません");
            }

            // 3種類の敵からランダムに1つ選択
            int randomIndex = Random.Range(0, summonPrefabs.Length);
            GameObject prefab = summonPrefabs[randomIndex];

            // 敵を生成
            GameObject enemy = Instantiate(prefab, spawnPos, Quaternion.identity);
            audioSource.PlayOneShot(summonSE);

            Instantiate(summonEffect, spawnPos, Quaternion.identity);

            if (targetPlayer != null)
            {
                // 召喚した敵は索敵範囲に関係なく、最初からプレイヤーを狙わせる
                // 1種類目の敵スクリプトを持っているかチェック
                if (enemy.TryGetComponent(out enemy enemyNormal))
                {
                    enemyNormal.OnSummoned(targetPlayer);
                }
                // 持っていなければ2種類目をチェック
                else if (enemy.TryGetComponent(out enemy_bomb enemyBomb))
                {
                    enemyBomb.OnSummoned(targetPlayer);
                }
                // 持っていなければ3種類目をチェック
                else if (enemy.TryGetComponent(out enemy_Sky enemySky))
                {
                    enemySky.OnSummoned(targetPlayer);
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
    // プレイヤーが飛ばした物がボスに当たった時(ThrowableObjectから呼ばれる)
    //   ・バリアを張っている時にバリアへ当てた → バリアが割れる(BreakBarrier)
    //   ・バリアを張っている時にバリア以外(体・腕)へ当てた → HPへ直接ダメージ。ただし半減
    //   ・バリアが割れてコアがむき出しの時に当てた → HPへ大ダメージ
    // 1回の発射で1回だけ(当たったら発射状態を解除する)
    // ===================================
    public void OnThrownObjectHit(GameObject thrown, Collider hitCollider)
    {
        if (barrier != null && hitCollider != null && hitCollider.transform.IsChildOf(barrier))
        {
            OnBarrierHit(thrown);
            return;
        }

        if (!isStartAction || currentHP <= 0f) return;
        ThrowableObject throwable = thrown.GetComponent<ThrowableObject>();
        if (throwable == null || !throwable.IsThrown) return;
        throwable.ResetThrown();

        float rate = IsBarrierUp ? barrierUpBodyRate : coreExposedRate;
        ApplyHitDamage(ProjectileDamage(thrown) * rate);
    }

    // バリアに当たった(バリア自身の衝突・バリア付近を通過した判定から呼ばれる)
    public void OnBarrierHit(GameObject thrown)
    {
        if (!isStartAction || currentHP <= 0f || !IsBarrierUp) return;
        ThrowableObject throwable = thrown.GetComponent<ThrowableObject>();
        if (throwable == null || !throwable.IsThrown) return;
        throwable.ResetThrown();

        BreakBarrier();
    }

    // バリアは1回当てると割れ、ダウンの間(revivTime)コアがむき出しになる。
    // ダウン(Down)の処理がバリアを消し、復活後のアニメイベントでバリアが戻る
    private void BreakBarrier()
    {
        Debug.Log("[enemy_Boss] バリアが割れた！コアがむき出し");
        bossState = BossState.Down;
        PlayHitFeedback(barrierBreakFeedback);
    }

    // 飛ばした物の種類ごとの基本ダメージ
    private float ProjectileDamage(GameObject thrown)
    {
        if (thrown.GetComponent<PunchArm>() != null) return handHitDamage;                      // ボスの手
        if (thrown.GetComponent<IMagnetEnemy>() != null) return handHitDamage * enemyDamageRate; // 吹っ飛ばした敵
        return handHitDamage * objectDamageRate;                                                // 普通のN/Sオブジェクト
    }

    // HPを減らす。ダメージが大きいほどヒットストップと震えも大きい。
    // HPが0になると BossEndCutscene が撃破カットシーンを再生し、その後リザルトへ進む
    private void ApplyHitDamage(float damage)
    {
        if (damage <= 0f || currentHP <= 0f) return;

        currentHP = Mathf.Clamp(currentHP - damage, 0.0f, maxHP);
        if (hpBarScript != null) hpBarScript.SyncHP(currentHP, maxHP);
        Debug.Log($"[enemy_Boss] {damage:0.#} ダメージ！ (残りHP {currentHP:0.#}/{maxHP}) {(IsBarrierUp ? "バリア越し(1/4)" : "コアむき出し")}");

        PlayHitFeedback(damage);

        if (currentHP <= 0f && bossState != BossState.Down)
        {
            isDown = false;
            bossState = BossState.Down;
        }
    }

    // ヒットストップとボスの震え(ダメージ量に応じて強くする)
    private void PlayHitFeedback(float damage)
    {
        float t = Mathf.Clamp01(damage / Mathf.Max(maxFeedbackDamage, 0.01f));
        HitStop.Play(Mathf.Lerp(hitStopMin, hitStopMax, t));

        shakeAmplitude = Mathf.Lerp(shakeMin, shakeMax, t);
        shakeRemaining = shakeTime;
    }

    // 震え: LateUpdateで位置をずらし、次のUpdateの最初に戻す(移動・回転の処理には影響させない)。
    // ヒットストップ中も震えて見えるよう実時間で減衰させる
    private void ApplyShake()
    {
        if (shakeRemaining <= 0f) return;

        shakeRemaining -= Time.unscaledDeltaTime;
        float fade = Mathf.Clamp01(shakeRemaining / Mathf.Max(shakeTime, 0.01f));
        shakeOffset = Random.insideUnitSphere * shakeAmplitude * fade;
        transform.position += shakeOffset;
    }

    private void RemoveShake()
    {
        if (shakeOffset == Vector3.zero) return;
        transform.position -= shakeOffset;
        shakeOffset = Vector3.zero;
    }

    // ===================================
    // リザルトへ遷移(ボス撃破カットシーンの後と、デバッグのHキーから呼ぶ)
    // ===================================
    public static void GoToResult()
    {
        if (goingToResult) return;
        goingToResult = true;

        // BGMを止めてからリザルトへ(AudioManagerはタイトルから来た時だけいるので、直接再生時はnull)
        if (AudioManager.Instance != null) AudioManager.Instance.StopBGM();

        SceneLoad.LoadDirect("ResultScene", FadeType.White);
        GameManager.Instance.EndGame();

        GameResultManager.SetResultData(
            GameManager.Instance.TotalKillCount,
            Mathf.FloorToInt(GameManager.Instance.ElapsedTime)
        );
    }
    public void EventChargeSound()
    {
        audioSource.PlayOneShot(chargeSE);
    }
    }