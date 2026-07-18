using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

// ボス登場カットシーン。プレイヤーがトリガー範囲に入ると「一度だけ」再生する。
//
// 演技させるのは “シーンに配置済みの本物のボス(modelBoss_v3 / enemy_Boss)” 本体。
// 別モデルを生成せず、ボス自身のAnimatorのコントローラを一時的に登場クリップへ差し替えて
// 動かすので、位置もスケールも本物のまま（でっかい別ボスが出る問題が起きない）。
//   - 再生中は enemy_Boss を止め（戦闘/注視/デバッグキーの割り込み防止）、
//     Animator を Generic(アバターnull)のパス一致にして animationBossStart_v1 を再生。
//   - 終了後は元のコントローラ/アバターへ戻し、enemy_Boss.isStartAction=true で戦闘開始。
//   - カメラは cameraBossStart_v1 のカメラノードを一時vcamで追従させ、Cinemachineのブレンドで
//     FreeLookから「滑らかに入り(before)」「滑らかに戻す(after)」。
//   - スキップ不可・操作不可（再生中はプレイヤーの入力/移動を止める）。
[RequireComponent(typeof(Collider))]
public class BossStartCutscene : MonoBehaviour
{
	[Header("ボスに再生させるクリップ(animationBossStart_v1)")]
	[SerializeField] private AnimationClip actorClip;

	[Header("カメラ側FBX(cameraBossStart_v1)とクリップ")]
	[SerializeField] private GameObject cameraPrefab;
	[SerializeField] private AnimationClip cameraClip;

	[Header("再生用テンプレート(CutscenePlayback.controller)")]
	[SerializeField] private RuntimeAnimatorController playbackTemplate;

	[Header("演出: 咆哮(手を開くタイミングで発動)")]
	[Tooltip("咆哮するクリップのフレーム番号(30FPS)。Animationウィンドウで確認した値を入れる")]
	[SerializeField] private float roarFrame = 120f;
	[Tooltip("カメラの揺れの長さ(秒)")]
	[SerializeField] private float roarShakeDuration = 0.9f;
	[Tooltip("カメラの揺れの強さ(m)。0で揺れなし")]
	[SerializeField] private float roarShakeStrength = 0.2f;
	[Tooltip("咆哮の瞬間からリップルが始まるまでの遅れ(秒)")]
	[SerializeField] private float roarRippleDelay = 0.25f;
	[Tooltip("波を繰り返す回数")]
	[SerializeField] private int roarRippleCount = 5;
	[Tooltip("波と波の間隔(秒)。Durationより短くすると波が重なって連続的に見える")]
	[SerializeField] private float roarRippleInterval = 0.22f;
	[Tooltip("1つの波が画面外へ抜けるまでの長さ(秒)")]
	[SerializeField] private float roarRippleDuration = 0.7f;
	[Tooltip("画面リップルの歪みの強さ(0で無効)")]
	[SerializeField] private float roarRippleStrength = 0.035f;
	[Tooltip("(任意)咆哮の瞬間にボス中心へ出すエフェクト。リップルだけで良ければ空のまま")]
	[SerializeField] private GameObject roarEffectPrefab;
	[Tooltip("エフェクトを自動で消すまでの秒数")]
	[SerializeField] private float roarEffectLifetime = 3f;
	[Tooltip("咆哮SE(任意。未指定なら音なし)")]
	[SerializeField] private AudioClip roarSound;
	[SerializeField, Range(0f, 1f)] private float roarVolume = 1f;

	[Header("動かすボス(未指定ならシーンの enemy_Boss を自動検索)")]
	[SerializeField] private enemy_Boss boss;

	[Header("カメラ生成アンカー(未指定ならボスの位置)")]
	[SerializeField] private Transform spawnPoint;

	[Header("カメラがカットシーンへ入る/戻るブレンド秒数")]
	[SerializeField] private float cameraBlendInTime = 1.0f;
	[SerializeField] private float cameraBlendOutTime = 1.2f;

