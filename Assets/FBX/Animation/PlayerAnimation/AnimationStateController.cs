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
	[SerializeField] private int aimLayerIndex = 1;   // 腕キャッチレイヤー(CatchHold)の番号
	[SerializeField] private float aimDamping = 6f;   // 前後左右ブレンドの追従
	[SerializeField] private float aimWeightSpeed = 6f; // 腕レイヤーの出入り

	[Header("引き寄せ(MagnetPull連携)")]
	[Tooltip("引き寄せ中(対象が飛んでくる間)の足アニメの再生速度倍率")]
	[SerializeField] private float pullFootSpeed = 2.2f;

	[Header("被弾")]
	[Tooltip("被弾アニメ(HitLittle)を優先する時間。この間はAim/Holdへ引き戻さない")]
	[SerializeField] private float hitAnimTime = 0.45f;

	private Animator animator;
	private PlayerMovement movement;
	private MagnetPull magnetPull;
	private PlayerHealth health;
	private float hitAnimUntil; // 被弾アニメを優先している間の終了時刻
	private float lastGroundedTime; // 接地Rayの瞬断でアニメがバタつかないための猶予用

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
	private static readonly int IsHoldingHash = Animator.StringToHash("IsHolding");
	private static readonly int HitHash = Animator.StringToHash("Hit");

	void Start()
	{
		animator = GetComponent<Animator>();
		movement = GetComponent<PlayerMovement>();
		magnetPull = GetComponent<MagnetPull>();
		health = GetComponent<PlayerHealth>();

		// ジャンプした瞬間にトリガーを立てる
		movement.Jumped += OnJumped;
		// 被弾した瞬間にHitLittleを再生する
		if (health != null) health.OnHit += OnHitAnim;
	}

	void OnDestroy()
	{
		if (movement != null) movement.Jumped -= OnJumped;
		if (health != null) health.OnHit -= OnHitAnim;
	}

	private void OnHitAnim()
	{
		animator.SetTrigger(HitHash);
		hitAnimUntil = Time.time + hitAnimTime;
	}

	void Update()
	{
		// Catch中(ZRホールド)は体がカメラを向くストレイフ＋catch姿勢(Aimステート)。
		// 引き寄せ完了(保持中)の地上は専用の保持移動(Holdステート: CatchRun系ブレンド)。
		// 空中でのキャッチだけは体を空中アニメのままにして、
		// 腕だけ CatchHold レイヤー(アバターマスク)でキャッチ姿勢を出す(交互切り替わりのチラつき防止)。
		bool catching = movement.IsCatching;
		bool pulling = magnetPull != null && magnetPull.IsPulling;
		bool holding = magnetPull != null && magnetPull.IsHolding;

		// 接地Rayが歩行中に一瞬切れてもステートが行き来しないよう、0.15秒の猶予を持たせる
		// (これが無いと Hold/Aim と通常移動が細かく行き来してアニメが混ざって見える)
		if (movement.IsGrounded) lastGroundedTime = Time.time;
		bool grounded = movement.IsGrounded || Time.time - lastGroundedTime < 0.15f;
		// 被弾アニメ再生中は Aim/Hold のAnyState遷移に引き戻されないよう一時的にオフにする
		bool inHitAnim = Time.time < hitAnimUntil;
		bool strafing = catching && !holding && grounded && !inHitAnim;
		bool holdingMove = holding && grounded && !inHitAnim;
		// 腕レイヤー(前ならえ姿勢): 空中キャッチ中と、保持中(足はHoldの移動アニメ、上半身は前ならえ)
		bool armPose = catching && (!grounded || holding);
		animator.SetBool(IsAimingHash, strafing);
		animator.SetBool(IsHoldingHash, holdingMove);

		// 腕キャッチレイヤー(空中キャッチ時のみ)のウェイトをなめらかに出し入れ
		// (レイヤー未作成でもエラーにならないようガード)
		aimWeight = Mathf.MoveTowards(aimWeight, armPose ? 1f : 0f, aimWeightSpeed * Time.deltaTime);
		if (aimLayerIndex > 0 && aimLayerIndex < animator.layerCount)
			animator.SetLayerWeight(aimLayerIndex, aimWeight);

		if (strafing || holdingMove)
		{
			// 体はカメラを向いている＝入力がそのままローカルのストレイフ方向
			// 壁で止められている時は入力ゼロ扱い→中央(IdolCatch/CatchMiddle)になる
			Vector2 mv = movement.IsBlocked ? Vector2.zero : movement.MoveInput;
			aimX = Mathf.MoveTowards(aimX, mv.x, aimDamping * Time.deltaTime);
			aimZ = Mathf.MoveTowards(aimZ, mv.y, aimDamping * Time.deltaTime);

			animator.SetFloat(VelocityXHash, aimX);
			animator.SetFloat(VelocityZHash, aimZ);
			// 引き寄せ中は足の運びを速める(catch移動ステートの再生速度に反映)
			animator.SetFloat(MoveSpeedHash, strafing && pulling ? pullFootSpeed : 1f);
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

		// 接地状態を渡す（着地でJumpEnd→Idleへ戻すのに使う）。
		// 生の接地Rayは歩行中に瞬断するため、猶予付きの値を渡す
		// (瞬断すると Hold/Aim→空中ステートへ一瞬飛んでアニメが混ざって見える)
		animator.SetBool(IsGroundedHash, grounded);

#if UNITY_EDITOR
		// ホールド中のアニメ診断: 実際に再生中のステートとクリップ(重み付き)を1秒ごとに出力
		if (holding && Time.frameCount % 60 == 0)
		{
			var st = animator.GetCurrentAnimatorStateInfo(0);
			string stateName =
				st.IsName("Hold") ? "Hold" :
				st.IsName("Aim") ? "Aim" :
				st.IsName("HitLittle_v1") ? "HitLittle" : $"その他({st.shortNameHash})";
			var clipInfos = animator.GetCurrentAnimatorClipInfo(0);
			string clips = string.Join(", ", System.Array.ConvertAll(
				clipInfos, c => $"{c.clip.name}:{c.weight:F2}"));
			float layer1 = animator.layerCount > 1 ? animator.GetLayerWeight(1) : -1f;
			Debug.Log($"[AnimDebug] state={stateName} 遷移中={animator.IsInTransition(0)} "
				+ $"clips=[{clips}] VelX={aimX:F2} VelZ={aimZ:F2} 腕レイヤーweight={layer1:F2}");
		}
#endif
	}

	private void OnJumped()
	{
		animator.SetTrigger(JumpHash);
	}
}
