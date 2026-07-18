using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;

// エイム(ADS)制御。
// カメラは1台の FreeLook のまま、エイム中だけ FOV / 距離 / 肩寄せを補間する。
// カメラを切り替えないので、エイムに入っても "今見ている方向" をそのまま維持できる
// （別カメラへ切り替わる時のような向きのスナップが起きない）。
[RequireComponent(typeof(PlayerInput))]
public class PlayerAim : MonoBehaviour
{
	[Header("対象カメラ(通常時のFreeLookを割り当てる)")]
	[SerializeField] private CinemachineFreeLook freeLook;

	[Header("カメラ基準値(プレイ中の一時値がシーンに保存されて劣化するのを防ぐため、起動時に強制適用)")]
	[Tooltip("通常時のFOV")]
	[SerializeField] private float baseFOV = 50f;
	[Tooltip("カメラ上下(Y軸)の基準速度。上下の視点移動が遅い時はここを上げる")]
	[SerializeField] private float baseYAxisSpeed = 2.5f;
	[Tooltip("通常時の画面横位置(0.5=中央)。昔の肩越しADSの一時値がシーンに残っていても起動時にここへ戻す")]
	[SerializeField] private float baseScreenX = 0.5f;

	// ※ADS(R3でカメラズームエイム)は廃止。
	//   Catch中の常時肩越し・ホールド中の過剰ズーム(FOV28)の原因だったため、
	//   ズームと肩寄せはロックオンだけが持つ。R3はロックオン専用。
	[Header("カメラ補間")]
	[SerializeField] private float lerpSpeed = 10f;        // 寄り/戻りのなめらかさ

	[Header("見上げ設定")]
	[Tooltip("カメラ上下(Y軸 0=下,1=上)がこの値を下回ると、注視点を頭上へ持ち上げてカメラが上を向き始める")]
	[SerializeField] private float lookUpStartY = 0.35f;
	[Tooltip("カメラを一番下まで下げた時の仰角(度)。FreeLookの構造上90ちょうどは不可、80前後が実用上限")]
	[SerializeField] private float maxLookUpAngle = 78f;
	[Tooltip("見上げゾーンでの上下視点速度の倍率(1=通常と同じ、小さいほどゆっくり上を向く)")]
	[SerializeField] private float lookUpSpeedScale = 0.85f;

	[Header("ロックオン設定(R3押し込み)")]
	[Tooltip("ロックオン対象のタグ(吸い寄せオブジェクトと敵)")]
	[SerializeField] private string[] lockOnTags = { "Enemy", "N_Pole", "S_Pole", "N_Enemy", "S_Enemy" };
	[SerializeField] private float lockOnRange = 30f;
	[Tooltip("ロックオン中にカメラが対象へ向く速さ(大きいほどキビキビ)")]
	[SerializeField] private float lockOnYawSpeed = 8f;
	[Tooltip("上下方向の追従の強さ")]
	[SerializeField] private float lockOnPitchGain = 0.06f;
	[Tooltip("右スティックをこの強さ以上に倒すと、倒した方向の別対象へ照準切替")]
	[SerializeField] private float switchThreshold = 0.7f;
	[Tooltip("切替後、スティックがこの値以下に戻るまで次の切替を受け付けない")]
	[SerializeField] private float switchRearmThreshold = 0.3f;
	[Tooltip("ロックオン中のズーム。通常FOVに掛ける倍率(1=変化なし、小さいほどアップ)")]
	[SerializeField] private float lockOnFOVScale = 0.85f;
	[Tooltip("ロックオン中の肩寄せ(0.5=中央, 小さいほど右肩越しになり狙っている対象が見やすい)")]
	[SerializeField] private float lockOnScreenX = 0.35f;

	[Header("遮蔽物の半透明化(ディザ方式)")]
	[Tooltip("カメラとプレイヤーの間に入った物(壁・柱・敵など)を網点状に抜いて半透明に見せる。カメラが寄る挙動(Cinemachine Collider)は無効化される")]
	[SerializeField] private bool fadeObstacles = true;
	[Tooltip("穴の中心(視線上)の不透明度(0=完全に透明、1=不透明)")]
	[SerializeField, Range(0f, 1f)] private float obstacleAlpha = 0.15f;
	[Tooltip("視線(カメラ→プレイヤーの線)からこの距離までは最強で抜ける(穴の半径)")]
	[SerializeField] private float holeRadius = 1.2f;
	[Tooltip("穴の縁のぼかし幅。この幅をかけてなだらかに不透明へ戻る")]
	[SerializeField] private float holeSoftness = 1.5f;
	[Tooltip("ディザがかかるまでの時間(秒)。いきなりではなくじわっとかかる")]
	[SerializeField] private float fadeInTime = 0.2f;
	[Tooltip("ディザが戻るまでの時間(秒)")]
	[SerializeField] private float fadeOutTime = 0.3f;
	[Tooltip("カメラ位置からこの半径内に重なっているコライダーも抜く(巨大な物の中にカメラが入ると、内側からのレイが当たらず素通しになるため)")]
	[SerializeField] private float cameraOverlapRadius = 0.8f;