	[Header("終了後にボスの戦闘を開始する(isStartAction=true)")]
	[SerializeField] private bool startBossAfter = true;

	[Header("再生中はHUD(スクリーンUI)を隠す")]
	[SerializeField] private bool hideUI = true;

	[Header("プレイヤーがトリガーに入ったら自動再生する")]
	[SerializeField] private bool playOnTrigger = true;

	// 進行状態
	private bool hasPlayed;
	private bool playing;
	private float endTime;
	private float cutsceneStartTime;
	private float clipFPS = 30f;

	// 咆哮演出の状態
	private bool roarTriggered;
	private float shakeRemaining;

	// カメラFBX
	private GameObject cameraInstance;
	private Transform cameraNode;   // カメラアニメ内の追従対象ノード
	private Camera rigCamera;       // FOVのコピー元

	// カメラ乗っ取り
	private Camera mainCam;
	private CinemachineBrain brain;
	private CinemachineVirtualCamera puppetVcam;      // カメラノードへ毎フレーム同期する一時vcam
	private CinemachineBlendDefinition savedBlend;
	private bool blendSaved;

	// プレイヤー退避(見た目は消さず、入力/移動だけ止める)
	private GameObject player;
	private readonly List<Behaviour> disabledPlayerBehaviours = new List<Behaviour>();
	private Rigidbody playerRb;
	private bool playerRbWasKinematic;

	// ボス退避(本物のボスのAnimatorを一時的に乗っ取る)
	private Animator bossAnim;
	private RuntimeAnimatorController originalBossController;
	private Avatar originalBossAvatar;
	private bool originalApplyRootMotion;
	private AnimatorCullingMode originalCullingMode;
	private bool bossWasEnabled;
	private bool bossTakenOver;

	// スケール固定用: cm単位リグ(Root_JNTin=Lcl Scaling 0.01)の補正が
	// 登場クリップ(Rootスケール1.0焼き込み)で吹き飛んで巨大化するのを防ぐため、
	// 再生前の各ボーンのlocalScaleを控えて毎フレーム戻す
	private readonly List<Transform> bossScaleBones = new List<Transform>();
	private readonly List<Vector3> bossScaleValues = new List<Vector3>();

	// UI退避
	private readonly List<Canvas> hiddenCanvases = new List<Canvas>();
	private bool nyuxtuHidden; // Nyuxtu3でHUDをしまったか(終了時にShowUIで返す)

	void Reset()
	{
		// アタッチ時にトリガー化しておく(範囲判定用)
		Collider c = GetComponent<Collider>();
		if (c != null) c.isTrigger = true;
	}

	void OnTriggerEnter(Collider other)
	{
		if (!playOnTrigger || hasPlayed || playing) return;

		// プレイヤー判定: 本体(PlayerMovement)か、無ければタグ"Player"
		bool isPlayer = other.GetComponentInParent<PlayerMovement>() != null
			|| other.CompareTag("Player");
		if (isPlayer) Play();
	}

