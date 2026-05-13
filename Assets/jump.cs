using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class jump : MonoBehaviour
{
    public AudioClip jumpSound;

    // ジャンプ力の定義
    [SerializeField] private float jumpForceX = 0f;
    [SerializeField] private float jumpForceY = 15.0f; // ※Impulseで飛ばすなら15～20くらいで飛びます
    [SerializeField] private float jumpForceZ = 0f;

    /// <summary>
    /// Colliderがこのトリガーに入った時に呼び出される
    /// </summary>
    /// <param name="other">侵入してきたオブジェクト</param>
    private void OnTriggerEnter(Collider other)
    {
        // 侵入してきたオブジェクトのタグが"Player"だった場合
        if (other.gameObject.CompareTag("Player"))
        {
            // プレイヤーが持っている磁石スクリプトを取得
            magnet playerMagnet = other.GetComponentInChildren<magnet>();

            if (playerMagnet != null)
            {
				if (!playerMagnet.isActive)
				{
					return;
				}

				// 自身の極性をタグで判定
				bool isThisN = gameObject.CompareTag("N_Pole");
                bool isThisS = gameObject.CompareTag("S_Pole");

                // プレイヤーの現在のモード
                int mode = playerMagnet.magnetMode;

                // 反発により同じ極同士の場合、上にジャンプさせる
                if ((mode == 1 && isThisN) || (mode == 2 && isThisS))
                {
                    // 音が設定されていれば鳴らす
                    if (jumpSound != null)
                    {
                        AudioSource.PlayClipAtPoint(jumpSound, transform.position);
                    }

                    Rigidbody playerRb = other.GetComponent<Rigidbody>();
                    if (playerRb != null)
                    {
                        // 現在の落下速度などを一度リセットしないと、安定した高さで飛びません
                        Vector3 vel = playerRb.linearVelocity;
                        vel.y = 0;
                        playerRb.linearVelocity = vel;

                        // プレイヤーに上方向の力を加える
                        playerRb.AddForce(new Vector3(jumpForceX, jumpForceY, jumpForceZ), ForceMode.Impulse);
                    }

                    // プレイヤーのControllerに「ジャンプ中」であることを伝える
                    Controller playerCtrl = other.GetComponent<Controller>();
                    if (playerCtrl != null)
                    {
                        playerCtrl.isJumping = true;
                    }
                }
            }
        }
    }

    // 上に乗ったままでモードが切り替わった時にも飛ぶように Stay も追加
    private void OnTriggerStay(Collider other)
    {
        OnTriggerEnter(other);
    }
}