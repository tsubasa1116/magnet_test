using UnityEngine;

public class MagnetObject : MonoBehaviour
{
    [Header("反発設定")]
    [SerializeField] private float repelForce = 25f;
    [SerializeField] private float liftForce = 2f;

    [Header("効果音")]
    [SerializeField] private AudioClip repelSound;


    private PlayerStateMachine playerState;
    private Rigidbody playerRb;


    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerState = other.GetComponent<PlayerStateMachine>();
        playerRb = other.GetComponent<Rigidbody>();
    }


    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerState = null;
        playerRb = null;
    }


    private void Update()
    {
        if (playerState == null || playerRb == null)
            return;


        // 磁力OFFなら無視
        PlayerCatch catchSystem = playerState.GetComponent<PlayerCatch>();

        if (catchSystem == null || !catchSystem.IsCatching)
            return;


        CheckMagnet();
    }


    private void CheckMagnet()
    {
        bool objectIsN = CompareTag("Repel_N");
        bool objectIsS = CompareTag("Repel_S");

        bool samePole =
            (playerState.CurrentState == MagnetState.N && objectIsN) ||
            (playerState.CurrentState == MagnetState.S && objectIsS);

        if (samePole) Repel();
        else          Attract();
    }

    private void Repel()
    {
        Debug.Log("反発");

        if (repelSound != null)
            AudioSource.PlayClipAtPoint(repelSound, transform.position);

        playerRb.linearVelocity = Vector3.zero;

        // オブジェクトから反対方向
        Vector3 direction = -transform.forward;

        Vector3 force =
            direction * repelForce +
            Vector3.up * liftForce;

        PlayerMovement movement = playerRb.GetComponent<PlayerMovement>();

        if (movement != null)
        {
            movement.SetExternalVelocity(force);
        }

        // 連続発動防止
        playerState = null;
    }

    private void Attract()
    {
        Debug.Log("吸着");

        Vector3 direction =
            (transform.position - playerRb.position).normalized;

        playerRb.AddForce(direction * 10f, ForceMode.Acceleration);
    }
}