	[Header("死亡時カメラ(バラバラに飛ぶ頭を追従・ズーム)")]
	[Tooltip("死亡時のズームFOV(小さいほどアップ)")]
	[SerializeField] private float deathFOV = 30f;
	[Tooltip("死亡時のカメラ距離倍率(小さいほど頭に寄る)")]
	[SerializeField, Range(0.1f, 1f)] private float deathRadiusScale = 0.35f;

	[Header("死亡演出(スローモーション)")]
	[Tooltip("死亡時のスローモーション倍率(1=等速)")]
	[SerializeField, Range(0.05f, 1f)] private float deathSlowScale = 0.2f;
	[Tooltip("スローを維持する時間(実時間の秒)")]
	[SerializeField] private float deathSlowHold = 1.2f;
	[Tooltip("スローから等速へなめらかに戻す時間(実時間の秒)")]
	[SerializeField] private float deathSlowRecover = 1.5f;

	[Header("カーソル")]
	[Tooltip("プレイ中はマウスカーソルを隠して画面中央にロックする")]
	[SerializeField] private bool hideCursor = true;

	[Header("ロックオンマーカー")]
	[Tooltip("独自デザインを使う場合に指定。未指定ならコードが四隅カギカッコ枠を自動生成する")]
	[SerializeField] private GameObject markerPrefab;
	[Tooltip("対象の大きさに対する枠の余白倍率")]
	[SerializeField] private float markerScale = 1.25f;
	[Tooltip("枠の回転速度(度/秒)")]
	[SerializeField] private float markerSpinSpeed = 120f;
	[SerializeField] private Color enemyMarkerColor = new Color(1f, 0.45f, 0.1f, 0.95f);
	[SerializeField] private Color nPoleMarkerColor = new Color(1f, 0.3f, 0.3f, 0.95f);
	[SerializeField] private Color sPoleMarkerColor = new Color(0.3f, 0.65f, 1f, 0.95f);

	public bool IsLockedOn => locked;
	public Transform LockOnTarget => lockTarget;

	// ロックオン中の照準点(対象コライダーの中心)。未ロック時は false
	public bool TryGetLockOnPoint(out Vector3 point)
	{
		if (locked && lockTarget != null)
		{
			point = LockPoint();
			return true;
		}
		point = Vector3.zero;
		return false;
	}

	private Camera mainCamera;
	private PlayerInput playerInput;
	private InputAction aimAction;
	private InputAction cameraAction;
	private MagnetPull magnetPull;
	private CinemachineInputProvider inputProvider;

	// 遮蔽物フェードの状態(元マテリアルの退避先と、生成した差し替えマテリアル)
	private readonly Dictionary<Renderer, Material[]> fadedRenderers = new Dictionary<Renderer, Material[]>();
	private readonly Dictionary<Renderer, Material[]> fadeInstances = new Dictionary<Renderer, Material[]>();
	private readonly Dictionary<Renderer, float> fadeWeights = new Dictionary<Renderer, float>(); // 0..1の時間フェード量
	private readonly HashSet<Renderer> blockedThisFrame = new HashSet<Renderer>();
	private Shader ditherShader; // ディザ半透明シェーダー(Resources/ObstacleDitherFade)

	// 死亡時カメラ(ラグドールで飛ぶ頭を追従・注視)
	private PlayerHealth health;
	private Transform headBone;
	private Transform originalLookAt;
	private Transform originalFollow;
	private readonly float[] savedOrbitRadii = new float[3];
	private bool deathFocusActive;
	private Coroutine slowMoRoutine;
	private float baseFixedDeltaTime; // スロー中は物理刻みも一緒に縮める(カクつき防止)

	// ロックオン状態
	private bool locked;               // 対象がDestroyされてもロック中と分かるよう明示フラグで持つ
	private Transform lockTarget;
	private Collider lockTargetCol;
	private Transform marker;          // マーカーのルート(カメラへ正対させる)
	private Transform markerSpinner;   // 回転する枠部分(自動生成時のみ)
	private Material markerMat;
	private bool switchArmed;          // スティックが中立へ戻り、次の照準切替を受け付けられるか
	private float normalYMaxSpeed;     // Y軸の通常速度(見上げゾーンで減速するため保持)

