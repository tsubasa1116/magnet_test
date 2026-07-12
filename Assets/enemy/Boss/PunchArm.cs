using UnityEngine;

public class PunchArm : MonoBehaviour
{
    // 分離後コライダー
    [SerializeField] private BoxCollider singleBoxCollider;
    [SerializeField] private GameObject boneRoot;

    // 腕のモデル
    [SerializeField] private Transform ArmMesh;
    [SerializeField] private int attackDamage = 1;

    private void OnCollisionEnter(Collision collision)
    {
        var playerController = collision.gameObject.GetComponent<PlayerHealth>();
        if (playerController != null)
        {
            playerController.TakeDamage(attackDamage);
        }
    }

    // 腕が分離したときに呼ぶ関数gaa
    public void DetachArm()
    {
        
    }
}