	// 外部(スクリプトや別トリガー)からも起動できるように公開
	public void Play()
	{
		if (hasPlayed || playing) return;
		if (actorClip == null || playbackTemplate == null)
		{
			Debug.LogWarning("[BossStartCutscene] actorClip / playbackTemplate が未設定のため再生できません");
			return;
		}
		hasPlayed = true;

		// 本物のボスを自動検索(未指定時)。
		// シーンにはボスが複数いる(karihaiti.prefab内にも別のボスがいる)ため、
		// 「最初に見つかった1体」ではなく“このトリガーに最も近いボス”を選ぶ。
		// ※以前 FindFirstObjectByType で別のボスを掴んで、そっちが動いてしまっていた
		if (boss == null)
		{
			float best = float.MaxValue;
			foreach (enemy_Boss b in FindObjectsByType<enemy_Boss>(FindObjectsSortMode.None))
			{
				float d = (b.transform.position - transform.position).sqrMagnitude;
				if (d < best) { best = d; boss = b; }
			}
		}

		// プレイヤーを取得(GameStartCutsceneと同じ優先度)
		PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
		player = pm != null ? pm.gameObject : GameObject.FindGameObjectWithTag("Player");

		// カメラ生成アンカー: spawnPoint > 本物のボス > 自分
		Transform anchor = spawnPoint != null ? spawnPoint
			: (boss != null ? boss.transform : transform);
		Vector3 anchorPos = anchor.position;
		Quaternion anchorRot = anchor.rotation;

		FreezePlayer();

		float length = actorClip.length;

		// 本物のボスに登場クリップを再生させる
		BeginBossPlayback();

		// カメラFBXをボスのアンカーへ生成
		if (cameraPrefab != null)
		{
			cameraInstance = Instantiate(cameraPrefab, anchorPos, anchorRot);
			if (cameraClip != null)
			{
				PlayCameraOn(cameraInstance, cameraClip);
				length = Mathf.Max(length, cameraClip.length);
			}
			cameraNode = FindCameraNode(cameraInstance);
			rigCamera = cameraNode != null ? cameraNode.GetComponent<Camera>() : null;
		}
		else
		{
			Debug.LogWarning("[BossStartCutscene] Camera Prefab が未設定のためカメラは動きません");
		}

		BeginCameraTakeover();

		endTime = Time.time + (length > 0f ? length : 5f);
		cutsceneStartTime = Time.time;
		clipFPS = actorClip != null && actorClip.frameRate > 0f ? actorClip.frameRate : 30f;
		roarTriggered = false;
		playing = true;
		Debug.Log($"[BossStartCutscene] 再生開始 ({length:F1}秒)");
	}

	void Update()
	{
		if (!playing) return;

		// 咆哮: 指定フレームに達したら一回だけ発動
		float elapsedFrames = (Time.time - cutsceneStartTime) * clipFPS;
		if (!roarTriggered && elapsedFrames >= roarFrame)
		{
			roarTriggered = true;
			TriggerRoar();
		}

		// スキップ入力は「受け付けない」。尺が来たら終わるだけ
		if (Time.time >= endTime) Finish();
	}

	// 咆哮: 画面リップル + カメラシェイク + SE (+ 任意でエフェクト)
	private void TriggerRoar()
	{
		if (roarRippleStrength > 0f) StartCoroutine(ScreenRippleBurst());

		if (roarEffectPrefab != null)
		{
			GameObject fx = Instantiate(roarEffectPrefab, GetBossCenter(), Quaternion.identity);
			Destroy(fx, roarEffectLifetime);
		}

		if (roarSound != null && mainCam != null)
			AudioSource.PlayClipAtPoint(roarSound, mainCam.transform.position, roarVolume);

		shakeRemaining = roarShakeDuration;
	}

	// 少し遅れて開始し、波を roarRippleCount 回連続で発生させる
	// (各波は独立したQuadなので、間隔<波の長さ なら同心円状に重なって走る)
	private IEnumerator ScreenRippleBurst()
	{
		if (roarRippleDelay > 0f) yield return new WaitForSeconds(roarRippleDelay);

		for (int i = 0; i < roarRippleCount; i++)
		{
			StartCoroutine(ScreenRippleRoutine());
			if (i < roarRippleCount - 1)
				yield return new WaitForSeconds(Mathf.Max(roarRippleInterval, 0.02f));
		}
	}

