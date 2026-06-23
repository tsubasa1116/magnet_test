using UnityEngine;
using UnityEngine.AI;

public class enemy_Boss : MonoBehaviour
{
    [Header("参照設定")]
    public Transform bossMesh;     // ボス全体のメッシュ
    public Transform armMesh;      // ★【重要】飛ばす専用の別オブジェクト（腕のメッシュなど）
    public Transform armAnchor;    // 腕の定位置（Arm_Anchor）
    public Transform targetPlayer;
    public Transform realArmBone;  // ★【追加】本体の消したい腕のボーン（R_Hand_JNTin など）

    [Header("調整パラメータ")]
    public float armSpeed = 15.0f;
    public float moveSpeed = 10.0f;
    public float targetDistance = 10.0f;

    private enum ArmState
    {
        Idle,
        Flying,
        Returning
    }

    private ArmState currentState = ArmState.Idle;
    private Vector3 targetPosition;

    public Transform rBos;
    public Transform target;
    public float aimSpeed = 5.0f;

    [SerializeField] private Animator anim;

    void Start()
    {
        anim = GetComponent<Animator>();
        anim.SetBool("idle", true);

        // ★最初は飛ばす用の腕を非表示にしておく
        if (armMesh != null) armMesh.gameObject.SetActive(false);
    }

    void Update()
    {
        if (target != null && bossMesh != null)
        {
            Vector3 direction = target.position - bossMesh.position;
            direction.y = 0;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            bossMesh.rotation = Quaternion.Slerp(bossMesh.rotation, targetRotation, aimSpeed * Time.deltaTime);

            if (Input.GetKeyDown(KeyCode.L))
            {
                if (currentState == ArmState.Idle) LaunchArm();
                else if (currentState == ArmState.Flying) currentState = ArmState.Returning;
            }

            if (Input.GetKeyDown(KeyCode.K))
            {
                float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);
                Vector3 targetCenterPos = targetPlayer.position + Vector3.up * 0.8f;
                transform.position = Vector3.MoveTowards(transform.position, targetCenterPos, moveSpeed * Time.deltaTime);
            }
        }

        switch (currentState)
        {
            case ArmState.Flying:
                targetPosition = targetPlayer.position + Vector3.up * 0.8f;
                Vector3 armDir = targetPosition - armMesh.position;
                Quaternion armTargetRotation = Quaternion.LookRotation(armDir);
                armMesh.rotation = Quaternion.Slerp(armMesh.rotation, armTargetRotation, aimSpeed * Time.deltaTime);

                MoveArmTo(targetPosition, () => { currentState = ArmState.Returning; });
                break;

            case ArmState.Returning:
                MoveArmTo(armAnchor.position, () => { ResetArm(); });
                break;
        }
    }

    // 腕を切り離して飛ばす
    void LaunchArm()
    {
        // ★1. 本体の腕ボーンのスケールを0にして、見た目上消す
        if (realArmBone != null) realArmBone.localScale = Vector3.zero;

        // ★2. 飛ばす用の別オブジェクトの腕を表示し、位置をアンカーに合わせる
        armMesh.gameObject.SetActive(true);
        armMesh.position = armAnchor.position;
        armMesh.rotation = armAnchor.rotation;

        armMesh.parent = null; // Animatorの支配から完全に独立させる

        targetPosition = targetPlayer.position + Vector3.up * 0.8f;
        currentState = ArmState.Flying;

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

        // ★3. 飛ばす用の腕を非表示にする
        armMesh.gameObject.SetActive(false);

        // ★4. 本体の腕ボーンのスケールを1に戻して再表示する
        if (realArmBone != null) realArmBone.localScale = Vector3.one;

        currentState = ArmState.Idle;
        Debug.Log("合体");
    }
}