	// 通常時の値(復帰用)
	private float normalFOV;
	private float normalScreenX = 0.5f;
	private CinemachineComposer[] composers = new CinemachineComposer[3];

	void Awake()
	{
		mainCamera = Camera.main;
		playerInput = GetComponent<PlayerInput>();
		aimAction = playerInput.actions["Aim"];
		// 押し込み一回でAim ON/OFFをトグル（押しっぱなし不要）
		aimAction.started += OnAimToggle;

		magnetPull = GetComponent<MagnetPull>();
		cameraAction = playerInput.actions["Camera"];
		inputProvider = freeLook != null ? freeLook.GetComponent<CinemachineInputProvider>() : null;

		// 死亡/復活でカメラの注視先を切り替える
		health = GetComponent<PlayerHealth>();
		if (health != null)
		{
			health.OnDied += OnPlayerDied;
			health.OnDamaged += OnPlayerHealthChanged;
		}

		baseFixedDeltaTime = Time.fixedDeltaTime;
	}

	void Start()
	{
		// シーンに保存された値は「プレイ中の一時値」で汚染されていることがあるため、
		// FOVとY軸速度はInspectorの基準値を正として毎回強制適用する(汚染の自動修復)
		normalFOV = baseFOV;
		freeLook.m_Lens.FieldOfView = baseFOV;
		normalYMaxSpeed = baseYAxisSpeed;
		freeLook.m_YAxis.m_MaxSpeed = baseYAxisSpeed;

		// 遮蔽物フェードを使う場合、Cinemachineの「障害物回避でカメラが寄る」機能は止める
		// (ドアップになる代わりに、遮蔽物側を半透明にして見せる)
		if (fadeObstacles)
		{
			var cinemachineCollider = freeLook.GetComponent<CinemachineCollider>();
			if (cinemachineCollider != null) cinemachineCollider.enabled = false;
		}
		// ScreenXもFOVと同様に基準値を正として強制適用する
		// (旧・肩越しADSの一時値0.35がシーンに保存されたままだと常時肩越しになるため)
		normalScreenX = baseScreenX;
		for (int i = 0; i < 3; i++)
		{
			composers[i] = freeLook.GetRig(i).GetCinemachineComponent<CinemachineComposer>();
			if (composers[i] != null) composers[i].m_ScreenX = baseScreenX;
		}

		// 磁極タグ付きの敵(N_Enemy/S_Enemy)もロックオン対象に含める。
		// lockOnTagsはシーンに保存された古い値が使われるため、コード側で不足分を補う
		// (enemy_normal/enemy_skyv2系のルートは磁石用にN_Enemy/S_Enemyタグが付いている)
		var mergedTags = new List<string>(lockOnTags);
		foreach (string requiredTag in new[] { "N_Enemy", "S_Enemy" })
			if (!mergedTags.Contains(requiredTag)) mergedTags.Add(requiredTag);
		lockOnTags = mergedTags.ToArray();

		// 遮蔽物用ディザ半透明シェーダー(Assets/Resources/ObstacleDitherFade.shader)
		ditherShader = Shader.Find("Custom/ObstacleDitherFade");
		if (fadeObstacles && ditherShader == null)
			Debug.LogWarning("[PlayerAim] ObstacleDitherFade シェーダーが見つかりません(Resources配下にあるか確認)");

		// 死亡時に追従する頭ボーンを控えておく(Genericリグなので名前で検索)
		originalLookAt = freeLook.LookAt;
		originalFollow = freeLook.Follow;
		headBone = FindHeadBone();

		// プレイ中はカーソルを隠す
		ApplyCursorState();
	}

	// カーソルを隠して中央へロック(ウィンドウにフォーカスが戻った時も再適用)
	private void ApplyCursorState()
	{
		if (!hideCursor) return;
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
	}

	void OnApplicationFocus(bool focused)
	{
		if (focused) ApplyCursorState();
	}

	// 頭ボーンを名前で探す。メッシュがスキンされているのはJNTIn骨格なので
	// Main_Head_JNTIn(実名)を最優先、無ければ head_jntin → head を含む最初のノード
	private Transform FindHeadBone()
	{
		Transform jntinFallback = null;
		Transform fallback = null;
		foreach (Transform t in GetComponentsInChildren<Transform>(true))
		{
			if (t.name == "Main_Head_JNTIn") return t;
			string lower = t.name.ToLowerInvariant();
			if (jntinFallback == null && lower.Contains("head_jntin")) jntinFallback = t;
			if (fallback == null && lower.Contains("head")) fallback = t;
		}
		return jntinFallback != null ? jntinFallback : fallback;
	}

