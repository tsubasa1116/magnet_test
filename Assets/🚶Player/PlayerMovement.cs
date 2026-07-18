using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
	[Header("移動設定")]
	[SerializeField] private float moveSpeed = 5f;
	[SerializeField] private float dashSpeed = 12f;
	[Tooltip("スティックをこの倒し具合以上でダッシュ、未満で歩き(ダッシュボタンは廃止)")]
	[SerializeField, Range(0f, 1f)] private float dashInputThreshold = 0.85f;
	[Tooltip("見た目の向きが進行方向へ追従する速さ(度/秒)。小さいほどゆっくり振り向く。移動方向自体は即時")]
	[SerializeField] private float rotationSpeed = 480f;
	[Tooltip("目標速度に達するまでの加速度(/秒)。小さいほどダッシュがじわっと速くなる")]
	[SerializeField] private float acceleration = 20f;
	[Tooltip("空中での加速度(m/s²)。小さいほど空中でキー入力が効きにくく、ジャンプ軌道や吹き飛びが保たれる")]
	[SerializeField] private float airAcceleration = 8f;
	[Tooltip("被弾後(無敵時間中)の空中操作の効き倍率。吹き飛び軌道を守るため小さめに")]
	[SerializeField, Range(0f, 1f)] private float knockbackAirControlScale = 0.25f;
	[Tooltip("被弾後(無敵時間中)の地上の加速度(m/s²)。小さいほど吹き飛びの慣性が残る")]
	[SerializeField] private float knockbackGroundAcceleration = 12f;

	[Header("ジャンプ設定")]
	[SerializeField] private float jumpForce = 7f;
	[SerializeField] private LayerMask groundLayer;
	[Tooltip("1=通常の重力。小さいほどふわっと(滞空が伸びる)、大きいほどズシッと")]
	[SerializeField] private float gravityScale = 0.6f;
	[Tooltip("入力に対し実速度がこの割合未満なら『壁で止められている』とみなす(Idle表示用)")]
	[SerializeField, Range(0f, 1f)] private float blockedRatio = 0.3f;

	[Header("段差")]
	[Tooltip("この高さまでの段差はジャンプ不要でスムーズに乗り越えられる")]
	[SerializeField] private float stepHeight = 0.35f;
	[Tooltip("段差検知の前方距離")]
	[SerializeField] private float stepCheckDistance = 0.45f;
	[Tooltip("段差を乗り越える時の持ち上げ速度(1物理ステップあたりm)")]
	[SerializeField] private float stepLift = 0.08f;

	private Rigidbody rb;
	private Vector2 moveInput;
	private bool isDashing;
	private bool isGrounded;
	private bool isBlocked;
	private Transform cameraTransform;
	private PlayerHealth health;
	private PlayerCatch catchState;
	private Vector3 targetForward; // 見た目の向きの目標。入力が止んでも保持してそこへ向き続ける
	private float currentMoveSpeed; // 実際に適用中の移動速度(加速のため保持)
	private bool jumpConsumed; // ジャンプ連打による2段ジャンプ防止(着地するまでtrue)
	private float lastJumpTime; // AddForceが速度に反映されるまでの間にフラグが回復しないように
	private Collider mainCollider; // 足元位置(bounds.min.y)の基準に使う
	private float lastStepClimbTime; // 段差乗り越え直後の打ち上がり防止用

    private bool isExternalForce;

    // --- アニメーション側(AnimationStateController)が参照する状態フラグ ---
    // 「どう動いているか」はこの行動コードが持ち、見た目の制御はAnimation側に任せる
    public Vector2 MoveInput => moveInput;
	public bool IsMoving => moveInput.sqrMagnitude > 0.01f;
	public bool IsRunning => isDashing && IsMoving;
	public bool IsGrounded => isGrounded;
	// 入力はあるが壁などで実際に進めていない状態（アニメをIdleにするのに使う）
	public bool IsBlocked => isBlocked;
	// ロープウェイ等に吸着中（外部ギミックが制御するので移動を止める）
	public bool IsOnRopeway { get; set; }

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

		// 壁に押し付けても張り付かないよう、プレイヤーのコライダーは摩擦ゼロにする。
		// (Minimum合成なので相手の壁マテリアルに関係なく摩擦0が適用され、必ず滑り落ちる)
		var slick = new PhysicsMaterial("PlayerNoFriction")
		{
			dynamicFriction = 0f,
			staticFriction = 0f,
			bounciness = 0f,
			frictionCombine = PhysicsMaterialCombine.Minimum,
			bounceCombine = PhysicsMaterialCombine.Minimum,
		};
		foreach (Collider col in GetComponents<Collider>())
			if (!col.isTrigger)
			{
				col.material = slick;
				if (mainCollider == null) mainCollider = col;
			}

		catchState = GetComponent<PlayerCatch>();
		health = GetComponent<PlayerHealth>();

		Vector3 f = transform.forward;
		f.y = 0f;
		targetForward = f.sqrMagnitude > 0.001f ? f.normalized : Vector3.forward;
		currentMoveSpeed = moveSpeed;
	}

	// Catch中か（ZRホールド中。体の向きと catchストレイフアニメに使う）
	public bool IsCatching => catchState != null && catchState.IsCatching;

	void FixedUpdate()
	{
		// 死亡中は移動・回転を一切しない（ラグドール物理に任せる）
		if (health != null && health.IsDead)
		{
			isDashing = false;
			isBlocked = false;
			return;
		}

		// ロープウェイ等に吸着中はギミック側が位置を制御するので、こちらは動かさない
		if (IsOnRopeway)
		{
			rb.linearVelocity = Vector3.zero;
			isDashing = false;
			isBlocked = false;
			return;
		}

		// スティックの倒し具合でダッシュ判定(深く倒す=ダッシュ、浅い=歩き)
		isDashing = moveInput.magnitude >= dashInputThreshold;
		isGrounded = CheckGrounded();

		// 接地していて上下速度がほぼゼロ=本当に着地している時だけジャンプ権を回復。
		// (接地Rayは足が着く少し前からtrueになるため、落下中(速度が大きい)は回復させない)
		// AddForceの反映は次の物理ステップなので、ジャンプ直後0.2秒も回復させない
		if (isGrounded && Mathf.Abs(rb.linearVelocity.y) < 0.1f && Time.time - lastJumpTime > 0.2f)
			jumpConsumed = false;

		// Move() が velocity を上書きする前に、前ステップで物理解決された実速度を測る。
		// 入力があるのに実速度が極端に小さい＝壁などで止められている。
		Vector3 v = rb.linearVelocity;
		v.y = 0f;
		float desired = (isDashing ? dashSpeed : moveSpeed) * Mathf.Clamp01(moveInput.magnitude);
		isBlocked = IsMoving && desired > 0.01f && v.magnitude < desired * blockedRatio;

        if (!IsOnRopeway)
        {
            Move();
        }
        
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
		// jumpConsumed: 離陸直後は接地判定がまだtrueのため、連打で2段ジャンプになるのを防ぐ
		if (value.isPressed && isGrounded && !jumpConsumed)
		{
			jumpConsumed = true;
			lastJumpTime = Time.time;
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
		if (IsCatching)
		{
			// Catch中は常にカメラの向き(水平)に体を向ける＝ストレイフ
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
			// ※カットシーン等でカメラが真上/真下を向くと moveDir がゼロになるため、
			//   ゼロベクトルを弾く(LookRotation viewing vector is zero エラーの防止)
			if (IsMoving && moveDir.sqrMagnitude > 0.0001f)
				targetForward = moveDir.normalized;

			Quaternion targetRot = Quaternion.LookRotation(targetForward);
			transform.rotation = Quaternion.RotateTowards(
				transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
		}

		// --- 速度 ---
		if (!IsMoving)
		{
			// 入力が無いときは地上でのみ水平速度を止める(摩擦ゼロなので必須。空中は勢いを残す)
			if (!isExternalForce && isGrounded)
			{
				if (health != null && health.IsInvincible)
				{
					// 被弾直後は慣性を残す(即停止せず徐々に減速)
					Vector3 v2 = rb.linearVelocity;
					Vector3 horiz2 = Vector3.MoveTowards(
						new Vector3(v2.x, 0f, v2.z), Vector3.zero,
						knockbackGroundAcceleration * Time.fixedDeltaTime);
					rb.linearVelocity = new Vector3(horiz2.x, v2.y, horiz2.z);
				}
				else
				{
					rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
				}
			}
            currentMoveSpeed = moveSpeed; // 停止中は基準速度に戻す(再開時に最高速から始まらない)
			return;
		}

		// 目標速度へだんだん加速/減速（ダッシュON/OFFが急にならない）
		float targetSpeed = isDashing ? dashSpeed : moveSpeed;
		currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, targetSpeed, acceleration * Time.deltaTime);
      
		if (!isExternalForce)
        {
			if (isGrounded)
			{
				// 段差乗り越えの直後は、角を駆け上がった上向き速度を殺す
				// (ダッシュで段に乗り上げた勢いで斜め上に打ち上がるのを防ぐ。ジャンプ直後は対象外)
				float vy = rb.linearVelocity.y;
				if (vy > 0f
					&& Time.time - lastStepClimbTime < 0.15f
					&& Time.time - lastJumpTime > 0.3f)
				{
					vy = 0f;
				}

				if (health != null && health.IsInvincible)
				{
					// 被弾直後(無敵中)は速度を直接書き換えず加速度で寄せる。
					// 吹き飛びの慣性が残り、入力方向へカクンと即転換しない
					Vector3 v = rb.linearVelocity;
					Vector3 horiz = Vector3.MoveTowards(
						new Vector3(v.x, 0f, v.z), moveDir * currentMoveSpeed,
						knockbackGroundAcceleration * Time.fixedDeltaTime);
					rb.linearVelocity = new Vector3(horiz.x, vy, horiz.z);
				}
				else
				{
					rb.linearVelocity = new Vector3(
						moveDir.x * currentMoveSpeed,
						vy,
						moveDir.z * currentMoveSpeed
					);
				}

				// 小さな段差はジャンプ不要で乗り越える
				StepClimb(moveDir);
			}
			else
			{
				// 空中: 壁・角に向かう入力成分を落とす(張り付き防止)。
				// 細いRayだと角をすり抜けて検知できないためSphereCastで判定し、
				// 角(2面)では1回目の投影後に残った成分をもう一度投影して落とす
				for (int i = 0; i < 2 && moveDir.sqrMagnitude > 0.001f; i++)
				{
					if (!Physics.SphereCast(transform.position + Vector3.up * 0.5f, 0.3f,
						moveDir.normalized, out RaycastHit wall, 0.6f, ~0, QueryTriggerInteraction.Ignore)
						|| wall.collider.transform.IsChildOf(transform))
						break;
					moveDir = Vector3.ProjectOnPlane(moveDir, wall.normal);
					moveDir.y = 0f;
				}

				// 空中: 速度を直接書き換えず、弱い加速度で寄せる(空中制御を効きにくくする)。
				// 被弾直後(無敵時間中)はさらに効きを下げて、吹き飛びの軌道を守る
				float accel = airAcceleration;
				if (health != null && health.IsInvincible) accel *= knockbackAirControlScale;

				Vector3 v = rb.linearVelocity;
				Vector3 horizontal = new Vector3(v.x, 0f, v.z);
				horizontal = Vector3.MoveTowards(
					horizontal, moveDir * currentMoveSpeed, accel * Time.fixedDeltaTime);
				rb.linearVelocity = new Vector3(horizontal.x, v.y, horizontal.z);
			}
        }
    }

	private bool CheckGrounded()
	{
		// 1本の細いRayだと床タイルの継ぎ目・コライダーの隙間の真上で空中判定になるため、
		// 体の幅ぶんの太さを持ったSphereCastで判定する(到達距離はRay1.1mと同等)
		foreach (RaycastHit h in Physics.SphereCastAll(
			transform.position, 0.25f, Vector3.down, 0.85f, groundLayer, QueryTriggerInteraction.Ignore))
		{
			if (!h.collider.transform.IsChildOf(transform)) return true;
		}
		return false;
	}

	// 小さな段差の乗り越え補助。
	// 足元の高さでは前方が塞がっていて、段差の高さ(stepHeight)より上では空いている場合、
	// 段の上面の高さを測り、「足りないぶんだけ」持ち上げる(上げすぎて浮かないように)。
	private void StepClimb(Vector3 moveDir)
	{
		if (moveDir.sqrMagnitude < 0.001f || mainCollider == null) return;

		Vector3 dir = moveDir.normalized;
		float feetY = mainCollider.bounds.min.y; // 現在の実際の足元の高さ
		Vector3 feet = new Vector3(transform.position.x, feetY + 0.05f, transform.position.z);

		// 足元の高さで前方が塞がっている?
		if (!Physics.Raycast(feet, dir, out RaycastHit low, stepCheckDistance, ~0, QueryTriggerInteraction.Ignore))
			return;
		if (low.collider.transform.IsChildOf(transform)) return;
		// ほぼ垂直な面(=段差/壁)だけを対象にする(坂は物理に任せる)
		if (Mathf.Abs(low.normal.y) > 0.3f) return;

		// 段差の高さより上は空いている?(空いていれば乗り越えられる低い段差)
		bool highBlocked = Physics.Raycast(feet + Vector3.up * stepHeight, dir,
			stepCheckDistance + 0.1f, ~0, QueryTriggerInteraction.Ignore);
		if (highBlocked) return;

		// 段の上面の高さを測る
		Vector3 probe = feet + Vector3.up * stepHeight + dir * (low.distance + 0.15f);
		if (!Physics.Raycast(probe, Vector3.down, out RaycastHit top, stepHeight + 0.1f, ~0, QueryTriggerInteraction.Ignore))
			return;

		// 足りない高さだけ持ち上げる(登り切ったら自動的に0になる=浮かない)
		float needed = top.point.y - feetY;
		if (needed <= 0.005f) return;
		rb.MovePosition(rb.position + Vector3.up * Mathf.Min(stepLift, needed + 0.02f));
		lastStepClimbTime = Time.time; // 直後の上向き速度クランプ用
	}

    public void StartExternalForce(float duration)
    {
        StartCoroutine(ExternalForceCoroutine(duration));
    }

    private IEnumerator ExternalForceCoroutine(float duration)
    {
        isExternalForce = true;

        yield return new WaitForSeconds(duration);

        isExternalForce = false;
    }
}
