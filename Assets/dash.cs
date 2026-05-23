using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class dash : MonoBehaviour
{
    public AudioClip dashSound;

    // ガウス加速器のような強い力で吹き飛ばす
    [SerializeField] private float dashForce = 30.0f;

    // Y軸（上方向）に少し力を持たせる（地面に擦らずに飛ぶため）
    [SerializeField] private float liftForce = 2.0f;

    /// <summary>
    /// Colliderのトリガー領域に侵入した時に呼び出される関数
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // 衝突したオブジェクトのタグが"Player"である場合
        if (other.gameObject.CompareTag("Player"))
        {
            // プレイヤーにアタッチされている磁力スクリプトを取得
            magnet playerMagnet = other.GetComponentInChildren<magnet>();

            if (playerMagnet != null)
            {
				if (!playerMagnet.isActive)
				{
					return;
				}

				// 電極ポールの極性をタグで判定
				bool isThisN = gameObject.CompareTag("N_Pole");
                bool isThisS = gameObject.CompareTag("S_Pole");

                // プレイヤーの現在の磁力モード
                int mode = playerMagnet.magnetMode;

                // 【同極に接触した場合】同じ極性なら反発して強く弾く（ダッシュ）
                if ((mode == 1 && isThisN) || (mode == 2 && isThisS))
                {
                    // 再生音が設定されていれば鳴らす
                    if (dashSound != null)
                    {
                        AudioSource.PlayClipAtPoint(dashSound, transform.position);
                    }

                    Rigidbody playerRb = other.GetComponent<Rigidbody>();
                    if (playerRb != null)
                    {
                        // 落下などの現在の速度を一度リセットする（安定して飛ばすため）
                        playerRb.linearVelocity = Vector3.zero;

                        // 変更：プレイヤーが向いている前方のベクトルの「逆（後ろ向き）」を取得
                        Vector3 backwardDir = -other.transform.forward;

                        // 後方への力に、上方向の力(liftForce)を加えて斜め後ろに飛び出す
                        Vector3 launchDirection = (backwardDir * dashForce) + (Vector3.up * liftForce);

                        // プレイヤーに力を加える
                        playerRb.AddForce(launchDirection, ForceMode.Impulse);
                    }

                    // プレイヤーのControllerに「ジャンプ（空中）」状態であることを伝える
                    Controller playerCtrl = other.GetComponent<Controller>();
                    if (playerCtrl != null)
                    {
                        playerCtrl.isJumping = true;
                    }
                }
            }
        }
    }

    // 触れたままでモードが切り替わった場合にも飛ぶように Stay イベントを追加
    private void OnTriggerStay(Collider other)
    {
        OnTriggerEnter(other);
    }
}