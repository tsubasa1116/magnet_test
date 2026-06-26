using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

public class enemy_Boss : MonoBehaviour
{
    [Header("参照設定")]
    public Transform bossMesh;     // ボス全体のメッシュ
    public Transform armMesh;      // ★【重要】飛ばす専用の別オブジェクト（腕のメッシュなど）
    public Transform armAnchor;    // 腕の定位置（Arm_Anchor）
    public Transform targetPlayer;
    public Transform realArmBone;  // ★【追加】本体の消したい腕のボーン（R_Hand_JNTin など）
    public Transform punchSocket;

    [Header("調整パラメータ")]
    public float armSpeed = 15.0f;
    public float retrunSpeed = 30.0f;
    public float moveSpeed = 10.0f;
    public float targetDistance = 10.0f;
    public float stopDistance = 3.0f;

    private enum ArmState
    {
        Idle,
        Flying,
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
        Down
    }

    private BossState bossState = BossState.Idle;
    private ArmState armState = ArmState.Idle;
    private Vector3 targetPosition;
    private float attackTimer = 0.0f;

    public Transform rBos;
    public Transform target;
    public float aimSpeed = 10.0f;

    [SerializeField] private Animator anim;

    void Start()
    {
        anim = GetComponent<Animator>();
        anim.SetBool("Idol", true);

        // 最初は飛ばす用の腕を非表示にしておく
        if (armMesh != null) armMesh.gameObject.SetActive(false);
    }

    [Header("ロボット腕の追尾設定")]
    public Transform armBone_R;
    public Transform armBone_L;

    public bool isTracking = false; // 追尾中かどうか

    public float transitionSpeed = 8f;

    private Vector3 currentOffset = Vector3.zero;
    private Vector3 lockedOffset = Vector3.zero;

    private Vector3 punchPos;
    private Quaternion punchRot;

    [Header("ロケットパンチ回転補正")]
    public Vector3 punchRotationOffset = new Vector3(0, 0, 0);


    void LateUpdate()
    {
        if (armBone_R == null || armBone_L == null) return;

        // 毎フレームのアニメーション自体の位置と回転を取得（左右）
        Vector3 rawAnimPos_R = armBone_R.position;
        Quaternion rawAnimRot_R = armBone_R.rotation;

        Vector3 rawAnimPos_L = armBone_L.position;
        Quaternion rawAnimRot_L = armBone_L.rotation;

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
        }
        else if (bossState == BossState.Punch)
        {
            // ロケットパンチ(左腕)
            if (armState == ArmState.Flying)
            {
                Vector3 targetPos = targetPlayer.position + Vector3.up * 0.8f;
                punchPos = Vector3.MoveTowards(punchPos, targetPos, armSpeed * Time.deltaTime);

                Vector3 dir = targetPos - punchPos;
                if (dir != Vector3.zero)
                {
                    // 1. ターゲットの方向を向くベースの回転（Z軸が前を向く）
                    Quaternion baseRotation = Quaternion.LookRotation(dir);

                    // 2. インスペクターで設定したボーンのズレを直すための補正回転
                    Quaternion offsetRotation = Quaternion.Euler(punchRotationOffset);

                    // 3. 2つを掛け合わせる（※Quaternionの掛け算は順序が重要です）
                    Quaternion targetRotation = baseRotation * offsetRotation;

                    // 4. スムーズに回転させる
                    punchRot = Quaternion.Slerp(punchRot, targetRotation, aimSpeed * Time.deltaTime);
                }

                if (Vector3.Distance(punchPos, targetPos) < 0.05f)
                {
                    armState = ArmState.Returning;
                }

                armBone_L.position = punchPos;
                armBone_L.rotation = punchRot;
            }
            else if (armState == ArmState.Returning)
            {
                punchPos = Vector3.MoveTowards(punchPos, rawAnimPos_L, retrunSpeed * Time.deltaTime);
                punchRot = Quaternion.Slerp(punchRot, rawAnimRot_L, aimSpeed * Time.deltaTime);

                if (Vector3.Distance(punchPos, rawAnimPos_L) < 0.05f)
                {
                    armState = ArmState.Idle;
                    bossState = BossState.Idle;
                    anim.SetBool("Idol", true);
                }

                armBone_L.position = punchPos;
                armBone_L.rotation = punchRot;
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

            // 左腕は左腕本来の位置をそのまま適用
            armBone_L.position = rawAnimPos_L;
            armBone_L.rotation = rawAnimRot_L;
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
                if (armState == ArmState.Idle) anim.SetTrigger("Punch");
                else if (armState == ArmState.Flying) armState = ArmState.Returning;
            }

            if (Input.GetKeyDown(KeyCode.K))
            {
                bossState = BossState.Move;
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
                }
                break;
           case BossState.SmashNormal:
                if (attackTimer == 0.0f)
                {
                    anim.SetBool("Move", false);
                    anim.SetBool("Idol", false);
                    anim.SetTrigger("Smash_N");
                }

                if (attackTimer >= 0.3f) isTracking = true;  // 追尾ON
                if (attackTimer >= 1.1f) isTracking = false; // 追尾OFF

                attackTimer += Time.deltaTime;

                if (attackTimer >= 3.3f)
                {
                    bossState = BossState.Idle;
                    attackTimer = 0.0f;
                }
                break;
            case BossState.Down:
                anim.SetBool("Move", false);
                anim.SetBool("Idol", false);
               // amim.SetBool("Down", true);
                break;
        }
    }

    // アニメーションイベントからこのメソッドを呼んでください
    public void FirePunch()
    {
        bossState = BossState.Punch;
        armState = ArmState.Flying;

        // 飛んでいく直前の、腕の初期位置と回転を記憶
        punchPos = armBone_L.position;
        punchRot = armBone_L.rotation;

        Debug.Log("パンチ発射");
    }
}