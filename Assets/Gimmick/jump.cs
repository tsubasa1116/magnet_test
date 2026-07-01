using UnityEngine;

// ジャンプ台。プレイヤーが「同じ極」で触れると反発し、台の面の向き(up)へ大きく飛ばす。
// 引き寄せ(ZR)ではなく、乗った/触れた瞬間に発動する。
public class jump : MonoBehaviour
{
	public AudioClip jumpSound;

	[SerializeField] private float jumpForce = 25f;
	[Tooltip("連続発動を防ぐ間隔(秒)")]
	[SerializeField] private float retriggerCooldown = 0.3f;

	[Header("エフェクト")]
	[SerializeField] private GameObject jumpEffect;

	private float cooldown;

	void Update()
	{
		if (cooldown > 0f) cooldown -= Time.deltaTime;
	}

	private void OnTriggerStay(Collider other)
	{
		if (cooldown > 0f) return;
		if (!other.CompareTag("Player")) return;

		var state = other.GetComponentInParent<PlayerStateMachine>();
		if (state == null) return;

		// この台の極(タグ)とプレイヤーの極が「同じ」なら反発してジャンプ
		bool standN = CompareTag("N_Pole");
		bool same = standN
			? state.CurrentState == MagnetState.N
			: state.CurrentState == MagnetState.S;
		if (!same) return;

		Rigidbody rb = other.GetComponentInParent<Rigidbody>();
		if (rb == null) return;

		// 台の面が向いている方向(up)へ飛ばす。まずその方向の速度成分を消して安定させる
		Vector3 dir = transform.up;
		rb.linearVelocity -= Vector3.Project(rb.linearVelocity, dir);
		rb.AddForce(dir * jumpForce, ForceMode.Impulse);

		if (jumpSound != null) AudioSource.PlayClipAtPoint(jumpSound, transform.position);
		if (jumpEffect != null) Instantiate(jumpEffect, transform.position + dir * 0.5f, Quaternion.identity);

		cooldown = retriggerCooldown;
	}
}
