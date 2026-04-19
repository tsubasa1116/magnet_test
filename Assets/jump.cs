using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class jump : MonoBehaviour
{
    public AudioClip jumpSound;

    // ジャンプする力（上向きの力）を定義
    [SerializeField] private float jumpForceX = 0f;
    [SerializeField] private float jumpForceY = 15.0f; // ※Impulseで飛ばすなら15～20くらいで飛びます
    [SerializeField] private float jumpForceZ = 0f;

    /// <summary>
    /// Colliderが他のトリガーに入った時に呼び出される
    /// </summary>
    /// <param name="other">当たった相手のオブジェクト</param>
    private void OnTriggerEnter(Collider other)
    {
        // 当たった相手のタグが"Player"だった場合
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

				// 床自身の極性をタグで判定
				bool isThisN = gameObject.CompareTag("N_Pole");
                bool isThisS = gameObject.CompareTag("S_Pole");

                // プレイヤーの現在のモード
                int mode = playerMagnet.magnetMode;

                // 【同じ極同士だった場合】上にジャンプさせる
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
                        // 落下中の速度を一度リセットすると、毎回安定した高さまで飛びます
                        Vector3 vel = playerRb.velocity;
                        vel.y = 0;
                        playerRb.velocity = vel;

                        // プレイヤーに上向きの力を加える
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

    // 床に乗ったままでモードを後から切り替えた時にも飛ぶように Stay も追加
    private void OnTriggerStay(Collider other)
    {
        OnTriggerEnter(other);
    }
}