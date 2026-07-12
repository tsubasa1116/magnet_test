using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerRespawn : MonoBehaviour
{
    [SerializeField] private float respawnDelay = 3f;

    private Vector3 checkpointPosition;
    private Quaternion checkpointRotation;

    private Rigidbody rb;
    private PlayerHealth health;
    private PlayerRagdoll ragdoll;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        health = GetComponent<PlayerHealth>();
        ragdoll = GetComponent<PlayerRagdoll>();

        checkpointPosition = transform.position;
        checkpointRotation = transform.rotation;
    }

    private void OnEnable()
    {
        health.OnDied += StartRespawn;
    }

    private void OnDisable()
    {
        health.OnDied -= StartRespawn;
    }

    private void StartRespawn()
    {
        StartCoroutine(RespawnCoroutine());
    }

    private IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        Respawn();
    }

    public void SetCheckpoint(Transform point)
    {
        checkpointPosition = point.position;
        checkpointRotation = point.rotation;
    }

    public void Respawn()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        transform.SetPositionAndRotation(checkpointPosition, checkpointRotation);

        ragdoll.DisableRagdoll();   // ラグドール解除
        health.Revive();            // HP回復・死亡状態解除
    }
}