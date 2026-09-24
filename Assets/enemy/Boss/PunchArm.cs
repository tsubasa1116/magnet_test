using UnityEngine;
using System.Collections;

public class PunchArm : MonoBehaviour
{
    // 分離後コライダー
    [SerializeField] private BoxCollider singleBoxCollider;
    [SerializeField] private GameObject boneRoot;

    // 腕のモデル
    [SerializeField] private Transform ArmMesh;
    [SerializeField] private int attackDamage = 1;

    [SerializeField] public enemy_Boss bossScript;
    [HideInInspector] public bool isLeft;

    private bool isDetached = false;
    private bool isHitL = false;
    private bool isHitR = false;

    private string attackTagN = "N_Pole";
    private string attackTagS = "S_Pole";

    // プレイヤーが1回発射した分離腕は、ボスに戻る(ResetArm)まで吸着できないようにする
    private bool thrownByPlayer = false;
    private string originalTag;
    private ThrowableObject throwable;

    public bool IsSpent => thrownByPlayer;

    private void Awake()
    {
        originalTag = tag;
    }

    private void FixedUpdate()
    {
        if (!thrownByPlayer || !CompareTag(originalTag)) return;

        // 発射直後にタグを外すとバリアのタグ判定(N_Pole/S_Pole)に当たらなくなるため、
        // 飛び終わってから(発射状態が解除されてから)外して、吸着・注目の対象から外す
        if (throwable == null) throwable = GetComponent<ThrowableObject>();
        if (throwable == null || !throwable.IsThrown) tag = "Untagged";
    }

    // プレイヤーが吸着して発射した時に呼ぶ
    public void MarkThrownByPlayer()
    {
        thrownByPlayer = true;
    }

    // もう一度吸着できる状態に戻す(ボスに戻った時・新しく分離した時)
    private void RestoreMagnetTag()
    {
        thrownByPlayer = false;
        if (!string.IsNullOrEmpty(originalTag)) tag = originalTag;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isDetached) return;

        var playerController = collision.gameObject.GetComponent<PlayerHealth>();
        if (playerController != null)
        {
            playerController.TakeDamage(attackDamage);
        }

        if (collision.gameObject.GetComponent<ThrowableObject>() != null && this.CompareTag("N_Hand"))
        {
            if (!isHitR) bossScript.HitToArm();
            isHitR = true;
        }
        else if (collision.gameObject.GetComponent<ThrowableObject>() != null && this.CompareTag("S_Hand"))
        {
            if (!isHitL) bossScript.HitToArmR();
            isHitL = true;
        }
    }

    public void ResetArm()
    {
        isDetached = false;
        isHitL = false;
        isHitR = false;
        RestoreMagnetTag();
    }

    // 腕が分離したときに呼ぶ関数
    public void DetachArm()
    {
        isDetached = true;
        RestoreMagnetTag();
    }
}   