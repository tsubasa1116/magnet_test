using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

// 体力0でラグドール化する。
// 事前に Unity の Ragdoll Wizard（GameObject > 3D Object > Ragdoll...）で
// 各ボーンに Rigidbody / Collider / CharacterJoint を付けておくこと。
//
// 通常時：ラグドール用ボーンの Rigidbody はキネマティック＆当たり判定OFFにして眠らせる
//         （本体カプセルや移動には一切干渉しない）。
// 死亡時：操作・アニメを止め、ボーンの物理を有効化して慣性・重力で崩れ落ちる。
public class PlayerRagdoll : MonoBehaviour
{
	[Header("死亡時に無効化する制御系")]
	[Tooltip("PlayerMovement / PlayerAim / PlayerStateMachine / MagnetPull / HeadLookAtEnemy / AnimationStateController / PlayerInput など")]
	[SerializeField] private Behaviour[] disableOnDeath;
	[SerializeField] private Animator animator;        // 未指定なら自動取得
	[SerializeField] private Rigidbody mainRigidbody;  // 移動用ルートRigidbody(未指定なら自動取得)
	[SerializeField] private Collider mainCollider;    // ルートの当たり(カプセル)。死亡時のみ無効化
	[Tooltip("死亡時に視点の旋回を止めるFreeLook(任意)")]
	[SerializeField] private CinemachineFreeLook lookCamera;

	// ラグドール用ボーン（＝ルート以外の Rigidbody）と、それに付いた Collider だけを操作する。
	// 本体カプセル等の「ボーンに属さないコライダー」は絶対に触らない（動けなくなるのを防ぐ）。
	private Rigidbody[] boneBodies;
	private Collider[] boneColliders;

	void Awake()
	{
		if (animator == null) animator = GetComponent<Animator>();
		if (mainRigidbody == null) mainRigidbody = GetComponent<Rigidbody>();

		var bodies = new List<Rigidbody>();
		var cols = new List<Collider>();
		foreach (var rb in GetComponentsInChildren<Rigidbody>(true))
		{
			if (rb == mainRigidbody) continue; // ルート本体は対象外
			bodies.Add(rb);
			var c = rb.GetComponent<Collider>();
			if (c != null) cols.Add(c);
		}
		boneBodies = bodies.ToArray();
		boneColliders = cols.ToArray();

		SetRagdollActive(false); // 通常時は眠らせる(アニメ駆動)
	}

	// ボーンの物理ON/OFF。false=キネマティック&当たり判定OFF
	private void SetRagdollActive(bool active)
	{
		foreach (var rb in boneBodies)
		{
			rb.isKinematic = !active;
			rb.useGravity = active;
			rb.detectCollisions = active; // 通常時OFF＝本体や地面に干渉しない
			if (active)
			{
				rb.linearVelocity = Vector3.zero;
				rb.angularVelocity = Vector3.zero;
			}
		}

		foreach (var c in boneColliders)
			c.enabled = active;
	}

	// 体力0などで呼ぶ：操作不能＋慣性・重力で崩れ落ちる
	public void EnableRagdoll()
	{
		// 倒れる直前の移動速度を引き継ぐ（急停止せず自然に倒れる＝暴れ軽減）
		Vector3 inheritVelocity = mainRigidbody != null ? mainRigidbody.linearVelocity : Vector3.zero;

		// アニメと操作を停止（アニメが動いているとボーンが上書きされて倒れない）
		if (animator != null) animator.enabled = false;
		if (disableOnDeath != null)
			foreach (var b in disableOnDeath)
				if (b != null) b.enabled = false;

		// ルートの移動制御を無効化（物理に任せる）。回転も固定する。
		if (mainCollider != null) mainCollider.enabled = false;
		if (mainRigidbody != null)
		{
			mainRigidbody.isKinematic = true;
			mainRigidbody.constraints = RigidbodyConstraints.FreezeAll;
		}

		// カメラの視点旋回を止める（FreeLookは旧Input直読みなので操作系を切っても回るため）
		if (lookCamera != null)
		{
			lookCamera.m_XAxis.m_MaxSpeed = 0f;
			lookCamera.m_YAxis.m_MaxSpeed = 0f;
		}

		// ボーン物理ON
		SetRagdollActive(true);

		// 慣性を引き継ぐ
		foreach (var rb in boneBodies)
			rb.linearVelocity = inheritVelocity;
	}
}