	// 死亡: バラバラに吹き飛ぶ「頭」をカメラで追従・注視し、ズームで寄る
	private void OnPlayerDied()
	{
		if (freeLook == null || headBone == null) return;

		// ロックオン/遮蔽フェードは後始末してから頭を追う
		Unlock();
		RestoreAllFaded();

		freeLook.Follow = headBone;
		freeLook.LookAt = headBone;
		for (int i = 0; i < 3; i++)
		{
			savedOrbitRadii[i] = freeLook.m_Orbits[i].m_Radius;
			freeLook.m_Orbits[i].m_Radius *= deathRadiusScale;
		}
		freeLook.m_Lens.FieldOfView = deathFOV;
		deathFocusActive = true;

		// 全体スローモーションでドラマチックに(実時間で保持→なめらかに等速へ)
		if (slowMoRoutine != null) StopCoroutine(slowMoRoutine);
		slowMoRoutine = StartCoroutine(DeathSlowMotion());
	}

	private System.Collections.IEnumerator DeathSlowMotion()
	{
		SetTimeScale(deathSlowScale);

		float t = 0f;
		while (t < deathSlowHold)
		{
			t += Time.unscaledDeltaTime;
			yield return null;
		}

		t = 0f;
		while (t < deathSlowRecover)
		{
			t += Time.unscaledDeltaTime;
			SetTimeScale(Mathf.Lerp(deathSlowScale, 1f, Mathf.Clamp01(t / deathSlowRecover)));
			yield return null;
		}

		SetTimeScale(1f);
		slowMoRoutine = null;
	}

	private void SetTimeScale(float s)
	{
		Time.timeScale = s;
		Time.fixedDeltaTime = baseFixedDeltaTime * s;
	}

	// スローを確実に解除する(復活・シーン破棄用)
	private void CancelSlowMotion()
	{
		if (slowMoRoutine != null)
		{
			StopCoroutine(slowMoRoutine);
			slowMoRoutine = null;
		}
		if (deathFocusActive) SetTimeScale(1f);
	}

	// 復活(Revive)したらカメラを元に戻す(OnDamagedはRevive時にも発火する)
	private void OnPlayerHealthChanged()
	{
		if (deathFocusActive && health != null && !health.IsDead && freeLook != null)
		{
			CancelSlowMotion();
			freeLook.Follow = originalFollow;
			freeLook.LookAt = originalLookAt;
			for (int i = 0; i < 3; i++)
				freeLook.m_Orbits[i].m_Radius = savedOrbitRadii[i];
			freeLook.m_Lens.FieldOfView = normalFOV;
			deathFocusActive = false;
		}
	}

	void OnDestroy()
	{
		// シーン遷移してもスローが残らないように(Time.timeScaleはシーンをまたいで持続する)
		CancelSlowMotion();

		if (aimAction != null)
		{
			aimAction.started -= OnAimToggle;
		}
		if (health != null)
		{
			health.OnDied -= OnPlayerDied;
			health.OnDamaged -= OnPlayerHealthChanged;
		}
		if (marker != null) Destroy(marker.gameObject);
		if (markerMat != null) Destroy(markerMat);
	}

	// R3押し込み = ロックオンのトグル専用
	private void OnAimToggle(InputAction.CallbackContext _)
	{
		if (locked) Unlock();
		else TryLockOn();
	}

	void OnDisable()
	{
		// 死亡等でこのコンポーネントが切られたら、カメラ操作を必ずプレイヤーに返す
		Unlock();
		RestoreAllFaded();
	}

	// --- 遮蔽物の半透明化 ---

