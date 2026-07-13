using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// プレイヤーの各種エフェクトを、行動コードのイベント/状態から生成する専任クラス。
//   ダッシュ中: runEffect を出しっぱなし、終了で少し遅れて消す
//   極切替: N/S それぞれのエフェクト
//   被弾 / 死亡: hit / down エフェクト
//   被弾時: 体を一瞬赤く染め、無敵時間中は点滅させる
public class PlayerEffects : MonoBehaviour
{
    // エフェクトの生成位置。未指定ならプレイヤーの足元
    [SerializeField] private Transform poleEffectPoint;

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

	[Header("被弾フラッシュ / 無敵点滅")]
	[Tooltip("被弾した瞬間に体を染める色")]
	[SerializeField] private Color damageFlashColor = new Color(1f, 0.25f, 0.25f, 1f);
	[Tooltip("赤く染める時間(実時間秒。ヒットストップ中も維持される)")]
	[SerializeField] private float damageFlashTime = 0.15f;
	[Tooltip("無敵時間中の点滅間隔(秒)")]
	[SerializeField] private float blinkInterval = 0.08f;

    private PlayerMovement movement;
	private PlayerStateMachine stateMachine;
	private PlayerHealth health;

	private GameObject currentRunEffect;
	private float runStopTimer;
	private SkinnedMeshRenderer[] bodyRenderers; // 点滅/赤フラッシュ対象(体のメッシュのみ。掴んだ物等は含めない)
	private Coroutine damageReaction;

	void Awake()
	{
		movement = GetComponent<PlayerMovement>();
		stateMachine = GetComponent<PlayerStateMachine>();
		health = GetComponent<PlayerHealth>();
		bodyRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
	}

	void OnEnable()
	{
		if (stateMachine != null) stateMachine.OnStateChanged += OnPoleChanged;
		if (health != null)
		{
			health.OnHit += OnHit;
			health.OnDied += OnDied;
		}
	}

	void OnDisable()
	{
		if (stateMachine != null) stateMachine.OnStateChanged -= OnPoleChanged;
		if (health != null)
		{
			health.OnHit -= OnHit;
			health.OnDied -= OnDied;
		}
		// 点滅の途中で無効化されても、体が消えたままにならないようにする
		SetBodyVisible(true);
	}

	void Update()
	{
		HandleRunEffect();
	}

    private void HandleRunEffect()
    {
        if (currentRunEffect != null)
            StopRunEffect();

        if (health != null && health.IsDead)
        {
            if (currentRunEffect != null)
                StopRunEffect();
            return;
        }

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
            if (runStopTimer >= runStopDelay)
                StopRunEffect();
        }
    }

    private void StopRunEffect()
    {
        if (currentRunEffect == null)
            return;

        currentRunEffect.transform.SetParent(null);

        var ps = currentRunEffect.GetComponent<ParticleSystem>();
        if (ps != null)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        Destroy(currentRunEffect, 2f);
        currentRunEffect = null;
    }

    private void OnPoleChanged(MagnetState s)
	{
		Spawn(s == MagnetState.N ? nPoleChangeEffect : sPoleChangeEffect, poleEffectLife);
	}

    private void OnHit()
	{
		Spawn(hitEffect, hitEffectLife);

		// 赤フラッシュ → 無敵時間が終わるまで点滅
		if (damageReaction != null) StopCoroutine(damageReaction);
		damageReaction = StartCoroutine(DamageReaction());
	}

    private void OnDied()
    {
        StopRunEffect();                  // ダッシュエフェクトが残っていれば消す
        Spawn(downEffect, hitEffectLife, true);
    }

    // 被弾リアクション: 一瞬赤く染める → 無敵時間中は点滅して「今は無敵」と分かるようにする
    private IEnumerator DamageReaction()
	{
		// ① 赤フラッシュ(元の色を退避してから染める)
		var originals = new List<(Material mat, string prop, Color color)>();
		foreach (var r in bodyRenderers)
		{
			if (r == null) continue;
			foreach (var mat in r.materials)
			{
				string prop = mat.HasProperty("_BaseColor") ? "_BaseColor"
					: mat.HasProperty("_Color") ? "_Color" : null;
				if (prop == null) continue;
				originals.Add((mat, prop, mat.GetColor(prop)));
				mat.SetColor(prop, damageFlashColor);
			}
		}

		// ヒットストップ(timeScale=0)中も赤が見えるように実時間で待つ
		yield return new WaitForSecondsRealtime(damageFlashTime);

		foreach (var (mat, prop, color) in originals)
			if (mat != null) mat.SetColor(prop, color);

		// ② 無敵時間中の点滅
		bool visible = true;
		while (health != null && health.IsInvincible && !health.IsDead)
		{
			visible = !visible;
			SetBodyVisible(visible);
			yield return new WaitForSeconds(blinkInterval);
		}

		SetBodyVisible(true);
		damageReaction = null;
	}

	private void SetBodyVisible(bool visible)
	{
		if (bodyRenderers == null) return;
		foreach (var r in bodyRenderers)
			if (r != null) r.enabled = visible;
	}

    private void Spawn(GameObject prefab, float life, bool allowWhenDead = false)
    {
        if (prefab == null) return;

        // 死亡中はダウンエフェクト以外は生成しない
        if (!allowWhenDead && health != null && health.IsDead)
            return;

        Transform point = poleEffectPoint != null ? poleEffectPoint : transform;

        GameObject fx = Instantiate(prefab, point.position, point.rotation);

        fx.transform.SetParent(point, true);

        if (life > 0f)
            Destroy(fx, life);
    }
}
