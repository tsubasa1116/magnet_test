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

    [SerializeField] private enemy_Boss bossScript;

    private bool isDetached = false;
    private bool isHitL = false;
    private bool isHitR = false;

    private string attackTagN = "N_Pole";
    private string attackTagS = "S_Pole";

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
    }

    // 腕が分離したときに呼ぶ関数
    public void DetachArm()
    {
        isDetached = true;
    }
}