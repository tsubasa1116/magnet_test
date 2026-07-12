using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    [SerializeField] private enemy_Boss boss;

    private bool activated = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerRespawn respawn = other.GetComponent<PlayerRespawn>();

            if (respawn != null)
            {
                respawn.SetCheckpoint(transform);
            }
            // ボス行動開始
            if (boss != null)
            {
                boss.isStartAction = true;
            }
        }
    }
}