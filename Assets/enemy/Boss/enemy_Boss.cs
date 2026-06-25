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
    public float aimSpeed = 5.0f;

    [SerializeField] private Animator anim;

    void Start()
    {
        anim = GetComponent<Animator>();
        anim.SetBool("Idol", true);

        // 最初は飛ばす用の腕を非表示にしておく
        if (armMesh != null) armMesh.gameObject.SetActive(false);
    }

    [Header("ロボット腕の追尾設定")]
    public Transform armBone;

    public bool isTracking = false; // 追尾中かどうか

    public float transitionSpeed = 8f;

    private Vector3 currentOffset = Vector3.zero;
    private Vector3 lockedOffset = Vector3.zero;

    void LateUpdate()
    {
        if (armBone == null) return;

        // 毎フレームの「アニメーション本来の位置」を取得
        Vector3 rawAnimPos = armBone.position;

        if (bossState == BossState.SmashNormal)
        {
            if (isTracking)
            {
                // 【15〜35フレーム（追従中）】

                // ★ポイント：追従が始まったばかりの時は、前回（振り下ろし時）のロックオフセットから
                // 急激に切り替わらないように、滑らかに（Lerpで）ターゲットとの差分へ移行させます。
                Vector3 targetPos = targetPlayer.position;
                Vector3 targetOffset = new Vector3(targetPos.x - rawAnimPos.x, 0, targetPos.z - rawAnimPos.z);

                // なめらかにプレイヤーの真上のズレへ近づける（最初からワープせずスーッと移動します）
                currentOffset = Vector3.Lerp(currentOffset, targetOffset, transitionSpeed * Time.deltaTime);

                // 追従が終わる瞬間のために「最後にロックオンしたズレ」を記憶
                lockedOffset = currentOffset;
            }
            else
            {
                // 【14フレーム以前 ＆ 36フレーム以降】

                // ★ポイント：追従していない時間帯（振りかぶる前と、振り下ろしている最中）は、
                // 前回の名残（currentOffset）をなめらかにゼロに戻しておくことで、
                // 追従開始時（15フレーム目）に明後あらぬ方向へ飛んでしまうのを防ぎます。
                currentOffset = Vector3.Lerp(currentOffset, Vector3.zero, transitionSpeed * Time.deltaTime);

                // ただし、35フレーム目に追従がオフになった直後は「ロックしたズレ」を維持したいので、
                // 振り下ろし中（lockedOffsetがゼロじゃない状態）はロックを優先させます。
                // （※攻撃が終わってIdleに戻ったら勝手にゼロに戻ります）
                if (lockedOffset != Vector3.zero)
                {
                    currentOffset = lockedOffset;
                }
            }
        }
        else
        {
            // 【スマッシュ攻撃以外の時（Idleなど）】
            // 次の攻撃に備えて、記憶していたロックも完全にリセットし、ズレをゼロに戻す
            lockedOffset = Vector3.zero;
            currentOffset = Vector3.Lerp(currentOffset, Vector3.zero, transitionSpeed * Time.deltaTime);
        }

        // 最終的な位置 ＝ アニメーションの生の位置 ＋ 計算したズレ
        armBone.position = rawAnimPos + currentOffset;
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

        switch (armState)
        {
            case ArmState.Flying:
                targetPosition = targetPlayer.position + Vector3.up * 0.8f;
                Vector3 armDir = targetPosition - armMesh.position;
                Quaternion armTargetRotation = Quaternion.LookRotation(armDir);
                armMesh.rotation = Quaternion.Slerp(armMesh.rotation, armTargetRotation, aimSpeed * Time.deltaTime);

                MoveArmTo(targetPosition, () => { armState = ArmState.Returning; });
                break;

            case ArmState.Returning:
                MoveArmTo(armAnchor.position, () => { ResetArm(); });
                break;
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

                if (attackTimer >= 3.5f)
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

    // 腕を切り離して飛ばす
    void LaunchArm()
    {
        // 飛ばす用の別オブジェクトの腕を表示する
        armMesh.gameObject.SetActive(true);

        if (realArmBone != null)
        {
            // アニメーションによって伸び切った瞬間の実際のボーンの位置と回転を代入
            armMesh.position = punchSocket.position;
            armMesh.rotation = punchSocket.rotation;

            // 位置と回転を渡した後に、本体の腕ボーンのスケールを0にして見た目上消す
            realArmBone.localScale = Vector3.zero;
        }
        else
        {
            // realArmBoneが未設定の場合のフォールバック
            armMesh.position = punchSocket.position;
            armMesh.rotation = punchSocket.rotation;
        }

        armMesh.parent = null; // Animatorの支配から完全に独立させる

        targetPosition = targetPlayer.position + Vector3.up * 0.8f;
        armState = ArmState.Flying;

        Debug.Log("切り離し");
    }

    void MoveArmTo(Vector3 target, System.Action onArrival)
    {
        armMesh.position = Vector3.MoveTowards(armMesh.position, target, armSpeed * Time.deltaTime);
        if (Vector3.Distance(armMesh.position, target) < 0.05f)
        {
            onArrival.Invoke();
        }
    }

    // 戻ってきた腕をボスに戻す
    void ResetArm()
    {
        armMesh.parent = armAnchor;
        armMesh.localPosition = Vector3.zero;
        armMesh.localRotation = Quaternion.identity;

        // 飛ばす用の腕を非表示にする
        armMesh.gameObject.SetActive(false);

        // 本体の腕ボーンのスケールを1に戻して再表示する
        if (realArmBone != null) realArmBone.localScale = Vector3.one;
        anim.SetBool("Idol", true);
        armState = ArmState.Idle;
        Debug.Log("合体");
    }
}