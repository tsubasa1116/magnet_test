using UnityEngine;

// ジャンプ台。
// プレイヤーが接触している間、MagnetPullへ自分を通知する。
// 実際にジャンプさせるのはMagnetPull側。
public class jump : MonoBehaviour
{
    public AudioClip jumpSound;

    private PlayerStateMachine playerState;

    [SerializeField] public float jumpForceX = 0f;
    [SerializeField] public float jumpForceY = 8.0f;
    [SerializeField] public float jumpForceZ = 0f;

    [Header("エフェクト")]
    [SerializeField] private GameObject jumpEffect;

    public void Launch(Rigidbody playerRb)
    {
        if (playerRb == null) return;

        if (jumpSound != null)
            AudioSource.PlayClipAtPoint(jumpSound, transform.position);

        // 落下速度をリセット
        Vector3 vel = playerRb.linearVelocity;
        vel.y = 0f;
        playerRb.linearVelocity = vel;

        // ジャンプ
        playerRb.AddForce(
            new Vector3(jumpForceX, jumpForceY, jumpForceZ),
            ForceMode.Impulse);

        // エフェクト
        if (jumpEffect != null)
        {
            Vector3 pos = playerRb.transform.position;
            pos.y = 1.5f;
            Instantiate(jumpEffect, pos, Quaternion.identity);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        MagnetPull magnet = collision.gameObject.GetComponent<MagnetPull>();
        if (magnet != null)
        {
            playerState = collision.gameObject.GetComponent<PlayerStateMachine>();
            magnet.SetCurrentJumpStand(this);
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        MagnetPull magnet = collision.gameObject.GetComponent<MagnetPull>();
        if (magnet != null)
        {
            magnet.SetCurrentJumpStand(null);
        }
    }

    public bool CanLaunch(PlayerStateMachine playerState)
    {
        if (playerState == null) return false;

        bool playerIsN = playerState.CurrentState == MagnetState.N;
        bool jumpIsN = CompareTag("N_Pole");

        return playerIsN == jumpIsN;
    }
}