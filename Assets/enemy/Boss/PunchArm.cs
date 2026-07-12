using UnityEngine;

public class PunchArm : MonoBehaviour
{
    // 分離後コライダー
    [SerializeField] private BoxCollider singleBoxCollider;
    [SerializeField] private GameObject boneRoot;

    // 腕のモデル
    [SerializeField] private Transform ArmMesh;
    [SerializeField] private int attackDamage = 1;

    private void OnTriggerEnter(Collider other)
    {
        var playerController = other.GetComponent<Controller>();
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