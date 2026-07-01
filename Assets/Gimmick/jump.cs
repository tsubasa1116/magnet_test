using UnityEngine;

// ジャンプ台。プレイヤーが引き寄せ(逆極)で作用させると大きくジャンプする。
// 起動はプレイヤー側(MagnetPull)から Launch() を呼ぶ。
public class jump : MonoBehaviour
{
	public AudioClip jumpSound;

	[SerializeField] private float jumpForceX = 0f;
	[SerializeField] private float jumpForceY = 25.0f;
	[SerializeField] private float jumpForceZ = 0f;

	[Header("エフェクト")]
	[SerializeField] private GameObject jumpEffect;

	// プレイヤーから呼ぶ：上方向へ大きく飛ばす
	public void Launch(Rigidbody playerRb)
	{
		if (playerRb == null) return;

		if (jumpSound != null)
			AudioSource.PlayClipAtPoint(jumpSound, transform.position);

		// 落下速度をリセットしてから飛ばす（安定した高さに）
		Vector3 vel = playerRb.linearVelocity;
		vel.y = 0f;
		playerRb.linearVelocity = vel;

		playerRb.AddForce(new Vector3(jumpForceX, jumpForceY, jumpForceZ), ForceMode.Impulse);

		if (jumpEffect != null)
		{
			Vector3 pos = playerRb.transform.position;
			pos.y = 2.0f;
			Instantiate(jumpEffect, pos, Quaternion.identity);
		}
	}
}
