using UnityEngine;

public class EnemyLaser : MonoBehaviour
{
    [Header("ダメージ")]
    [SerializeField] private int damage = 10;

    [Header("ダメージ間隔")]
    [SerializeField] private float damageInterval = 0.2f;

    private float timer;

    private void OnEnable()
    {
        timer = 0f;
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            PlayerHealth player = other.GetComponent<PlayerHealth>();

            if (player != null)
            {
                player.TakeDamage(damage, transform.position);
            }

            timer = damageInterval;
        }
    }
}