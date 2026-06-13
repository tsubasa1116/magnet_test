using UnityEngine;
using UnityEngine.InputSystem;

// ZR(Magnet ON OFF アクション)を押している間、画面中央のRayで「逆極」の物体を
// だんだん加速しながら引き寄せ、手元(handPoint)にくっつける。
//   プレイヤー S極 → "N_Pole" を引き寄せ / N極 → "S_Pole" を引き寄せ
// ・ZRを離すと引き寄せ解除(落とす)
// ・保持中(ZR押下中)に自分の極を切り替える(ZL/Q)と、同極になって反発し、
//   自分の向いている方向へ物体がぶっ飛ぶ。
[RequireComponent(typeof(PlayerStateMachine))]
[RequireComponent(typeof(PlayerInput))]
public class MagnetPull : MonoBehaviour
{
	[Header("参照")]
	[Tooltip("引き寄せた物体がくっつく位置(手のボーンなど)")]
	[SerializeField] private Transform handPoint;
	[Tooltip("中央Ray用カメラ。未指定なら Camera.main")]
	[SerializeField] private Camera aimCamera;

	[Header("Ray")]
	[SerializeField] private float rayRange = 30f;
	[SerializeField] private LayerMask rayMask = ~0;

	[Header("引き寄せ")]
	[SerializeField] private float pullAcceleration = 25f;
	[SerializeField] private float maxPullSpeed = 40f;
	[SerializeField] private float attachDistance = 0.3f;

	[Header("反発(極切替でぶっ飛ばす)")]
	[SerializeField] private float repelForce = 30f;

	private PlayerStateMachine stateMachine;
	private InputAction pullAction;

	private Rigidbody held;            // 引き寄せ中/保持中の物体
	private float pullSpeed;           // 現在の引き寄せ速度(加速で増える)
	private bool attached;             // 手元に固定済みか
	private MagnetState grabbedPole;   // 掴んだ時の自分の極(切替検出用)

	// 離す/反発時に元へ戻すための退避
	private bool savedUseGravity;
	private bool savedIsKinematic;

	void Awake()
	{
		stateMachine = GetComponent<PlayerStateMachine>();
		if (aimCamera == null) aimCamera = Camera.main;
		pullAction = GetComponent<PlayerInput>().actions["Magnet ON OFF"]; // ZR
	}

	void Update()
	{
		bool pulling = pullAction != null && pullAction.IsPressed();

		if (!pulling)
		{
			if (held != null) Release(); // ZRを離した
			return;
		}

		if (held == null)
		{
			TryGrab();
		}
		else if (stateMachine.CurrentState != grabbedPole)
		{
			// 保持中に極を切り替えた＝同極になり反発 → 向いている方向へ飛ばす
			Repel();
		}
	}

	void FixedUpdate()
	{
		if (held != null && !attached) PullHeld();
	}

	// 引き寄せ対象のタグ(逆極)
	private string WantedTag()
		=> stateMachine.CurrentState == MagnetState.S ? "N_Pole" : "S_Pole";

	private void TryGrab()
	{
		if (aimCamera == null) return;

		Ray ray = aimCamera.ScreenPointToRay(
			new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));

		if (!Physics.Raycast(ray, out RaycastHit hit, rayRange, rayMask)) return;
		if (!hit.collider.CompareTag(WantedTag())) return;

		Rigidbody rb = hit.collider.attachedRigidbody;
		if (rb == null) return;

		held = rb;
		attached = false;
		pullSpeed = 0f;
		grabbedPole = stateMachine.CurrentState;

		// 引き寄せ中は物理を一旦止めて暴れさせない
		savedUseGravity = rb.useGravity;
		savedIsKinematic = rb.isKinematic;
		rb.useGravity = false;
		rb.linearVelocity = Vector3.zero;
		rb.angularVelocity = Vector3.zero;
	}

	private void PullHeld()
	{
		if (handPoint == null) return;

		Vector3 to = handPoint.position - held.position;
		if (to.magnitude <= attachDistance)
		{
			Attach();
			return;
		}

		// だんだん加速
		pullSpeed = Mathf.Min(pullSpeed + pullAcceleration * Time.fixedDeltaTime, maxPullSpeed);
		Vector3 step = to.normalized * pullSpeed * Time.fixedDeltaTime;
		if (step.sqrMagnitude > to.sqrMagnitude) step = to; // 行き過ぎ防止
		held.MovePosition(held.position + step);
	}

	private void Attach()
	{
		attached = true;
		held.isKinematic = true;
		held.transform.SetParent(handPoint); // 手に追従
		held.transform.position = handPoint.position;
	}

	// ZRを離した：物理を元に戻して落とす
	private void Release()
	{
		if (attached) held.transform.SetParent(null);
		held.isKinematic = savedIsKinematic;
		held.useGravity = savedUseGravity;
		ClearHeld();
	}

	// 極切替：自分の向いている方向へぶっ飛ばす
	private void Repel()
	{
		Vector3 dir = transform.forward;
		if (attached) held.transform.SetParent(null);
		held.isKinematic = false;
		held.useGravity = true;
		held.AddForce(dir * repelForce, ForceMode.Impulse);
		ClearHeld();
	}

	private void ClearHeld()
	{
		held = null;
		attached = false;
		pullSpeed = 0f;
	}
}