	// カメラとプレイヤーの間にある物(壁・柱・敵・箱など)を半透明にし、
	// どいたら元のマテリアルに戻す
	private void UpdateObstacleFade()
	{
		if (!fadeObstacles || mainCamera == null) return;

		blockedThisFrame.Clear();

		Vector3 origin = mainCamera.transform.position;
		Vector3 target = transform.position + Vector3.up * 1f;
		Vector3 diff = target - origin;

		// シェーダーの「視線の円筒切り取り」用にプレイヤー側の端点を毎フレーム渡す
		Shader.SetGlobalVector("_ObstacleFadePlayerPos", target);

		// ① カメラとプレイヤーの間を遮っている物
		foreach (RaycastHit hit in Physics.RaycastAll(
			origin, diff.normalized, diff.magnitude, ~0, QueryTriggerInteraction.Ignore))
		{
			FadeCollider(hit.collider);
		}

		// ② カメラ位置に重なっている物。
		// 巨大な物のコライダー内部にカメラが入ると「内側から撃つレイは当たらない」ため
		// ①では検出できず素通しになる。重なり判定で拾って抜く。
		// (見えているのはカメラ前へはみ出した部分＝カメラに近いので、距離ディザが最大強度で効く)
		foreach (Collider col in Physics.OverlapSphere(
			origin, cameraOverlapRadius, ~0, QueryTriggerInteraction.Ignore))
		{
			FadeCollider(col);
		}

		// 時間フェード: 遮っている間は1へ、遮らなくなったら0へじわっと動かし、
		// 0まで戻りきったものだけ元のマテリアルへ復元する(いきなりかからない/戻らない)
		var restore = new List<Renderer>();
		var animKeys = new List<Renderer>(fadeWeights.Keys);
		foreach (Renderer r in animKeys)
		{
			if (r == null) { restore.Add(r); continue; }

			float w = fadeWeights[r];
			w = blockedThisFrame.Contains(r)
				? Mathf.MoveTowards(w, 1f, Time.deltaTime / Mathf.Max(fadeInTime, 0.01f))
				: Mathf.MoveTowards(w, 0f, Time.deltaTime / Mathf.Max(fadeOutTime, 0.01f));
			fadeWeights[r] = w;

			if (w <= 0f) { restore.Add(r); continue; }

			if (fadeInstances.TryGetValue(r, out Material[] mats))
				foreach (Material m in mats)
					if (m != null) m.SetFloat("_FadeT", w);
		}
		foreach (Renderer r in restore)
		{
			if (r != null && fadedRenderers.ContainsKey(r)) r.sharedMaterials = fadedRenderers[r];
			fadedRenderers.Remove(r);
			fadeWeights.Remove(r);
			DestroyFadeInstances(r);
		}
	}

	// 対象コライダー配下のメッシュをフェードする(自分・引き寄せ中の保持物は対象外)
	private void FadeCollider(Collider col)
	{
		Transform ht = col.transform;
		if (ht.IsChildOf(transform)) return; // 自分は対象外
		// 磁石で引き寄せ中の物は対象外(カメラ前を横切るたびチラつくのを防ぐ)
		if (magnetPull != null && magnetPull.HeldObject != null
			&& ht.IsChildOf(magnetPull.HeldObject)) return;

		foreach (Renderer r in col.GetComponentsInChildren<Renderer>())
		{
			if (r == null) continue;
			// メッシュ系だけ差し替える(パーティクルやスプライトはシェーダー互換が無いため)
			if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
			FadeRenderer(r);
			blockedThisFrame.Add(r);
		}
	}

	// マテリアルを「ディザ半透明」の差し替えマテリアルへ置き換える。
	// アルファブレンドではなく網点で抜く方式なので、前後関係が壊れず
	// 内部の面が透けて汚くならない(中途半端な透過にならない)
	private void FadeRenderer(Renderer r)
	{
		if (fadedRenderers.ContainsKey(r)) return;
		if (ditherShader == null) return;

		Material[] originals = r.sharedMaterials;
		fadedRenderers[r] = originals; // 元マテリアルを退避(戻す時はこれを再代入)

		var faded = new Material[originals.Length];
		for (int i = 0; i < originals.Length; i++)
		{
			Material src = originals[i];
			Material m = new Material(ditherShader);
			if (src != null)
			{
				// 見た目(テクスチャ・色)は元マテリアルからコピー
				string texProp = src.HasProperty("_BaseMap") ? "_BaseMap"
					: (src.HasProperty("_MainTex") ? "_MainTex" : null);
				if (texProp != null && src.GetTexture(texProp) != null)
				{
					m.SetTexture("_BaseMap", src.GetTexture(texProp));
					m.SetTextureScale("_BaseMap", src.GetTextureScale(texProp));
					m.SetTextureOffset("_BaseMap", src.GetTextureOffset(texProp));
				}
				if (src.HasProperty("_BaseColor")) m.SetColor("_BaseColor", src.GetColor("_BaseColor"));
				else if (src.HasProperty("_Color")) m.SetColor("_BaseColor", src.GetColor("_Color"));
			}
			m.SetFloat("_Alpha", obstacleAlpha);
			m.SetFloat("_HoleRadius", holeRadius);
			m.SetFloat("_HoleSoftness", holeSoftness);
			m.SetFloat("_FadeT", 0f); // 0から時間をかけてじわっとかける
			faded[i] = m;
		}
		fadeInstances[r] = faded;
		fadeWeights[r] = 0f;
		r.sharedMaterials = faded;
	}

