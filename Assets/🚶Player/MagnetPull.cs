using UnityEngine;

// Catch中(ZRホールド)に、画面中央のRayで「逆極」の対象に作用する。対象の種類で分岐:
//   ・Grapple 点      → 立体機動(ワイヤーで飛びつく)
//   ・jump(ジャンプ台)→ 大きくジャンプ(一発)
//   ・RopewayMagnet    → ロープウェイに吸着して運ばれる
//   ・それ以外(物体)  → 手元(handPoint)へ引き寄せる
// 逆極の対応: プレイヤー S極 → "N_Pole" / N極 → "S_Pole"
// ・ZRを離す(Catch終了)で全て解除。物体は保持中に極を切り替えると反発でぶっ飛ばす。
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
	private AuraRing heldAura;   // 掴んだ物のオーラ(持った通知用)
	private Bomb heldBomb;       // 掴んだ物が爆弾なら投擲フラグ用

	// --- ギミック相互作用（引き寄せ対象がギミックなら、物体を引くのではなくこちらが作用する） ---
	private Rigidbody playerRb;            // 自分のRigidbody(ジャンプ台で使う)
	private Grapple currentGrapple;        // 立体機動中の対象
	private RopewayMagnet currentRopeway;  // ロープウェイ吸着中の対象
	private float jumpCooldown;            // ジャンプ台の連射防止

	private bool Interacting => held != null || currentGrapple != null || currentRopeway != null;

	// エフェクト等が参照する：引き寄せ中/保持中の対象(無ければnull)
	public Transform HeldObject => held != null ? held.transform : null;

	void Awake()
	{
		catchState = GetComponent<PlayerCatch>();
		stateMachine = GetComponent<PlayerStateMachine>();
		playerRb = GetComponent<Rigidbody>();
		if (aimCamera == null) aimCamera = Camera.main;
	}

	void Update()
	{
		if (jumpCooldown > 0f) jumpCooldown -= Time.deltaTime;

		if (!catchState.IsCatching)
		{
			EndInteraction(); // ZRを離した（Catch終了）
			return;
		}

		if (!Interacting)
		{
			if (jumpCooldown <= 0f) TryInteract();
		}
		else if (stateMachine.CurrentState != grabbedPole)
		{
			// 保持中に極を切り替えた
			if (held != null) Repel();   // 物体は反発でぶっ飛ばす
			else EndInteraction();        // 立体機動/ロープウェイは終了
		}
	}

	void FixedUpdate()
	{
		// 物体の引き寄せだけ毎物理ステップで動かす（ギミックは各自で動く）
		if (held != null && !attached) PullHeld();
	}

	// 相互作用の終了（ZR離し・極切替）
	private void EndInteraction()
	{
		if (held != null) Release();
		if (currentGrapple != null)
		{
			currentGrapple.StopGrapple();
			currentGrapple = null;
		}
		if (currentRopeway != null)
		{
			currentRopeway.DetachPlayer();
			currentRopeway = null;
		}
	}

	private Transform HandParent => handPoint != null ? handPoint : transform;
	private Vector3 HandPos => handPoint != null
		? handPoint.position
		: transform.position + transform.forward * 0.8f + Vector3.up * 1f;

	private string WantedTag()
		=> stateMachine.CurrentState == MagnetState.S ? "N_Pole" : "S_Pole";

	// 引き寄せ対象を判定して、種類ごとの作用を起動する
	private void TryInteract()
	{
		if (aimCamera == null) return;

		Ray ray = aimCamera.ScreenPointToRay(
			new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));

		// ギミック(ジャンプ台/ロープウェイ)はトリガーコライダーなので Collide で拾う。
		// 距離順に見て、自分は無視・非対象のトリガーは透過・非対象のソリッド(壁)で遮断。
		RaycastHit[] hits = Physics.RaycastAll(ray, rayRange, rayMask, QueryTriggerInteraction.Collide);
		System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

		foreach (var hit in hits)
		{
			Collider col = hit.collider;
			if (col.transform.IsChildOf(transform)) continue; // 自分は無視

			if (!col.CompareTag(WantedTag()))
			{
				if (col.isTrigger) continue; // 非対象のトリガーは視線を遮らない
				return;                       // 非対象のソリッド(壁等)で遮られる
			}

			// ① 立体機動：Grapple 点
			Grapple grapple = col.GetComponentInParent<Grapple>();
			if (grapple != null)
			{
				grabbedPole = stateMachine.CurrentState;
				grapple.StartGrapple(gameObject); // 内部で StartGrappleEffect も呼ばれる
				currentGrapple = grapple;
				return;
			}

			// ② ジャンプ台（一発で飛ぶ。連射防止にクールダウン）
			jump jumpStand = col.GetComponentInParent<jump>();
			if (jumpStand != null)
			{
				jumpStand.Launch(playerRb);
				jumpCooldown = 0.6f;
				return;
			}

			// ③ ロープウェイ吸着
			RopewayMagnet ropeway = col.GetComponentInParent<RopewayMagnet>();
			if (ropeway != null)
			{
				grabbedPole = stateMachine.CurrentState;
				ropeway.AttachPlayer(gameObject);
				currentRopeway = ropeway;
				return;
			}

			// ④ それ以外：通常の物体引き寄せ
			Rigidbody rb = col.attachedRigidbody;
			if (rb != null) Grab(rb);
			return;
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

		// オーラ/爆弾との連携
		heldAura = rb.GetComponentInChildren<AuraRing>();
		heldBomb = rb.GetComponent<Bomb>();
		if (heldBomb != null) heldBomb.isThrown = false;
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
		if (heldAura != null) heldAura.SetHeld(true);
	}

	// ZRを離した：物理を元に戻して落とす
	private void Release()
	{
		if (attached) held.transform.SetParent(null);
		RestoreColliders();
		if (heldAura != null) heldAura.SetHeld(false);
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
		if (heldAura != null) heldAura.SetHeld(false);
		if (heldBomb != null) heldBomb.isThrown = true;
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
		heldAura = null;
		heldBomb = null;
		attached = false;
		pullVel = Vector3.zero;
	}
}
