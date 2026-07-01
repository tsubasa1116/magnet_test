using UnityEngine;

// ロープウェイの磁石部分。プレイヤーが引き寄せ(逆極)で作用させると吸着し、
// ロープウェイ(親が動く)に運ばれる。起動はプレイヤー側(MagnetPull)から呼ぶ。
public class RopewayMagnet : MonoBehaviour
{
	[SerializeField] private float attachDistance = 0.8f;

	private Transform player;
	private Rigidbody playerRb;
	private PlayerMovement playerMovement;

	public bool HasPlayer => player != null;

	// プレイヤーから呼ぶ：吸着開始
	public void AttachPlayer(GameObject playerObj)
	{
		player = playerObj.transform;
		playerRb = playerObj.GetComponent<Rigidbody>();
		playerMovement = playerObj.GetComponent<PlayerMovement>();

		if (playerRb != null)
		{
			playerRb.linearVelocity = Vector3.zero;
			playerRb.useGravity = false;
		}
		if (playerMovement != null) playerMovement.IsOnRopeway = true;
	}

	// プレイヤーから呼ぶ：吸着解除
	public void DetachPlayer()
	{
		if (playerRb != null)
		{
			playerRb.useGravity = true;
			playerRb.linearVelocity = Vector3.zero;
		}
		if (playerMovement != null) playerMovement.IsOnRopeway = false;

		player = null;
		playerRb = null;
		playerMovement = null;
	}

	void LateUpdate()
	{
		if (player == null) return;
		// 磁石の側面にプレイヤーを固定（親=ロープウェイが動くと一緒に運ばれる）
		player.position = transform.position - transform.forward * attachDistance;
		player.rotation = Quaternion.LookRotation(-transform.forward, Vector3.up);
	}
}
