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

	[Header("エイム設定")]
	[SerializeField] private float aimFOV = 28f;           // エイム時のFOV(小さいほどアップ)
	[SerializeField] private float aimRadiusScale = 0.6f;  // エイム時の距離倍率(小さいほど接近)
	[SerializeField] private float aimScreenX = 0.35f;     // エイム時の肩寄せ(0.5=中央, 小さいほど右肩越し)
	[SerializeField] private float lerpSpeed = 10f;        // 寄り/戻りのなめらかさ
	[SerializeField] private LayerMask aimTargetLayer;

	[Header("見上げ設定")]
	[Tooltip("カメラ上下(Y軸 0=下,1=上)がこの値を下回ると、注視点を頭上へ持ち上げてカメラが上を向き始める")]
	[SerializeField] private float lookUpStartY = 0.35f;
	[Tooltip("カメラを一番下まで下げた時の仰角(度)。FreeLookの構造上90ちょうどは不可、80前後が実用上限")]
	[SerializeField] private float maxLookUpAngle = 78f;
	[Tooltip("見上げゾーンでの上下視点速度の倍率(1=通常と同じ、小さいほどゆっくり上を向く)")]
	[SerializeField] private float lookUpSpeedScale = 0.85f;

	[Header("ロックオン設定(R3押し込み)")]
	[Tooltip("ロックオン対象のタグ(吸い寄せオブジェクトと敵)")]
	[SerializeField] private string[] lockOnTags = { "Enemy", "N_Pole", "S_Pole" };
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

	public bool IsAiming { get; private set; }
	public Vector3 AimPoint { get; private set; }
	public bool IsLockedOn => locked;
	public Transform LockOnTarget => lockTarget;

	private Camera mainCamera;
	private PlayerInput playerInput;
	private InputAction aimAction;
	private InputAction cameraAction;
	private PlayerCatch catchState;
	private MagnetPull magnetPull;
	private CinemachineInputProvider inputProvider;

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
	private float[] normalRadii = new float[3];
	private float normalScreenX = 0.5f;
	private CinemachineComposer[] composers = new CinemachineComposer[3];

	void Awake()
	{
		mainCamera = Camera.main;
		playerInput = GetComponent<PlayerInput>();
		aimAction = playerInput.actions["Aim"];
		// 押し込み一回でAim ON/OFFをトグル（押しっぱなし不要）
		aimAction.started += OnAimToggle;

		catchState = GetComponent<PlayerCatch>();
		magnetPull = GetComponent<MagnetPull>();
		cameraAction = playerInput.actions["Camera"];
		inputProvider = freeLook != null ? freeLook.GetComponent<CinemachineInputProvider>() : null;
	}

	void Start()
	{
		// シーンに保存された値は「プレイ中の一時値」で汚染されていることがあるため、
		// FOVとY軸速度はInspectorの基準値を正として毎回強制適用する(汚染の自動修復)
		normalFOV = baseFOV;
		freeLook.m_Lens.FieldOfView = baseFOV;
		normalYMaxSpeed = baseYAxisSpeed;
		freeLook.m_YAxis.m_MaxSpeed = baseYAxisSpeed;
		for (int i = 0; i < 3; i++)
		{
			normalRadii[i] = freeLook.m_Orbits[i].m_Radius;
			composers[i] = freeLook.GetRig(i).GetCinemachineComponent<CinemachineComposer>();
		}
		if (composers[1] != null) normalScreenX = composers[1].m_ScreenX;
	}

	void OnDestroy()
	{
		if (aimAction != null)
		{
			aimAction.started -= OnAimToggle;
		}
		if (marker != null) Destroy(marker.gameObject);
		if (markerMat != null) Destroy(markerMat);
	}

	private void OnAimToggle(InputAction.CallbackContext _)
	{
		IsAiming = !IsAiming;

		// R3押し込みはロックオンのトグルも兼ねる
		if (locked) Unlock();
		else TryLockOn();
	}

	void OnDisable()
	{
		// 死亡等でこのコンポーネントが切られたら、カメラ操作を必ずプレイヤーに返す
		Unlock();
	}

	void Update()
	{
		// Aimは「Catch中だけ」有効。Catchでなければ強制的に通常カメラへ戻す
		if (catchState != null && !catchState.IsCatching) IsAiming = false;

		float t = lerpSpeed * Time.deltaTime;

		// FOV(ズーム)。エイム中は大きく寄り、ロックオン中は少しだけ寄る
		float targetFOV = IsAiming ? aimFOV : normalFOV * (locked ? lockOnFOVScale : 1f);
		freeLook.m_Lens.FieldOfView = Mathf.Lerp(freeLook.m_Lens.FieldOfView, targetFOV, t);

		// 距離(各リグの半径)と肩寄せ
		for (int i = 0; i < 3; i++)
		{
			float targetRadius = normalRadii[i] * (IsAiming ? aimRadiusScale : 1f);
			freeLook.m_Orbits[i].m_Radius = Mathf.Lerp(freeLook.m_Orbits[i].m_Radius, targetRadius, t);

			if (composers[i] != null)
			{
				float targetX = IsAiming ? aimScreenX : normalScreenX;
				composers[i].m_ScreenX = Mathf.Lerp(composers[i].m_ScreenX, targetX, t);
			}
		}

		UpdateLookUp();
		UpdateLockOn();

		if (IsAiming) UpdateAimPoint();
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

		// オブジェクトを保持(ホールド)したらロックオンは解除する
		if (magnetPull != null && magnetPull.IsHolding)
		{
			Unlock();
			return;
		}

		// 対象が消えた(Destroy含む)・掴んだ・離れすぎた → 近くの別対象へ乗り換え(いなければ解除)
		bool gone = lockTarget == null || !lockTarget.gameObject.activeInHierarchy;
		bool invalid = gone
			|| (magnetPull != null && magnetPull.HeldObject == lockTarget)
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
		if (lockTarget.CompareTag("N_Pole")) return nPoleMarkerColor;
		if (lockTarget.CompareTag("S_Pole")) return sPoleMarkerColor;
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

	private void UpdateAimPoint()
	{
		Ray ray = mainCamera.ScreenPointToRay(
			new Vector3(Screen.width / 2f, Screen.height / 2f, 0f)
		);
		AimPoint = Physics.Raycast(ray, out RaycastHit hit, 100f, aimTargetLayer)
			? hit.point
			: ray.GetPoint(100f);
	}
}
