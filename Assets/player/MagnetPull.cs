using UnityEngine;

// Catch中(ZRホールド)に、画面中央のRayで「逆極」の物体を手元(handPoint)へ引き寄せる。
//   プレイヤー S極 → "N_Pole" / N極 → "S_Pole" を引き寄せ
// ・ZRを離す(Catch終了)と落とす
// ・保持中に自分の極を切り替える(ZL/Q)と同極で反発し、向いている方向へ飛ばす
[RequireComponent(typeof(PlayerCatch))]
[RequireComponent(typeof(PlayerStateMachine))]
public class MagnetPull : MonoBehaviour
{
	[Header("参照")]
	[Tooltip("引き寄せた物体がくっつく位置(手のボーンなど)。未指定なら体の前方")]
	[SerializeField] private Transform handPoint;
	[Tooltip("中央Ray用カメラ。未指定なら Camera.main")]
	[SerializeField] private Camera aimCamera;

	[Header("Ray")]
	[SerializeField] private float rayRange = 30f;
	[SerializeField] private LayerMask rayMask = ~0;

	[Header("引き寄せ(磁石っぽい浮遊感)")]
	[Tooltip("小さいほど機敏に吸い寄せ、大きいほどゆっくり漂って近づく")]
	[SerializeField] private float pullSmoothTime = 0.25f;
	[SerializeField] private float maxPullSpeed = 40f;
	[SerializeField] private float attachDistance = 0.3f;
	[Tooltip("引き寄せ中の上下のゆらぎ(浮遊感)。近づくほど弱まる")]
	[SerializeField] private float floatAmplitude = 0.15f;
	[SerializeField] private float floatFrequency = 6f;

	[Header("反発(極切替でぶっ飛ばす)")]
	[SerializeField] private float repelForce = 30f;

	private PlayerCatch catchState;
	private PlayerStateMachine stateMachine;

	private Rigidbody held;
	private Vector3 pullVel;           // SmoothDamp用の速度
	private Collider[] heldColliders;  // 保持中に無効化するコライダー
	private bool attached;
	private MagnetState grabbedPole;
	private bool savedUseGravity;
	private bool savedIsKinematic;

	// エフェクト等が参照する：引き寄せ中/保持中の対象(無ければnull)
	public Transform HeldObject => held != null ? held.transform : null;

	void Awake()
	{
		catchState = GetComponent<PlayerCatch>();
		stateMachine = GetComponent<PlayerStateMachine>();
		if (aimCamera == null) aimCamera = Camera.main;
	}

	void Update()
	{
		if (!catchState.IsCatching)
		{
			if (held != null) Release(); // ZRを離した（Catch終了）
			return;
		}

		if (held == null)
		{
			TryGrab();
		}
		else if (stateMachine.CurrentState != grabbedPole)
		{
			Repel(); // 保持中に極切替＝同極で反発
		}
	}

	void FixedUpdate()
	{
		if (held != null && !attached) PullHeld();
	}

	private Transform HandParent => handPoint != null ? handPoint : transform;
	private Vector3 HandPos => handPoint != null
		? handPoint.position
		: transform.position + transform.forward * 0.8f + Vector3.up * 1f;

	private string WantedTag()
		=> stateMachine.CurrentState == MagnetState.S ? "N_Pole" : "S_Pole";

	private void TryGrab()
	{
		if (aimCamera == null) return;

		Ray ray = aimCamera.ScreenPointToRay(
			new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));

		// 三人称なので中央Rayは自分に当たりやすい。全ヒットを距離順に見て自分を除外し、
		// 最初に当たった「自分以外」が対象タグなら掴む（手前に壁等があれば掴まない）。
		RaycastHit[] hits = Physics.RaycastAll(ray, rayRange, rayMask, QueryTriggerInteraction.Ignore);
		System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

		foreach (var hit in hits)
		{
			if (hit.collider.transform.IsChildOf(transform)) continue; // 自分は無視

			if (hit.collider.CompareTag(WantedTag()))
			{
				Rigidbody rb = hit.collider.attachedRigidbody;
				if (rb != null) Grab(rb);
			}
			return; // 最初の「自分以外」のヒットで判定終了（壁越しには掴まない）
		}
	}

	private void Grab(Rigidbody rb)
	{
		held = rb;
		attached = false;
		pullVel = Vector3.zero;
		grabbedPole = stateMachine.CurrentState;

		// 引き寄せ中はキネマティックにして確実に・滑らかに動かす
		savedUseGravity = rb.useGravity;
		savedIsKinematic = rb.isKinematic;
		rb.isKinematic = true;
		rb.useGravity = false;
		rb.linearVelocity = Vector3.zero;
		rb.angularVelocity = Vector3.zero;

		// 保持中はコライダーを無効化（プレイヤーや地面を押さない）
		heldColliders = rb.GetComponentsInChildren<Collider>();
		foreach (var c in heldColliders) c.enabled = false;
	}

	private void PullHeld()
	{
		Vector3 to = HandPos - held.position;
		if (to.magnitude <= attachDistance)
		{
			Attach();
			return;
		}

		// 上下のゆらぎ（浮遊感）。近づくほど弱める
		float distFactor = Mathf.Clamp01(to.magnitude / 3f);
		Vector3 bob = Vector3.up * Mathf.Sin(Time.time * floatFrequency) * floatAmplitude * distFactor;
		Vector3 target = HandPos + bob;

		// 磁石に吸い寄せられるように滑らかに移動（SmoothDamp=ふわっと加速→減速）
		Vector3 next = Vector3.SmoothDamp(held.position, target, ref pullVel, pullSmoothTime, maxPullSpeed, Time.fixedDeltaTime);
		held.MovePosition(next);
	}

	private void Attach()
	{
		attached = true;
		held.transform.SetParent(HandParent); // 手に追従
		held.transform.position = HandPos;
	}

	// ZRを離した：物理を元に戻して落とす
	private void Release()
	{
		if (attached) held.transform.SetParent(null);
		RestoreColliders();
		held.isKinematic = savedIsKinematic;
		held.useGravity = savedUseGravity;
		ClearHeld();
	}

	// 極切替：自分の向いている方向へぶっ飛ばす
	private void Repel()
	{
		Vector3 dir = transform.forward;
		if (attached) held.transform.SetParent(null);
		RestoreColliders();
		held.isKinematic = false;
		held.useGravity = true;
		held.AddForce(dir * repelForce, ForceMode.Impulse);
		ClearHeld();
	}

	private void RestoreColliders()
	{
		if (heldColliders == null) return;
		foreach (var c in heldColliders)
			if (c != null) c.enabled = true;
	}

	private void ClearHeld()
	{
		held = null;
		heldColliders = null;
		attached = false;
		pullVel = Vector3.zero;
	}
}