	// 差し替え用に生成したマテリアルを破棄(リーク防止)
	private void DestroyFadeInstances(Renderer r)
	{
		if (r == null || !fadeInstances.TryGetValue(r, out Material[] mats))
		{
			// 破棄済みレンダラーの分も掃除する
			var deadKeys = new List<Renderer>();
			foreach (var kv in fadeInstances)
				if (kv.Key == null) deadKeys.Add(kv.Key);
			foreach (var k in deadKeys)
			{
				foreach (Material m in fadeInstances[k]) if (m != null) Destroy(m);
				fadeInstances.Remove(k);
			}
			return;
		}
		foreach (Material m in mats) if (m != null) Destroy(m);
		fadeInstances.Remove(r);
	}

	private void RestoreAllFaded()
	{
		foreach (var kv in fadedRenderers)
			if (kv.Key != null) kv.Key.sharedMaterials = kv.Value;
		fadedRenderers.Clear();
		fadeWeights.Clear();

		foreach (var kv in fadeInstances)
			foreach (Material m in kv.Value)
				if (m != null) Destroy(m);
		fadeInstances.Clear();
	}

	void Update()
	{
		// 死亡中はカメラ制御をしない(死亡カメラのFOV/距離を上書きしないように)
		if (health != null && health.IsDead) return;

		float t = lerpSpeed * Time.deltaTime;

		// FOV(ズーム)。ロックオン中だけ少し寄る
		float targetFOV = normalFOV * (locked ? lockOnFOVScale : 1f);
		freeLook.m_Lens.FieldOfView = Mathf.Lerp(freeLook.m_Lens.FieldOfView, targetFOV, t);

		// 肩寄せ: ロックオン中だけ右肩越し(プレイヤーを画面左へ寄せて対象を見やすく)。
		// 通常時は中央(0.5)＝肩越しなし
		for (int i = 0; i < 3; i++)
		{
			if (composers[i] != null)
			{
				float targetX = locked ? lockOnScreenX : normalScreenX;
				composers[i].m_ScreenX = Mathf.Lerp(composers[i].m_ScreenX, targetX, t);
			}
		}

		UpdateLookUp();
		UpdateLockOn();
	}

	// FreeLookはカメラ位置とプレイヤー注視が連動していて、カメラを地面より下に置けない
	// ＝そのままでは真上を見上げられない。そこでカメラが下限に近づくほど注視点を
	// プレイヤーの頭上高くへ持ち上げ、カメラ位置は低いまま視線だけを上へ向ける。
	private void UpdateLookUp()
	{
		float t = lookUpStartY > 0f
			? Mathf.Clamp01(1f - freeLook.m_YAxis.Value / lookUpStartY)
			: 0f;

		// 見上げゾーンに入るほど上下の視点速度を落とす(急に空へ振り向かないように)
		freeLook.m_YAxis.m_MaxSpeed = normalYMaxSpeed * Mathf.Lerp(1f, lookUpSpeedScale, t);

		float eased = t * t; // 下限付近ほど強く効かせ、通常域の構図には影響させない

		// 最下段オービットの半径から、目標仰角となる注視点の高さを逆算
		float offsetY = Mathf.Tan(maxLookUpAngle * eased * Mathf.Deg2Rad)
			* freeLook.m_Orbits[2].m_Radius;

		for (int i = 0; i < 3; i++)
		{
			if (composers[i] != null)
				composers[i].m_TrackedObjectOffset = new Vector3(0f, offsetY, 0f);
		}
	}

	// --- ロックオン ---

	// 画面中央に最も近い対象へロックオンする(exclude は乗り換え時に除外したい対象)
	private void TryLockOn(Transform exclude = null)
	{
		Vector3 camPos = mainCamera.transform.position;
		Vector3 fwd = mainCamera.transform.forward;
		float bestScore = float.MaxValue;
		Transform best = null;
		Collider bestCol = null;

		foreach (var (t, col, point) in GatherCandidates())
		{
			if (t == exclude) continue;
			if (Vector3.Distance(transform.position, point) > lockOnRange) continue;
			if (!HasLineOfSight(point, t)) continue;

			// 画面中央に近いものを優先し、距離で同点解消
			float score = Vector3.Angle(fwd, point - camPos)
				+ Vector3.Distance(camPos, point) * 0.5f;
			if (score < bestScore)
			{
				bestScore = score;
				best = t;
				bestCol = col;
			}
		}

		if (best == null) return;

		locked = true;
		lockTarget = best;
		lockTargetCol = bestCol;
		switchArmed = false; // スティックが一度中立に戻るまで照準切替しない
		// ロックオン中は通常のカメラ操作を止める(スティックは照準切替に使う)
		if (inputProvider != null) inputProvider.enabled = false;
	}

	private void Unlock()
	{
		locked = false;
		lockTarget = null;
		lockTargetCol = null;
		if (inputProvider != null) inputProvider.enabled = true;
		if (marker != null) marker.gameObject.SetActive(false);
	}

