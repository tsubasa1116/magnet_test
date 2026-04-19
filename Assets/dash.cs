using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class dash : MonoBehaviour
{
    public AudioClip dashSound;

    // ガウス加速のような吹き飛ばす強い力
    [SerializeField] private float dashForce = 30.0f;

    // Y軸（上方向）に少し浮かせる力 (地面に引っかからずに飛ぶため)
    [SerializeField] private float liftForce = 2.0f;

    /// <summary>
    /// Colliderが他のトリガーに入った時に呼び出される
    /// </summary>
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

				// 仕掛け自身の極性をタグで判定
				bool isThisN = gameObject.CompareTag("N_Pole");
                bool isThisS = gameObject.CompareTag("S_Pole");

                // プレイヤーの現在のモード
                int mode = playerMagnet.magnetMode;

                // 【同じ極同士だった場合】後ろに勢いよく弾く（反発）
                if ((mode == 1 && isThisN) || (mode == 2 && isThisS))
                {
                    // 音が設定されていれば鳴らす
                    if (dashSound != null)
                    {
                        AudioSource.PlayClipAtPoint(dashSound, transform.position);
                    }

                    Rigidbody playerRb = other.GetComponent<Rigidbody>();
                    if (playerRb != null)
                    {
                        // 落下中などの速度を一度リセットする（安定して飛ばすため）
                        playerRb.velocity = Vector3.zero;

                        // ★変更：プレイヤーが今向いている前方ベクトルの「逆（マイナス）」を取得
                        Vector3 backwardDir = -other.transform.forward;

                        // 後方に強く、少し上向き(liftForce)で斜め後ろに打ち出す
                        Vector3 launchDirection = (backwardDir * dashForce) + (Vector3.up * liftForce);

                        // プレイヤーに力を加える
                        playerRb.AddForce(launchDirection, ForceMode.Impulse);
                    }

                    // プレイヤーのControllerに「ジャンプ（空中）中」であることを伝える
                    Controller playerCtrl = other.GetComponent<Controller>();
                    if (playerCtrl != null)
                    {
                        playerCtrl.isJumping = true;
                    }
                }
            }
        }
    }

    // 乗ったままでモードを後から切り替えた時にも飛ぶように Stay も追加
    private void OnTriggerStay(Collider other)
    {
        OnTriggerEnter(other);
    }
}