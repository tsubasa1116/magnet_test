using UnityEngine;
using UnityEngine.UIElements;

// プレイヤーの体力。0になったらラグドール化(操作不能・物理で倒れる)。
// 被弾時はノックバック + ヒットストップ + 短い無敵時間(点滅)が発生する。
[RequireComponent(typeof(PlayerRagdoll))]
public class PlayerHealth : MonoBehaviour
{
	[SerializeField] private int maxHp = 100;

	[Header("被弾リアクション")]
	[Tooltip("ノックバックの初速(m/s)。攻撃してきた相手から離れる方向へ飛ぶ")]
	[SerializeField] private float knockbackSpeed = 6f;
	[Tooltip("ノックバックの上向き初速(m/s)。少し浮かせると吹き飛び感が出る")]
	[SerializeField] private float knockbackUpSpeed = 2.5f;
	[Tooltip("ノックバック中(移動入力を受け付けない)時間")]
	[SerializeField] private float knockbackTime = 0.25f;
	[Tooltip("被弾後の無敵時間。この間はダメージを受けず、体が点滅する")]
	[SerializeField] private float invincibleTime = 1.2f;
	[Tooltip("被弾時のヒットストップ(実時間秒)")]
	[SerializeField] private float hitStopTime = 0.12f;

	public int Hp { get; private set; }
	public bool IsDead { get; private set; }
	public bool IsInvincible => Time.time < invincibleUntil;
	public bool InKnockback => Time.time < knockbackUntil;

	// エフェクト等が購読する（被弾・死亡の通知）
	public event System.Action OnDamaged; // HPが変化した(Revive含む。UI更新用)
	public event System.Action OnHit;     // 実際にダメージを受けた瞬間だけ(演出用)
	public event System.Action OnDied;

	private PlayerRagdoll ragdoll;
	private Rigidbody rb;
	private PlayerMovement movement;
	private float invincibleUntil;
	private float knockbackUntil;

	void Awake()
	{
		Hp = maxHp;
		ragdoll = GetComponent<PlayerRagdoll>();
		rb = GetComponent<Rigidbody>();
		movement = GetComponent<PlayerMovement>();
	}

	void Update()
	{
		// デバッグ用: I キーで即死
		if (Input.GetKeyDown(KeyCode.I)) TakeDamage(maxHp);
	}

	// 敵などから呼ぶ。攻撃元の位置が不明な場合はこちら(正面から殴られた扱いで後方へ下がる)
	public void TakeDamage(int amount)
		=> TakeDamage(amount, transform.position + transform.forward);

	// 攻撃元の位置つき。ノックバック方向が「攻撃元から離れる向き」になる
	public void TakeDamage(int amount, Vector3 sourcePosition)
	{
        if (IsDead || IsInvincible) return;

		Hp = Mathf.Max(0, Hp - amount);

		invincibleUntil = Time.time + invincibleTime; // 無敵開始(点滅はPlayerEffectsが行う)
		ApplyKnockback(sourcePosition);
		HitStop.Play(hitStopTime);

        OnDamaged?.Invoke();
		OnHit?.Invoke();
		if (Hp == 0) Die();
	}

	// 攻撃元から離れる方向へ吹き飛ばす。ノックバック中は PlayerMovement が入力を無視する
	private void ApplyKnockback(Vector3 sourcePosition)
	{
		if (rb == null) return;

		Vector3 dir = transform.position - sourcePosition;
		dir.y = 0f;
		dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : -transform.forward;

		rb.linearVelocity = dir * knockbackSpeed + Vector3.up * knockbackUpSpeed;
		knockbackUntil = Time.time + knockbackTime;

		// ノックバック中は移動入力で速度を上書きさせない(PlayerMovementの外力機構を利用)
		if (movement != null) movement.StartExternalForce(knockbackTime);
	}

	private void Die()
	{
		IsDead = true;
		OnDied?.Invoke();
		ragdoll.EnableRagdoll();
    }

    public void Revive()
    {
        Hp = maxHp;
        IsDead = false;

        OnDamaged?.Invoke();
    }

    // 動作確認用：Inspectorの「⋮」から実行できる
    [ContextMenu("Debug: Kill")]
	private void DebugKill() => TakeDamage(maxHp);
}
