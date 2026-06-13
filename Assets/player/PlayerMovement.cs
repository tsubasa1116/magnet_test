using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
	[Header("移動設定")]
	[SerializeField] private float moveSpeed = 5f;
	[SerializeField] private float dashSpeed = 12f;
	[Tooltip("見た目の向きが進行方向へ追従する速さ(度/秒)。小さいほどゆっくり振り向く。移動方向自体は即時")]
	[SerializeField] private float rotationSpeed = 480f;
	[Tooltip("目標速度に達するまでの加速度(/秒)。小さいほどダッシュがじわっと速くなる")]
	[SerializeField] private float acceleration = 20f;

	[Header("ジャンプ設定")]
	[SerializeField] private float jumpForce = 7f;
	[SerializeField] private LayerMask groundLayer;
	[Tooltip("1=通常の重力。小さいほどふわっと(滞空が伸びる)、大きいほどズシッと")]
	[SerializeField] private float gravityScale = 0.6f;
	[Tooltip("入力に対し実速度がこの割合未満なら『壁で止められている』とみなす(Idle表示用)")]
	[SerializeField, Range(0f, 1f)] private float blockedRatio = 0.3f;

	private Rigidbody rb;
	private Vector2 moveInput;
	private bool isDashing;
	private bool isGrounded;
	private bool isBlocked;
	private Transform cameraTransform;
	private PlayerHealth health;
	private InputAction dashAction;
	private PlayerAim aim;
	private Vector3 targetForward; // 見た目の向きの目標。入力が止んでも保持してそこへ向き続ける
	private float currentMoveSpeed; // 実際に適用中の移動速度(加速のため保持)

	// --- アニメーション側(AnimationStateController)が参照する状態フラグ ---
	// 「どう動いているか」はこの行動コードが持ち、見た目の制御はAnimation側に任せる
	public Vector2 MoveInput => moveInput;
	public bool IsMoving => moveInput.sqrMagnitude > 0.01f;
	public bool IsRunning => isDashing && IsMoving;
	public bool IsGrounded => isGrounded;
	// 入力はあるが壁などで実際に進めていない状態（アニメをIdleにするのに使う）
	public bool IsBlocked => isBlocked;

	// ジャンプした瞬間に通知する（アニメのトリガー用）
	public event System.Action Jumped;

	void Awake()
	{
		rb = GetComponent<Rigidbody>();
		// 物理で倒れないように回転を固定（向きはスクリプトで制御する）
		rb.freezeRotation = true;
		// 物理ステップ間を補間して、カメラ追従時のカクつきを防ぐ
		rb.interpolation = RigidbodyInterpolation.Interpolate;
		cameraTransform = Camera.main.transform;

		// Dashは「押している間だけ」なので、イベントではなく毎フレーム状態を読む
		var playerInput = GetComponent<PlayerInput>();
		dashAction = playerInput.actions["Dash"];

		aim = GetComponent<PlayerAim>();
		health = GetComponent<PlayerHealth>();

		Vector3 f = transform.forward;
		f.y = 0f;
		targetForward = f.sqrMagnitude > 0.001f ? f.normalized : Vector3.forward;
		currentMoveSpeed = moveSpeed;
	}

	// エイム中か（移動の向き制御とアニメ側が参照する）
	public bool IsAiming => aim != null && aim.IsAiming;

	void FixedUpdate()
	{
		// 死亡中は移動・回転を一切しない（ラグドール物理に任せる）
		if (health != null && health.IsDead)
		{
			isDashing = false;
			isBlocked = false;
			return;
		}

		// 実際のボタンの押下状態をそのまま反映（離せば必ずfalseに戻る）
		isDashing = dashAction != null && dashAction.IsPressed();
		isGrounded = CheckGrounded();

		// Move() が velocity を上書きする前に、前ステップで物理解決された実速度を測る。
		// 入力があるのに実速度が極端に小さい＝壁などで止められている。
		Vector3 v = rb.linearVelocity;
		v.y = 0f;
		float desired = (isDashing ? dashSpeed : moveSpeed) * Mathf.Clamp01(moveInput.magnitude);
		isBlocked = IsMoving && desired > 0.01f && v.magnitude < desired * blockedRatio;

		Move();
		ApplyExtraGravity();
	}

	// 標準重力に対して gravityScale 倍になるよう差分を加える。
	// gravityScale<1 で重力が弱まり、ジャンプがふわっとする。
	private void ApplyExtraGravity()
	{
		rb.AddForce(Physics.gravity * (gravityScale - 1f), ForceMode.Acceleration);
	}

	// --- 入力（PlayerInput経由でPlayerControlsから自動で呼ばれる：キー/コントローラー両対応） ---

	public void OnMove(InputValue value)
	{
		moveInput = value.Get<Vector2>();
	}

	public void OnJump(InputValue value)
	{
		if (value.isPressed && isGrounded)
		{
			rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
			Jumped?.Invoke(); // ジャンプ開始をアニメ側へ通知
		}
	}

	// --- 移動 ---

	private void Move()
	{
		// カメラ基準の移動方向（水平面）
		Vector3 forward = cameraTransform.forward;
		Vector3 right = cameraTransform.right;
		forward.y = 0f;
		right.y = 0f;
		forward.Normalize();
		right.Normalize();

		Vector3 moveDir = forward * moveInput.y + right * moveInput.x;
		if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

		// --- 向きの制御 ---
		if (IsAiming)
		{
			// エイム中は常にカメラの向き(水平)に体を向ける＝ストレイフ
			if (forward.sqrMagnitude > 0.001f)
			{
				targetForward = forward;
				transform.rotation = Quaternion.LookRotation(forward);
			}
		}
		else
		{
			// 入力がある間は目標方向を更新（操作はこの時点で即時に反映される）。
			// 入力が止んでも targetForward は保持され、見た目は最後の向きへ向き続ける。
			// → チョン押しして離しても、モデルはちゃんとその方向まで振り向く。
			if (IsMoving)
				targetForward = moveDir.normalized;

			Quaternion targetRot = Quaternion.LookRotation(targetForward);
			transform.rotation = Quaternion.RotateTowards(
				transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
		}

		// --- 速度 ---
		if (!IsMoving)
		{
			// 入力が無いときは水平速度を止める（滑り防止）
			rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
			currentMoveSpeed = moveSpeed; // 停止中は基準速度に戻す(再開時に最高速から始まらない)
			return;
		}

		// 目標速度へだんだん加速/減速（ダッシュON/OFFが急にならない）
		float targetSpeed = isDashing ? dashSpeed : moveSpeed;
		currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, targetSpeed, acceleration * Time.deltaTime);

		rb.linearVelocity = new Vector3(
			moveDir.x * currentMoveSpeed,
			rb.linearVelocity.y,
			moveDir.z * currentMoveSpeed
		);
	}

	private bool CheckGrounded()
	{
		// 足元に短いRayを飛ばして地面判定
		return Physics.Raycast(transform.position, Vector3.down, 1.1f, groundLayer);
	}
}