	// 画面中心から外側へ波打つスクリーンリップル(1波分)。
	// カメラ前のQuadに Magnet/ScreenRipple を貼り、_Progress を 0→1 へ流す
	private IEnumerator ScreenRippleRoutine()
	{
		Shader rippleShader = Shader.Find("Magnet/ScreenRipple");
		if (rippleShader == null || mainCam == null)
		{
			Debug.LogWarning("[BossStartCutscene] ScreenRipple シェーダーが見つかりません(Resources配下にあるか確認)");
			yield break;
		}

		GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
		quad.name = "RoarScreenRipple";
		Destroy(quad.GetComponent<Collider>());
		// 描画は頂点シェーダーで画面全体に引き伸ばすため、位置はカリング回避用
		quad.transform.SetParent(mainCam.transform, false);
		quad.transform.localPosition = new Vector3(0f, 0f, 0.5f);

		Material m = new Material(rippleShader);
		m.SetFloat("_Amplitude", roarRippleStrength);
		MeshRenderer mr = quad.GetComponent<MeshRenderer>();
		mr.sharedMaterial = m;
		mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		mr.receiveShadows = false;

		float t = 0f;
		while (t < roarRippleDuration)
		{
			t += Time.deltaTime;
			m.SetFloat("_Progress", Mathf.Clamp01(t / Mathf.Max(roarRippleDuration, 0.01f)));
			yield return null;
		}

		Destroy(quad);
		Destroy(m);
	}

	// ボスのレンダラー境界の中心(咆哮エフェクトの発生位置)
	private Vector3 GetBossCenter()
	{
		if (boss == null) return transform.position;
		Bounds b = new Bounds(boss.transform.position, Vector3.zero);
		bool has = false;
		foreach (Renderer r in boss.GetComponentsInChildren<Renderer>())
		{
			if (!has) { b = r.bounds; has = true; }
			else b.Encapsulate(r.bounds);
		}
		return has ? b.center : boss.transform.position;
	}

	void LateUpdate()
	{
		// Animatorがボーンを動かした後にスケールだけ元へ戻す(描画前)。
		// 再生本編中も終了ブレンド中(最終ポーズ固定)も巨大化させない。
		if (bossTakenOver) PinBossScales();

		if (!playing) return;

		// 咆哮のカメラシェイク量(残り時間で減衰。カメラのローカルXY方向に揺らす)
		Vector3 shakeOffset = Vector3.zero;
		if (shakeRemaining > 0f)
		{
			shakeRemaining -= Time.deltaTime;
			float k = Mathf.Clamp01(shakeRemaining / Mathf.Max(roarShakeDuration, 0.01f));
			shakeOffset = new Vector3(
				Mathf.PerlinNoise(Time.time * 30f, 0.37f) - 0.5f,
				Mathf.PerlinNoise(0.71f, Time.time * 30f) - 0.5f,
				0f) * (2f * roarShakeStrength * k);
		}

		if (puppetVcam != null && cameraNode != null)
		{
			// 一時vcamをカメラノードへ同期(FreeLook↔vcam のブレンドはCinemachineが担当)
			puppetVcam.transform.SetPositionAndRotation(cameraNode.position, cameraNode.rotation);
			if (shakeOffset != Vector3.zero)
				puppetVcam.transform.position += puppetVcam.transform.rotation * shakeOffset;
			if (rigCamera != null)
			{
				LensSettings lens = puppetVcam.m_Lens;
				lens.FieldOfView = rigCamera.fieldOfView;
				puppetVcam.m_Lens = lens;
			}
		}
		else if (brain == null && mainCam != null && cameraNode != null)
		{
			// Brainが無い環境向けフォールバック: メインカメラを直接重ねる(ブレンド無し)
			mainCam.transform.SetPositionAndRotation(cameraNode.position, cameraNode.rotation);
			if (shakeOffset != Vector3.zero)
				mainCam.transform.position += mainCam.transform.rotation * shakeOffset;
			if (rigCamera != null) mainCam.fieldOfView = rigCamera.fieldOfView;
		}
	}

