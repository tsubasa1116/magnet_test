using UnityEngine;

public class enemy_Boss : MonoBehaviour
{
    [Header("参照設定")]
    public Transform armMesh;      // 飛ばす腕（Arm_Mesh）
    public Transform armAnchor;    // 腕の定位置（Arm_Anchor）

    [Header("調整パラメータ")]
    public float flySpeed = 15.0f;   // 飛ばすスピード
    public float targetDistance = 10.0f; // どこまで飛ばすか

    // 腕の状態を管理するフラグ
    private enum ArmState { Idle, Flying, Returning }
    private ArmState currentState = ArmState.Idle;

    private Vector3 targetPosition; // 腕の目的地

    public Transform rBos;        // 右腕のボーン（R_Bos）を割り当てる
    public Transform target;      // プレイヤーのTransformを割り当てる
    public float aimSpeed = 5.0f; // 振り向くスピード

    void Update()
    {
        // スペースキーでアクション開始
        if (Input.GetKeyDown(KeyCode.L))
        {
            if (currentState == ArmState.Idle)
            {
                LaunchArm();
            }
            else if (currentState == ArmState.Flying)
            {
                // 飛んでいる最中に押したら戻るモードにする
                currentState = ArmState.Returning;
            }
        }

        if (currentState == ArmState.Idle && target != null && armMesh != null)
        {
            // 腕からターゲット（プレイヤー）への方向を計算
            Vector3 direction = target.position - armMesh.position;

            // その方向を向くための回転を作る
            Quaternion targetRotation = Quaternion.LookRotation(direction);

            // 骨ではなく、腕のメッシュ（pCube2）自体を滑らかに回転させる！
            armMesh.rotation = Quaternion.Slerp(armMesh.rotation, targetRotation, aimSpeed * Time.deltaTime);
        }

        // 状態に応じた腕の移動処理
        switch (currentState)
        {
            case ArmState.Flying:
                MoveArmTo(targetPosition, () =>
                {
                    // 目的地に着いたら自動で戻り状態にする（今回はわかりやすさ優先）
                    currentState = ArmState.Returning;
                });
                break;

            case ArmState.Returning:
                // アンカー（ボスの定位置）に向かって戻る
                MoveArmTo(armAnchor.position, () =>
                {
                    // 戻ってきたら親子関係を復活させる
                    ResetArm();
                });
                break;
        }
    }

    // 腕を切り離して飛ばす
    void LaunchArm()
    {
        // 親子関係を解除（Animatorの支配から抜ける）
        armMesh.parent = null;

        // ボスの前方（Transform.forward）に向けて目的地を設定
        targetPosition = armMesh.position + armMesh.forward * targetDistance;
        currentState = ArmState.Flying;
        Debug.Log("切り離し");
    }

    // 腕を目的地に向けて移動させる共通処理
    void MoveArmTo(Vector3 target, System.Action onArrival)
    {
        armMesh.position = Vector3.MoveTowards(armMesh.position, target, flySpeed * Time.deltaTime);

        // 目的地にほぼ到着したか判定
        if (Vector3.Distance(armMesh.position, target) < 0.05f)
        {
            onArrival.Invoke();
        }
    }

    // 戻ってきた腕をボスに戻す
    void ResetArm()
    {
        // 再びアンカーの子オブジェクトにする
        armMesh.parent = armAnchor;

        // ズレを無くすために位置と回転を完全に同期させる
        armMesh.localPosition = Vector3.zero;
        armMesh.localRotation = Quaternion.identity;

        currentState = ArmState.Idle;
        Debug.Log("合体");
    }
}
