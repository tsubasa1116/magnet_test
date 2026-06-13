using UnityEngine;

// プレイヤーのアニメーションをまとめて制御するクラス。
// 「今どう動いているか(フラグ・値)」は PlayerMovement などの行動コードが持ち、
// このクラスは "そのフラグを受けて実際にどうアニメさせるか" だけを担当する。
//
// 移動アニメ:
//   Velocity Z … Walk(0) ↔ Run(1) のブレンド。ダッシュ中に Run へ寄せる。
//   MoveSpeed  … スティックの倒し具合(0〜1)。Idle⇔移動の判定と、
//                BlendTree の Speed Multiplier(再生速度)に使う。
//                → ゆっくり倒すとゆっくり再生、Maxまで倒すと通常再生。
[RequireComponent(typeof(Animator))]
public class AnimationStateController : MonoBehaviour
{
	[Header("なめらかさ(大きいほど機敏)")]
	[SerializeField] private float blendDamping = 0.01f; // Walk↔Run 切替
	[SerializeField] private float speedDamping = 0.08f; // 倒し具合の変化

	[Header("エイム(ストレイフ)設定")]
	[SerializeField] private int aimLayerIndex = 1;   // 上半身エイムレイヤーの番号
	[SerializeField] private float aimDamping = 6f;   // 前後左右ブレンドの追従
	[SerializeField] private float aimWeightSpeed = 6f; // 上半身レイヤーの出入り

	private Animator animator;
	private PlayerMovement movement;

	private float velocityZ; // 0=Walk, 1=Run
	private float moveSpeed;  // 0〜1 スティックの倒し具合
	private float aimX, aimZ; // エイム時のストレイフ方向(-1〜1)
	private float aimWeight;  // 上半身エイムレイヤーのウェイト

	private static readonly int VelocityZHash = Animator.StringToHash("Velocity Z");
	private static readonly int VelocityXHash = Animator.StringToHash("Velocity X");
	private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
	private static readonly int JumpHash = Animator.StringToHash("Jump");
	private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
	private static readonly int IsAimingHash = Animator.StringToHash("IsAiming");

	void Start()
	{
		animator = GetComponent<Animator>();
		movement = GetComponent<PlayerMovement>();

		// ジャンプした瞬間にトリガーを立てる
		movement.Jumped += OnJumped;
	}

	void OnDestroy()
	{
		if (movement != null) movement.Jumped -= OnJumped;
	}

	void Update()
	{
		bool aiming = movement.IsAiming;
		animator.SetBool(IsAimingHash, aiming);

		// 上半身エイムレイヤーのウェイトをなめらかに出し入れ
		// (レイヤー未作成でもエラーにならないようガード)
		aimWeight = Mathf.MoveTowards(aimWeight, aiming ? 1f : 0f, aimWeightSpeed * Time.deltaTime);
		if (aimLayerIndex > 0 && aimLayerIndex < animator.layerCount)
			animator.SetLayerWeight(aimLayerIndex, aimWeight);

		if (aiming)
		{
			// 体はカメラを向いている＝入力がそのままローカルのストレイフ方向
			// 壁で止められている時は入力ゼロ扱い→中央(IdolCatch)になる
			Vector2 mv = movement.IsBlocked ? Vector2.zero : movement.MoveInput;
			aimX = Mathf.MoveTowards(aimX, mv.x, aimDamping * Time.deltaTime);
			aimZ = Mathf.MoveTowards(aimZ, mv.y, aimDamping * Time.deltaTime);

			animator.SetFloat(VelocityXHash, aimX);
			animator.SetFloat(VelocityZHash, aimZ);
			animator.SetFloat(MoveSpeedHash, 1f); // エイム時は等速再生
		}
		else
		{
			// 通常移動: 倒し具合で再生速度、Velocity Z で Walk↔Run
			// 壁で止められている時は Idle 扱い(tilt=0)
			float tilt = movement.IsBlocked ? 0f : Mathf.Clamp01(movement.MoveInput.magnitude);
			float targetVelocityZ = (movement.IsRunning && !movement.IsBlocked) ? 1f : 0f;

			moveSpeed = Mathf.MoveTowards(moveSpeed, tilt, speedDamping * Time.deltaTime);
			velocityZ = Mathf.MoveTowards(velocityZ, targetVelocityZ, blendDamping * Time.deltaTime);

			animator.SetFloat(MoveSpeedHash, moveSpeed);
			animator.SetFloat(VelocityXHash, 0f);
			animator.SetFloat(VelocityZHash, velocityZ);

			aimX = aimZ = 0f;
		}

		// 接地状態を渡す（着地でJumpEnd→Idleへ戻すのに使う）
		animator.SetBool(IsGroundedHash, movement.IsGrounded);
	}

	private void OnJumped()
	{
		animator.SetTrigger(JumpHash);
	}
}
