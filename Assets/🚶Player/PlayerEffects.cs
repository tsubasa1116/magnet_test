using UnityEngine;

// プレイヤーの各種エフェクトを、行動コードのイベント/状態から生成する専任クラス。
//   ダッシュ中: runEffect を出しっぱなし、終了で少し遅れて消す
//   極切替: N/S それぞれのエフェクト
//   被弾 / 死亡: hit / down エフェクト
public class PlayerEffects : MonoBehaviour
{
	[Header("ダッシュ")]
	[SerializeField] private GameObject runEffect;
	[SerializeField] private float runStopDelay = 0.5f;

	[Header("極切替")]
	[SerializeField] private GameObject nPoleChangeEffect;
	[SerializeField] private GameObject sPoleChangeEffect;
	[SerializeField] private float poleEffectLife = 1.5f;

	[Header("被弾 / 死亡")]
	[SerializeField] private GameObject hitEffect;
	[SerializeField] private GameObject downEffect;
	[SerializeField] private float hitEffectLife = 2f;

	private PlayerMovement movement;
	private PlayerStateMachine stateMachine;
	private PlayerHealth health;

	private GameObject currentRunEffect;
	private float runStopTimer;

	void Awake()
	{
		movement = GetComponent<PlayerMovement>();
		stateMachine = GetComponent<PlayerStateMachine>();
		health = GetComponent<PlayerHealth>();
	}

	void OnEnable()
	{
		if (stateMachine != null) stateMachine.OnStateChanged += OnPoleChanged;
		if (health != null)
		{
			health.OnDamaged += OnDamaged;
			health.OnDied += OnDied;
		}
	}

	void OnDisable()
	{
		if (stateMachine != null) stateMachine.OnStateChanged -= OnPoleChanged;
		if (health != null)
		{
			health.OnDamaged -= OnDamaged;
			health.OnDied -= OnDied;
		}
	}

	void Update()
	{
		HandleRunEffect();
	}

	private void HandleRunEffect()
	{
		bool running = movement != null && movement.IsRunning;

		if (running)
		{
			runStopTimer = 0f;
			if (currentRunEffect == null && runEffect != null)
			{
				currentRunEffect = Instantiate(runEffect, transform.position, Quaternion.identity, transform);
				currentRunEffect.transform.localPosition = Vector3.zero;
			}
		}
		else if (currentRunEffect != null)
		{
			runStopTimer += Time.deltaTime;
			if (runStopTimer >= runStopDelay) StopRunEffect();
		}
	}

	private void StopRunEffect()
	{
		currentRunEffect.transform.SetParent(null);
		var ps = currentRunEffect.GetComponent<ParticleSystem>();
		if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
		Destroy(currentRunEffect, 2f);
		currentRunEffect = null;
	}

	private void OnPoleChanged(MagnetState s)
	{
		Spawn(s == MagnetState.N ? nPoleChangeEffect : sPoleChangeEffect, poleEffectLife);
	}

	private void OnDamaged() => Spawn(hitEffect, hitEffectLife);
	private void OnDied() => Spawn(downEffect, hitEffectLife);

	private void Spawn(GameObject prefab, float life)
	{
		if (prefab == null) return;
		GameObject fx = Instantiate(prefab, transform.position, Quaternion.identity);
		if (life > 0f) Destroy(fx, life);
	}
}