	private void UpdateLockOn()
	{
		if (!locked) return;

		// ホールド中もロックオンは維持する(飛ばす先の指定に使うため)。
		// ただし「掴んだ対象そのもの」へのロックは意味がないので静かに外す
		// (自動乗り換えはしない。飛ばす先は改めてR3で選んでもらう)
		if (magnetPull != null && magnetPull.HeldObject == lockTarget)
		{
			Unlock();
			return;
		}

		// 対象が消えた(Destroy含む)・離れすぎた → 近くの別対象へ乗り換え(いなければ解除)
		bool gone = lockTarget == null || !lockTarget.gameObject.activeInHierarchy;
		bool invalid = gone
			|| Vector3.Distance(transform.position, LockPoint()) > lockOnRange * 1.3f;
		if (invalid)
		{
			Transform old = lockTarget;
			Unlock();
			TryLockOn(exclude: old);
			if (!locked) return;
		}

		// 画面中央が対象を向くよう、FreeLookの2軸を毎フレーム寄せていく。
		// (SimpleFollowバインドでは m_XAxis.Value への加算が「今フレームの相対回転」になる)
		Vector3 camPos = mainCamera.transform.position;
		Vector3 fwd = mainCamera.transform.forward;
		Vector3 to = LockPoint() - camPos;
		Vector3 fwdFlat = new Vector3(fwd.x, 0f, fwd.z);
		Vector3 toFlat = new Vector3(to.x, 0f, to.z);

		if (fwdFlat.sqrMagnitude > 0.0001f && toFlat.sqrMagnitude > 0.0001f)
		{
			float yawErr = Vector3.SignedAngle(fwdFlat, toFlat, Vector3.up);
			freeLook.m_XAxis.Value += yawErr * Mathf.Min(1f, lockOnYawSpeed * Time.deltaTime);

			// 上下はY軸(0..1)を実際の仰角誤差が縮む方向へ動かす。
			// 見上げオフセット込みの複雑な対応関係でも、実カメラの向きを測るフィードバックなので収束する
			float pitchCur = Mathf.Atan2(fwd.y, fwdFlat.magnitude) * Mathf.Rad2Deg;
			float pitchTgt = Mathf.Atan2(to.y, toFlat.magnitude) * Mathf.Rad2Deg;
			freeLook.m_YAxis.Value = Mathf.Clamp01(
				freeLook.m_YAxis.Value - (pitchTgt - pitchCur) * lockOnPitchGain * Time.deltaTime);
		}

		// 右スティックを一定以上倒したら、その方向の別対象へ照準切替
		Vector2 look = cameraAction != null ? cameraAction.ReadValue<Vector2>() : Vector2.zero;
		if (!switchArmed)
		{
			if (look.magnitude <= switchRearmThreshold) switchArmed = true;
		}
		else if (look.magnitude >= switchThreshold)
		{
			SwitchTarget(look.normalized);
			switchArmed = false;
		}
	}

	// 現在の対象から見て、スティックを倒した方向(画面基準)にある別対象へ切り替える
	private void SwitchTarget(Vector2 dir)
	{
		if (lockTarget == null) return;

		Vector3 curVp = mainCamera.WorldToViewportPoint(LockPoint());
		float bestScore = float.MaxValue;
		Transform best = null;
		Collider bestCol = null;

		foreach (var (t, col, point) in GatherCandidates())
		{
			if (t == lockTarget) continue;
			if (Vector3.Distance(transform.position, point) > lockOnRange) continue;
			if (!HasLineOfSight(point, t)) continue;

			Vector3 vp = mainCamera.WorldToViewportPoint(point);
			if (vp.z <= 0f) continue; // カメラの真後ろは対象外

			Vector2 d = new Vector2(vp.x - curVp.x, vp.y - curVp.y);
			if (d.sqrMagnitude < 0.000001f) continue;

			float dot = Vector2.Dot(dir, d.normalized);
			if (dot < 0.35f) continue; // 倒した方向から外れすぎているものは無視

			// 方向が合っていて画面上で近いものを優先
			float score = d.magnitude / dot;
			if (score < bestScore)
			{
				bestScore = score;
				best = t;
				bestCol = col;
			}
		}

		if (best != null)
		{
			lockTarget = best;
			lockTargetCol = bestCol;
		}
	}