	// ------------------------------------------------------------
	// ボス: 本物のAnimatorを一時的に乗っ取って登場クリップを再生
	// ------------------------------------------------------------
	private void BeginBossPlayback()
	{
		if (boss == null)
		{
			Debug.LogWarning("[BossStartCutscene] ボス(enemy_Boss)が見つからないため演技できません");
			return;
		}

		bossAnim = boss.GetComponent<Animator>();
		if (bossAnim == null) bossAnim = boss.GetComponentInChildren<Animator>(true);
		if (bossAnim == null)
		{
			Debug.LogWarning("[BossStartCutscene] ボスにAnimatorが無いため演技させられません");
			return;
		}

		// 戦闘/注視/デバッグキーが割り込まないように行動制御を止める
		bossWasEnabled = boss.enabled;
		boss.enabled = false;

		// 再生前(通常サイズ)の各ボーンのlocalScaleを控える。
		// 登場クリップはボーンのスケールを焼き込んでおり、avatar=null で流すと
		// cm補正(Root_JNTin=0.01)が上書きされて巨大化するので、毎フレーム元へ戻す。
		bossScaleBones.Clear();
		bossScaleValues.Clear();
		foreach (Transform t in boss.GetComponentsInChildren<Transform>(true))
		{
			bossScaleBones.Add(t);
			bossScaleValues.Add(t.localScale);
		}

		// 元の状態を控えて、Generic(アバターnull)のパス一致で登場クリップを再生する
		//（modelMain/CutsceneTestで実績のある再生方式に合わせる）
		originalBossController = bossAnim.runtimeAnimatorController;
		originalBossAvatar = bossAnim.avatar;
		originalApplyRootMotion = bossAnim.applyRootMotion;
		originalCullingMode = bossAnim.cullingMode;
		bossTakenOver = true;

		bossAnim.enabled = true;
		bossAnim.avatar = null;
		bossAnim.applyRootMotion = true;
		bossAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

		bossAnim.runtimeAnimatorController = BuildPlaybackController(actorClip);
		bossAnim.Rebind();
		bossAnim.Update(0f);
		PinBossScales(); // フレーム0から通常サイズにする
	}

	// 再生中、各ボーンのlocalScaleを再生前の値へ固定する(巨大化防止)。
	// 回転・移動アニメはそのまま活かし、スケールだけ元に戻す。
	private void PinBossScales()
	{
		for (int i = 0; i < bossScaleBones.Count; i++)
			if (bossScaleBones[i] != null)
				bossScaleBones[i].localScale = bossScaleValues[i];
	}

	// ボスを元のコントローラ/アバターへ戻し、戦闘を開始する
	private void RestoreBoss()
	{
		if (bossTakenOver && bossAnim != null)
		{
			bossAnim.speed = 1f;
			bossAnim.runtimeAnimatorController = originalBossController;
			bossAnim.avatar = originalBossAvatar;
			bossAnim.applyRootMotion = originalApplyRootMotion;
			bossAnim.cullingMode = originalCullingMode;
			bossAnim.Rebind();
			bossAnim.Update(0f);   // 復帰直後のTポーズ点滅を防ぐため即評価
		}
		bossTakenOver = false;
		bossAnim = null;
		bossScaleBones.Clear();
		bossScaleValues.Clear();

		if (boss != null)
		{
			boss.enabled = bossWasEnabled;
			if (startBossAfter) boss.isStartAction = true; // 戦闘開始
		}
	}

