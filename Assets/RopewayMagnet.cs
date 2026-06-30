using UnityEngine;

public class RopewayMagnet : MonoBehaviour
{
    [SerializeField] private float attachDistance = 0.8f;

    private Transform player;
    private Rigidbody playerRb;
    private Controller playerController;
    private magnet playerMagnet;

    void Update()
    {
        if (player == null)
            return;

        // 磁石OFFまたは極変更で解除
        if (playerMagnet == null || !playerMagnet.isActive || playerMagnet.magnetMode != 2)
        {
            ReleasePlayer();
            return;
        }

        // 箱の側面に固定
        player.position = transform.position - transform.forward * attachDistance;
        player.rotation = Quaternion.LookRotation(-transform.forward, Vector3.up);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        magnet m = other.GetComponentInChildren<magnet>();

        if (m == null)
            return;

        if (!m.isActive)
            return;

        // ロープウェイはN極
        if (m.magnetMode != 2)
            return;

        if (player == null)
        {
            player = other.transform;
            playerRb = other.GetComponent<Rigidbody>();
            playerController = other.GetComponent<Controller>();
            playerMagnet = m;

            if (playerRb != null)
            {
                playerRb.linearVelocity = Vector3.zero;
                playerRb.useGravity = false;
            }

            if (playerController != null)
            {
                playerController.isOnRopeway = true;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (player == other.transform)
        {
            ReleasePlayer();
        }
    }

    private void ReleasePlayer()
    {
        if (playerRb != null)
        {
            playerRb.useGravity = true;
            playerRb.linearVelocity = Vector3.zero;
        }

        if (playerController != null)
        {
            playerController.isOnRopeway = false;
        }

        player = null;
        playerRb = null;
        playerController = null;
        playerMagnet = null;
    }
}