	// ロックオン候補(タグで収集)。自分自身と、いま磁力で掴んでいる物は除外
	private List<(Transform t, Collider col, Vector3 point)> GatherCandidates()
	{
		var list = new List<(Transform, Collider, Vector3)>();
		foreach (string tag in lockOnTags)
		{
			GameObject[] objs;
			try { objs = GameObject.FindGameObjectsWithTag(tag); }
			catch (UnityException) { continue; } // 未定義のタグは無視

			foreach (var go in objs)
			{
				Transform t = go.transform;
				if (t.IsChildOf(transform)) continue;
				if (magnetPull != null && magnetPull.HeldObject != null
					&& (t == magnetPull.HeldObject || t.IsChildOf(magnetPull.HeldObject))) continue;

				Collider col = go.GetComponentInChildren<Collider>();
				Vector3 point = col != null ? col.bounds.center : t.position;
				list.Add((t, col, point));
			}
		}
		return list;
	}

	// カメラから対象までの間に壁(自分と対象以外のソリッド)が無いか
	private bool HasLineOfSight(Vector3 point, Transform target)
	{
		Vector3 origin = mainCamera.transform.position;
		Vector3 diff = point - origin;
		var hits = Physics.RaycastAll(origin, diff.normalized, diff.magnitude,
			~0, QueryTriggerInteraction.Ignore);
		foreach (var h in hits)
		{
			Transform ht = h.collider.transform;
			if (ht.IsChildOf(transform) || ht.IsChildOf(target)) continue;
			return false;
		}
		return true;
	}

	private Vector3 LockPoint()
	{
		if (lockTargetCol != null) return lockTargetCol.bounds.center;
		return lockTarget != null ? lockTarget.position : Vector3.zero;
	}

	// --- ロックオンマーカー(狙っている対象の可視化) ---

	// カメラ確定後の位置に合わせたいので LateUpdate で更新
	void LateUpdate()
	{
		UpdateObstacleFade();

		if (!locked || lockTarget == null)
		{
			if (marker != null) marker.gameObject.SetActive(false);
			return;
		}

		EnsureMarker();
		marker.gameObject.SetActive(true);
		marker.position = LockPoint();
		marker.rotation = mainCamera.transform.rotation; // 常にカメラへ正対

		// 対象の大きさに合わせて枠のサイズを調整
		float size = 1f;
		if (lockTargetCol != null)
		{
			Vector3 e = lockTargetCol.bounds.extents;
			size = Mathf.Max(e.x, e.y, e.z) * 2f;
		}
		marker.localScale = Vector3.one * Mathf.Clamp(size * markerScale, 0.6f, 6f);

		if (markerSpinner != null)
			markerSpinner.localRotation = Quaternion.Euler(0f, 0f, Time.time * markerSpinSpeed);

		if (markerMat != null) markerMat.color = ColorForTarget();
	}

	private Color ColorForTarget()
	{
		if (lockTarget.CompareTag("Enemy")) return enemyMarkerColor;
		if (lockTarget.CompareTag("N_Pole") || lockTarget.CompareTag("N_Enemy")) return nPoleMarkerColor;
		if (lockTarget.CompareTag("S_Pole") || lockTarget.CompareTag("S_Enemy")) return sPoleMarkerColor;
		return Color.white;
	}

	private void EnsureMarker()
	{
		if (marker != null) return;

		if (markerPrefab != null)
		{
			// 独自デザインが指定されていればそれを使う(色・回転はプレハブ側にお任せ)
			marker = Instantiate(markerPrefab).transform;
			return;
		}

		// 自動生成: 対象を囲む四隅のカギカッコ枠
		marker = new GameObject("LockOnMarker").transform;
		markerSpinner = new GameObject("Frame").transform;
		markerSpinner.SetParent(marker, false);

		markerMat = new Material(Shader.Find("Sprites/Default"));

		for (int ix = -1; ix <= 1; ix += 2)
		{
			for (int iy = -1; iy <= 1; iy += 2)
			{
				CreateMarkerBar(new Vector2(ix * 0.36f, iy * 0.5f), new Vector2(0.28f, 0.07f)); // 横棒
				CreateMarkerBar(new Vector2(ix * 0.5f, iy * 0.36f), new Vector2(0.07f, 0.28f)); // 縦棒
			}
		}
	}

	private void CreateMarkerBar(Vector2 pos, Vector2 size)
	{
		var bar = GameObject.CreatePrimitive(PrimitiveType.Quad);
		bar.name = "bar";
		Destroy(bar.GetComponent<Collider>()); // レイキャストやLOS判定を邪魔しない
		bar.transform.SetParent(markerSpinner, false);
		bar.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
		bar.transform.localScale = new Vector3(size.x, size.y, 1f);

		var renderer = bar.GetComponent<MeshRenderer>();
		renderer.sharedMaterial = markerMat;
		renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		renderer.receiveShadows = false;
	}

}