	// ------------------------------------------------------------
	// カメラ乗っ取り(ブレンドイン)
	// ------------------------------------------------------------
	private void BeginCameraTakeover()
	{
		mainCam = Camera.main;
		brain = mainCam != null ? mainCam.GetComponent<CinemachineBrain>() : null;

		if (brain == null) return; // フォールバック(LateUpdateで直接重ねる)

		// カメラノードの現在ポーズを初期値に持つ一時vcamを最優先で立てる。
		// Brainが FreeLook → この一時vcam へ cameraBlendInTime かけてブレンドする。
		GameObject go = new GameObject("BossCutsceneCamera");
		puppetVcam = go.AddComponent<CinemachineVirtualCamera>();
		puppetVcam.Priority = 10000;
		if (cameraNode != null)
			puppetVcam.transform.SetPositionAndRotation(cameraNode.position, cameraNode.rotation);
		if (rigCamera != null)
		{
			LensSettings lens = puppetVcam.m_Lens;
			lens.FieldOfView = rigCamera.fieldOfView;
			puppetVcam.m_Lens = lens;
		}

		savedBlend = brain.m_DefaultBlend;
		blendSaved = true;
		brain.m_DefaultBlend = new CinemachineBlendDefinition(
			CinemachineBlendDefinition.Style.EaseInOut, cameraBlendInTime);
	}

	private void Finish()
	{
		if (!playing) return;
		playing = false;

		// ボスは最終ポーズで固定(戻りブレンド中に登場クリップがループ再生しないように)
		if (bossAnim != null) bossAnim.speed = 0f;

		// カメラを FreeLook へブレンドで戻す。
		// 一時vcamは最後のカメラポーズを保持したまま優先度を下げ、Brainに戻りをブレンドさせる。
		if (brain != null)
			brain.m_DefaultBlend = new CinemachineBlendDefinition(
				CinemachineBlendDefinition.Style.EaseInOut, cameraBlendOutTime);
		if (puppetVcam != null) puppetVcam.Priority = -10000;

		// カメラFBXはもう不要(ポーズは一時vcamへコピー済み)
		if (cameraInstance != null) Destroy(cameraInstance);
		cameraNode = null;
		rigCamera = null;

		StartCoroutine(FinishRoutine());
	}

	private IEnumerator FinishRoutine()
	{
		// カメラの戻りブレンドを待つ間、ボスは最終ポーズを保持しておく。
		// (カメラが引きながら戻るので、登場ポーズ→戦闘アイドルの切替が目立ちにくい)
		float wait = brain != null ? cameraBlendOutTime + 0.1f : 0f;
		float deadline = Time.time + wait;
		while (Time.time < deadline) yield return null;

		RestoreBoss();
		RestorePlayer();

		// 一時vcamとブレンド設定を元に戻す
		if (brain != null && blendSaved) brain.m_DefaultBlend = savedBlend;
		blendSaved = false;
		if (puppetVcam != null) Destroy(puppetVcam.gameObject);
		puppetVcam = null;

		Debug.Log("[BossStartCutscene] 終了(操作可能・ボス行動開始)");
		enabled = false;
	}

	// ------------------------------------------------------------
	// プレイヤー: 入力/移動を止める(見た目は消さない)
	// ------------------------------------------------------------
	private void FreezePlayer()
	{
		// HUDを隠す。Nyuxtu3があればスライドアウトで滑らかにしまう(Canvasは無効化しない)
		if (hideUI)
		{
			if (Nyuxtu3.Instance != null)
			{
				Nyuxtu3.Instance.HideUI();
				nyuxtuHidden = true;
			}
			else
			{
				foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
				{
					if (c.enabled && c.isRootCanvas && c.renderMode != RenderMode.WorldSpace)
					{
						c.enabled = false;
						hiddenCanvases.Add(c);
					}
				}
			}
		}

		if (player == null) return;

		// 入力そのものを止める(移動・エイム・カメラ操作すべて)
		DisableBehaviour(player.GetComponentInChildren<UnityEngine.InputSystem.PlayerInput>());
		DisableBehaviour(player.GetComponentInChildren<PlayerMovement>());
		DisableBehaviour(player.GetComponentInChildren<PlayerAim>());
		DisableBehaviour(player.GetComponentInChildren<MagnetPull>());

		// アニメ制御を止めて、その場で立ち(Idol_v1)に固定する。
		// (歩きループのまま止まると「その場足踏み」に見えるため)
		AnimationStateController asc = player.GetComponentInChildren<AnimationStateController>();
		DisableBehaviour(asc);
		Animator pa = player.GetComponentInChildren<Animator>();
		if (pa != null && pa.runtimeAnimatorController != null && pa.HasState(0, Animator.StringToHash("Idol_v1")))
		{
			pa.SetBool("IsGrounded", true);
			pa.Play("Idol_v1", 0, 0f);
			pa.Update(0f);
		}

		// 物理で押し流されないように固定
		playerRb = player.GetComponent<Rigidbody>();
		if (playerRb != null)
		{
			playerRbWasKinematic = playerRb.isKinematic;
			playerRb.linearVelocity = Vector3.zero;
			playerRb.angularVelocity = Vector3.zero;
			playerRb.isKinematic = true;
		}
	}

