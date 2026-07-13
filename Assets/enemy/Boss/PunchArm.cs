using UnityEngine;

public class PunchArm : MonoBehaviour
{
    // 分離後コライダー
    [SerializeField] private BoxCollider singleBoxCollider;
    [SerializeField] private GameObject boneRoot;

    // 腕のモデル
    [SerializeField] private Transform ArmMesh;
    [SerializeField] private int attackDamage = 1;

    private bool isDetached = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (isDetached) return;

        var playerController = collision.gameObject.GetComponent<PlayerHealth>();
        if (playerController != null)
        {
            playerController.TakeDamage(attackDamage);
        }
    }

    public void ResetArm()
    {
        isDetached = false;
    }

    // 腕が分離したときに呼ぶ関数
    public void DetachArm()
    {
        isDetached = true;
    }
}