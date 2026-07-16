using UnityEngine;

public class beam : MonoBehaviour
{
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float lifeTime = 2f;   // 生存時間

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Enemy")) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerHealth playerHealth = collision.gameObject.GetComponent<PlayerHealth>();

            if (playerHealth != null)
                playerHealth.TakeDamage(attackDamage, transform.position);
        }
        // 何かに当たったら消す
        Destroy(gameObject);
    }

    private void OnDrawGizmos()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null) return;

        Gizmos.color = Color.red;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(col.center, col.size);
    }
}