	private void RestorePlayer()
	{
		if (nyuxtuHidden)
		{
			nyuxtuHidden = false;
			if (Nyuxtu3.Instance != null) Nyuxtu3.Instance.ShowUI();
		}
		foreach (Canvas c in hiddenCanvases)
			if (c != null) c.enabled = true;
		hiddenCanvases.Clear();

		if (playerRb != null) playerRb.isKinematic = playerRbWasKinematic;
		playerRb = null;

		foreach (Behaviour b in disabledPlayerBehaviours)
			if (b != null) b.enabled = true;
		disabledPlayerBehaviours.Clear();
	}

	private void DisableBehaviour(Behaviour b)
	{
		if (b != null && b.enabled)
		{
			b.enabled = false;
			disabledPlayerBehaviours.Add(b);
		}
	}

	// テンプレート(CutscenePlayback)を元に、登場クリップを差し込んだAOCを作る
	private AnimatorOverrideController BuildPlaybackController(AnimationClip clip)
	{
		var aoc = new AnimatorOverrideController(playbackTemplate);
		var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
		aoc.GetOverrides(overrides);
		for (int i = 0; i < overrides.Count; i++)
		{
			// テンプレートのClipステート(元はHumanoidのIdol_v1)だけ差し替える
			if (overrides[i].Key != null && overrides[i].Key.humanMotion)
				overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, clip);
		}
		aoc.ApplyOverrides(overrides);
		return aoc;
	}

	// カメラFBXにクリップを流す(アバターはそのまま=カメラは通常ノード再生)
	private void PlayCameraOn(GameObject go, AnimationClip clip)
	{
		Animator animator = go.GetComponent<Animator>();
		if (animator == null) animator = go.AddComponent<Animator>();
		animator.enabled = true;
		animator.applyRootMotion = true;
		animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
		animator.runtimeAnimatorController = BuildPlaybackController(clip);
		animator.Rebind();
		animator.Update(0f);
	}

	// カメラアニメFBX内の「カメラ位置ノード」を探す。
	// Cameraコンポーネントがあればそれ(描画は無効化)、無ければ名前にcameraを含むノード
	private static Transform FindCameraNode(GameObject rig)
	{
		Camera cam = rig.GetComponentInChildren<Camera>(true);
		if (cam != null)
		{
			cam.enabled = false; // 二重描画防止
			AudioListener listener = cam.GetComponent<AudioListener>();
			if (listener != null) listener.enabled = false;
			return cam.transform;
		}

		Transform found = null;
		foreach (Transform t in rig.GetComponentsInChildren<Transform>(true))
			if (t.name.ToLowerInvariant().Contains("camera")) found = t;
		return found != null ? found : rig.transform;
	}

	void OnDestroy()
	{
		// シーン破棄などで中断された場合は、乗っ取ったカメラ/ボス/プレイヤーを戻す
		if (playing)
		{
			playing = false;
			if (cameraInstance != null) Destroy(cameraInstance);
			if (brain != null && blendSaved) brain.m_DefaultBlend = savedBlend;
			if (puppetVcam != null) Destroy(puppetVcam.gameObject);
			RestoreBoss();
			RestorePlayer();
		}
	}
}
