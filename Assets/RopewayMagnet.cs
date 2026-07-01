using UnityEngine;

// ロープウェイの磁石部分。プレイヤーが引き寄せ(逆極)で作用させると、
// 立体機動と同じくスプリングで磁石へ吸い込まれる。磁石は動くので運ばれる。
// 途中で離す(ZR解除)と関節が外れ、慣性でスイングバイする。
// 起動はプレイヤー側(MagnetPull)から Attach/Detach を呼ぶ。
public class RopewayMagnet : MonoBehaviour
{
	[Tooltip("吸い込み速度(ワイヤーを縮める速さ)。速すぎないように")]
	[SerializeField] private float pullSpeed = 6f;
	[SerializeField] private float spring = 10f;
	[SerializeField] private float damper = 5f;
	[SerializeField] private float startImpulse = 8f;

	private Rigidbody playerRb;
	private SpringJoint joint;

	public bool HasPlayer => joint != null;

	// プレイヤーから呼ぶ：吸着開始（スプリングで引き寄せ）
	public void AttachPlayer(GameObject playerObj)
	{
		if (joint != null) return;

		playerRb = playerObj.GetComponent<Rigidbody>();
		if (playerRb == null) return;

		joint = playerObj.AddComponent<SpringJoint>();
		joint.autoConfigureConnectedAnchor = false;
		joint.connectedAnchor = transform.position;

		float dist = Vector3.Distance(playerObj.transform.position, transform.position);
		joint.maxDistance = dist * 0.8f;
		joint.minDistance = 0f;
		joint.spring = spring;
		joint.damper = damper;
		joint.massScale = 4.5f;

		// 少し勢いをつける
		Vector3 dir = (transform.position - playerObj.transform.position).normalized;
		playerRb.AddForce(dir * startImpulse, ForceMode.Impulse);
	}

	// プレイヤーから呼ぶ：解除（関節を外す＝慣性でスイングバイ）
	public void DetachPlayer()
	{
		if (joint != null) Destroy(joint);
		joint = null;
		playerRb = null;
	}

	void FixedUpdate()
	{
		if (joint == null) return;
		// 磁石が動くのでアンカーを追従させ、ワイヤーを徐々に縮めて吸い込む
		joint.connectedAnchor = transform.position;
		joint.maxDistance = Mathf.MoveTowards(joint.maxDistance, 0f, pullSpeed * Time.fixedDeltaTime);
	}
}
