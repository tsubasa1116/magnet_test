using UnityEngine;

public class dash : MonoBehaviour
{
    [Header("効果音")]
    [SerializeField] private AudioClip dashSound;

    [Header("ダッシュ設定")]
    // 吹き飛ばす力
    [SerializeField] private float dashForce = 2000.0f;

    // 少し上方向へ飛ばす力
    [SerializeField] private float liftForce = 2.0f;

    /// <summary>
    /// トリガーに入った瞬間
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // プレイヤー以外は無視
        if (!other.CompareTag("Player"))
            return;

        // プレイヤーの状態取得
        PlayerStateMachine playerState = other.GetComponent<PlayerStateMachine>();
        if (playerState == null)
            return;

        // プレイヤーの磁力状態取得
        PlayerCatch playerCatch = other.GetComponent<PlayerCatch>();
        if (playerCatch == null || !playerCatch.IsCatching)
            return;

        // このオブジェクトの極性
        bool isThisN = CompareTag("N_Pole");
        bool isThisS = CompareTag("S_Pole");

        // 同極なら反発
        bool samePole =
            (playerState.CurrentState == MagnetState.N && isThisN) ||
            (playerState.CurrentState == MagnetState.S && isThisS);

        if (!samePole)
            return;

        // 効果音
        if (dashSound != null)
        {
            AudioSource.PlayClipAtPoint(dashSound, transform.position);
        }

        // Rigidbody取得
        Rigidbody playerRb = other.GetComponent<Rigidbody>();
        if (playerRb == null)
            return;

        // 現在の速度をリセット
        playerRb.linearVelocity = Vector3.zero;

        // オブジェクト正面に飛ばす
        Vector3 launchDirection =
            -transform.forward * dashForce +
            Vector3.up * liftForce;

        playerRb.AddForce(launchDirection, ForceMode.VelocityChange);

        PlayerMovement movement = other.GetComponent<PlayerMovement>();

        if (movement != null)
        {
            movement.StartExternalForce(0.3f);
        }
    }

    /// <summary>
    /// 接触中に極性が切り替わった場合も反応させる
    /// </summary>
    //private void OnTriggerStay(Collider other)
    //{
    //    OnTriggerEnter(other);
    //}
}