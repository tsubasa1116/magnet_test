using UnityEngine;
using UnityEngine.UIElements;

// プレイヤーの体力。0になったらラグドール化(操作不能・物理で倒れる)。
[RequireComponent(typeof(PlayerRagdoll))]
public class PlayerHealth : MonoBehaviour
{
	[SerializeField] private int maxHp = 100;

	public int Hp { get; private set; }
	public bool IsDead { get; private set; }

	// エフェクト等が購読する（被弾・死亡の通知）
	public event System.Action OnDamaged;
	public event System.Action OnDied;

	private PlayerRagdoll ragdoll;

	void Awake()
	{
		Hp = maxHp;
		ragdoll = GetComponent<PlayerRagdoll>();
	}

	void Update()
	{
		// デバッグ用: I キーで即死
		if (Input.GetKeyDown(KeyCode.I)) TakeDamage(maxHp);
	}

	// 敵などから呼ぶ
	public void TakeDamage(int amount)
	{
		if (IsDead) return;

		Hp = Mathf.Max(0, Hp - amount);
		OnDamaged?.Invoke();
		if (Hp == 0) Die();
	}

	private void Die()
	{
		IsDead = true;
		OnDied?.Invoke();
		ragdoll.EnableRagdoll();
	}

	// 動作確認用：Inspectorの「⋮」から実行できる
	[ContextMenu("Debug: Kill")]
	private void DebugKill() => TakeDamage(maxHp);
}
