using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cinemachine;

// ボス撃破カットシーン。ボスのHPが0になったら自動再生する(一度だけ)。
//
// BossStartCutscene と同じ方式で、シーン上の本物のボス(enemy_Boss)本体を演技させる:
//   - ボスのAnimatorのコントローラを一時的に撃破クリップ(animationBossEnd_v1)へ差し替え、
//     Generic(アバターnull)のパス一致で再生。cm単位リグの巨大化はスケール固定で防ぐ。
//   - 分離中の腕は再生前に本体へ戻す(撃破アニメは本体の腕ごと演技するため)。
//   - カメラは cameraBossEnd_v1 のカメラノードを一時vcamで追従させ、Cinemachineブレンドで
//     戦闘カメラから「滑らかに入り」「滑らかに戻す」。
//   - スキップ不可・操作不可(再生中はプレイヤーの入力/移動を止める)。
//   - 終了後: ボスはクリップ最終ポーズ(本体消失・両手が地面に残る)のまま停止し、復活しない。
//
// デバッグ: debugKillKey(既定K)でボスHPを0にして即撃破テストできる。
public class BossEndCutscene : MonoBehaviour
{
	[Header("ボスに再生させるクリップ(animationBossEnd_v1)")]
	[SerializeField] private AnimationClip actorClip;

	[Header("カメラ側FBX(cameraBossEnd_v1)とクリップ")]
	[SerializeField] private GameObject cameraPrefab;
	[SerializeField] private AnimationClip cameraClip;

	[Header("再生用テンプレート(CutscenePlayback.controller)")]
	[SerializeField] private RuntimeAnimatorController playbackTemplate;

	[Header("演出: 白フラッシュ(クリップのフレーム番号・昇順で)")]
	[Tooltip("この各フレームで画面を白く光らせる(カットの切り替わり用)")]
	[SerializeField] private float[] flashFrames = { 6f, 35f, 45f };
	[Tooltip("白から透明へ戻る時間(秒)")]
	[SerializeField] private float flashFadeTime = 0.25f;
	[SerializeField, Range(0f, 1f)] private float flashMaxAlpha = 1f;

	[Header("演出: 消滅球体(ボスを包んで消す)")]
	[Tooltip("球体が広がり始めるクリップのフレーム番号")]
	[SerializeField] private float sphereStartFrame = 150f;
	[Tooltip("最大まで広がる時間(秒)")]
	[SerializeField] private float sphereExpandTime = 0.45f;
	[Tooltip("最大のまま保持する時間(秒)。この間(包まれている間)にボスを消す")]
	[SerializeField] private float sphereHoldTime = 0.2f;
	[Tooltip("しぼんで消える時間(秒)")]
	[SerializeField] private float sphereShrinkTime = 0.55f;
	[Tooltip("球体の最大半径(ボスがすっぽり入る大きさ)")]
	[SerializeField] private float sphereMaxRadius = 8f;
	[SerializeField] private Color sphereColor = new Color(1f, 1f, 1f, 0.95f);
	[Tooltip("球に貼るマテリアル(ホログラム等)。未指定なら sphereColor の単色球")]
	[SerializeField] private Material sphereMaterial;
	[Tooltip("独自の球体エフェクトを使う場合に指定(直径1で作ること。指定時は sphereMaterial より優先)")]
	[SerializeField] private GameObject spherePrefab;

	[Header("対象ボス(未指定ならシーンで最も近い enemy_Boss)")]
	[SerializeField] private enemy_Boss boss;

	[Header("カメラ生成アンカー(未指定ならボスの位置)")]
	[SerializeField] private Transform spawnPoint;

	[Header("カメラがカットシーンへ入る/戻るブレンド秒数")]
	[SerializeField] private float cameraBlendInTime = 1.0f;
	[SerializeField] private float cameraBlendOutTime = 1.2f;

	[Header("再生中はHUD(スクリーンUI)を隠す")]
	[SerializeField] private bool hideUI = true;

	[Header("デバッグ: このキーでボスHPを0にして撃破カットシーンを起動(Noneで無効)")]
	[SerializeField] private KeyCode debugKillKey = KeyCode.K;

	// 進行状態
	private bool hasPlayed;
	private bool playing;
	private float endTime;
	private float cutsceneStartTime;
	private float clipFPS = 30f;

	// 演出の状態
	private int nextFlashIndex;
	private bool sphereTriggered;
	private GameObject flashCanvasGO;
	private Image flashImage;
	private GameObject vanishSphere;
	private Material vanishSphereMat;

	// カメラFBX
	private GameObject cameraInstance;
	private Transform cameraNode;   // カメラアニメ内の追従対象ノード
	private Camera rigCamera;       // FOVのコピー元

	// カメラ乗っ取り
	private Camera mainCam;
	private CinemachineBrain brain;
	private CinemachineVirtualCamera puppetVcam;
	private CinemachineBlendDefinition savedBlend;
	private bool blendSaved;

	// プレイヤー退避
	private GameObject player;
	private readonly List<Behaviour> disabledPlayerBehaviours = new List<Behaviour>();
	private Rigidbody playerRb;
	private bool playerRbWasKinematic;
	private bool playerFrozen;

	// ボス乗っ取り(撃破後は戻さない)
	private Animator bossAnim;
	private bool bossTakenOver;

	// スケール固定用(cmリグ補正の巨大化防止。BossStartCutsceneと同じ対策)
	private readonly List<Transform> bossScaleBones = new List<Transform>();
	private readonly List<Vector3> bossScaleValues = new List<Vector3>();

	// UI退避
	private readonly List<Canvas> hiddenCanvases = new List<Canvas>();
	private bool nyuxtuHidden; // Nyuxtu3でHUDをしまったか(終了時にShowUIで返す)

	void Update()
	{
		if (playing)
		{
			UpdateCutsceneEffects();

			// スキップ入力は受け付けない。尺が来たら終わるだけ
			if (Time.time >= endTime) Finish();
			return;
		}

		if (hasPlayed) return;

		ResolveBoss();
		if (boss == null) return;

		// デバッグ: HPを0にして即撃破(戦闘開始前でも起動できる)
		if (debugKillKey != KeyCode.None && Input.GetKeyDown(debugKillKey))
		{
			Debug.Log("[BossEndCutscene] デバッグ: ボスHPを0にして撃破カットシーンを起動します");
			boss.currentHP = 0f;
			Play();
			return;
		}

		// 本番トリガー: 戦闘が始まっていて、HPが0になったら撃破
		// (isStartAction前はHP未初期化(0)のため誤発火しないようガード)
		if (boss.isStartAction && boss.currentHP <= 0f) Play();
	}

	void LateUpdate()
	{
		// Animatorがボーンを動かした後にスケールだけ元へ戻す(描画前)
		if (bossTakenOver && bossAnim != null && bossAnim.enabled) PinBossScales();

		if (!playing) return;

		if (puppetVcam != null && cameraNode != null)
		{
			puppetVcam.transform.SetPositionAndRotation(cameraNode.position, cameraNode.rotation);
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
			if (rigCamera != null) mainCam.fieldOfView = rigCamera.fieldOfView;
		}
	}

	// 対象ボスの解決: 未指定なら最も近い enemy_Boss
	private void ResolveBoss()
	{
		if (boss != null) return;
		float best = float.MaxValue;
		foreach (enemy_Boss b in FindObjectsByType<enemy_Boss>(FindObjectsSortMode.None))
		{
			float d = (b.transform.position - transform.position).sqrMagnitude;
			if (d < best) { best = d; boss = b; }
		}
	}

	public void Play()
	{
		if (hasPlayed || playing) return;
		if (actorClip == null || playbackTemplate == null)
		{
			Debug.LogWarning("[BossEndCutscene] actorClip / playbackTemplate が未設定のため再生できません");
			return;
		}
		ResolveBoss();
		if (boss == null)
		{
			Debug.LogWarning("[BossEndCutscene] ボス(enemy_Boss)が見つかりません");
			return;
		}
		hasPlayed = true;

		PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
		player = pm != null ? pm.gameObject : GameObject.FindGameObjectWithTag("Player");

		Transform anchor = spawnPoint != null ? spawnPoint : boss.transform;
		Vector3 anchorPos = anchor.position;
		Quaternion anchorRot = anchor.rotation;

		FreezePlayer();

		float length = actorClip.length;

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
			Debug.LogWarning("[BossEndCutscene] Camera Prefab が未設定のためカメラは動きません");
		}

		BeginCameraTakeover();

		endTime = Time.time + (length > 0f ? length : 5f);
		cutsceneStartTime = Time.time;
		clipFPS = actorClip != null && actorClip.frameRate > 0f ? actorClip.frameRate : 30f;
		nextFlashIndex = 0;
		sphereTriggered = false;
		playing = true;
		Debug.Log($"[BossEndCutscene] 撃破カットシーン再生開始 ({length:F1}秒)");
	}

	// ------------------------------------------------------------
	// ボス: 撃破クリップを本体で再生(戻さない)
	// ------------------------------------------------------------
	private void BeginBossPlayback()
	{
		bossAnim = boss.GetComponent<Animator>();
		if (bossAnim == null) bossAnim = boss.GetComponentInChildren<Animator>(true);
		if (bossAnim == null)
		{
			Debug.LogWarning("[BossEndCutscene] ボスにAnimatorが無いため演技させられません");
			return;
		}

		// 分離中の腕を本体へ戻す(撃破アニメは本体の腕ごと演技する)。
		// ReviveArm内のスケール演出コルーチンごと止めて、腕ボーンを確実に等倍へ
		boss.ReviveArm();
		boss.ReviveArmR();

		// 行動・タイマー・召喚などを完全停止(撃破後に復活処理が走らないように)
		boss.StopAllCoroutines();
		boss.enabled = false;
		if (boss.armBone_L != null) boss.armBone_L.localScale = Vector3.one;
		if (boss.armBone_R != null) boss.armBone_R.localScale = Vector3.one;

		// バリアが出ていたら消す(演出の邪魔になるため)
		foreach (Transform t in boss.GetComponentsInChildren<Transform>(true))
		{
			string n = t.name.ToLowerInvariant();
			if (n.Contains("shield") || n.Contains("barrier"))
			{
				t.gameObject.SetActive(false);
				break;
			}
		}

		// 通常サイズの各ボーンのlocalScaleを控える(再生中は毎フレーム戻す)
		bossScaleBones.Clear();
		bossScaleValues.Clear();
		foreach (Transform t in boss.GetComponentsInChildren<Transform>(true))
		{
			bossScaleBones.Add(t);
			bossScaleValues.Add(t.localScale);
		}
		bossTakenOver = true;

		bossAnim.enabled = true;
		bossAnim.avatar = null;
		bossAnim.applyRootMotion = true;
		bossAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

		bossAnim.runtimeAnimatorController = BuildPlaybackController(actorClip);
		bossAnim.Rebind();
		bossAnim.Update(0f);
		PinBossScales();
	}

	private void PinBossScales()
	{
		for (int i = 0; i < bossScaleBones.Count; i++)
			if (bossScaleBones[i] != null)
				bossScaleBones[i].localScale = bossScaleValues[i];
	}

	// ------------------------------------------------------------
	// カメラ乗っ取り(ブレンドイン)
	// ------------------------------------------------------------
	private void BeginCameraTakeover()
	{
		mainCam = Camera.main;
		brain = mainCam != null ? mainCam.GetComponent<CinemachineBrain>() : null;

		if (brain == null) return; // フォールバック(LateUpdateで直接重ねる)

		GameObject go = new GameObject("BossEndCutsceneCamera");
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

		// ボスはクリップ最終ポーズ(本体消失・両手が残る)のまま完全停止。
		// Animator自体を止めることで、以後ボーンへの書き込みが起きない
		// (最後のPinBossScales済みの状態が保持される)
		if (bossAnim != null)
		{
			bossAnim.Update(0f); // 最終フレームを確実に評価
			PinBossScales();
			bossAnim.enabled = false;
		}

		// カメラを戦闘カメラへブレンドで戻す
		if (brain != null)
			brain.m_DefaultBlend = new CinemachineBlendDefinition(
				CinemachineBlendDefinition.Style.EaseInOut, cameraBlendOutTime);
		if (puppetVcam != null) puppetVcam.Priority = -10000;

		if (cameraInstance != null) Destroy(cameraInstance);
		cameraNode = null;
		rigCamera = null;

		StartCoroutine(FinishRoutine());
	}

	private IEnumerator FinishRoutine()
	{
		float wait = brain != null ? cameraBlendOutTime + 0.1f : 0f;
		float deadline = Time.time + wait;
		while (Time.time < deadline) yield return null;

		RestorePlayer();

		if (brain != null && blendSaved) brain.m_DefaultBlend = savedBlend;
		blendSaved = false;
		if (puppetVcam != null) Destroy(puppetVcam.gameObject);
		puppetVcam = null;

		// フラッシュ用Canvasを片付ける(球体は自分のコルーチンが最後まで面倒を見る)
		if (flashCanvasGO != null) Destroy(flashCanvasGO);
		flashCanvasGO = null;
		flashImage = null;

		Debug.Log("[BossEndCutscene] 撃破カットシーン終了(操作可能)");
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
		playerFrozen = true;

		// 入力そのものを止める(移動・エイム・磁石・カメラ操作すべて)
		DisableBehaviour(player.GetComponentInChildren<UnityEngine.InputSystem.PlayerInput>());
		DisableBehaviour(player.GetComponentInChildren<PlayerMovement>());
		DisableBehaviour(player.GetComponentInChildren<PlayerAim>());
		DisableBehaviour(player.GetComponentInChildren<MagnetPull>());

		// 戦闘中の撃破は空中(スイング中など)の可能性があるため、
		// すぐには固定せず「接地してから」固定する
		playerRb = player.GetComponent<Rigidbody>();
		if (playerRb != null)
		{
			playerRbWasKinematic = playerRb.isKinematic;
			playerRb.linearVelocity = Vector3.zero;
			playerRb.angularVelocity = Vector3.zero;
		}
		StartCoroutine(SettleThenFreeze());
	}

	// 接地を待ってから物理を固定し、その場アイドルにする
	private IEnumerator SettleThenFreeze()
	{
		if (playerRb != null && !playerRb.isKinematic)
		{
			float deadline = Time.time + 2f;
			while (Time.time < deadline && playerFrozen)
			{
				if (IsPlayerGroundedNow() && Mathf.Abs(playerRb.linearVelocity.y) < 0.05f) break;
				yield return new WaitForFixedUpdate();
			}
		}
		if (!playerFrozen) yield break; // すでに復帰済みなら何もしない

		if (playerRb != null)
		{
			playerRb.linearVelocity = Vector3.zero;
			playerRb.angularVelocity = Vector3.zero;
			playerRb.isKinematic = true;
		}

		// 着地後にその場アイドルへ固定(足踏み・空中ポーズ防止)
		AnimationStateController asc = player.GetComponentInChildren<AnimationStateController>();
		DisableBehaviour(asc);
		Animator pa = player.GetComponentInChildren<Animator>();
		if (pa != null && pa.runtimeAnimatorController != null && pa.HasState(0, Animator.StringToHash("Idol_v1")))
		{
			pa.SetBool("IsGrounded", true);
			pa.Play("Idol_v1", 0, 0f);
			pa.Update(0f);
		}
	}

	// プレイヤーが接地しているか(自分のコライダーは無視)
	private bool IsPlayerGroundedNow()
	{
		foreach (RaycastHit hit in Physics.SphereCastAll(
			player.transform.position + Vector3.up * 0.3f, 0.25f, Vector3.down,
			1.0f, ~0, QueryTriggerInteraction.Ignore))
		{
			if (!hit.collider.transform.IsChildOf(player.transform)) return true;
		}
		return false;
	}

	private void RestorePlayer()
	{
		playerFrozen = false;

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

	// テンプレート(CutscenePlayback)を元に、撃破クリップを差し込んだAOCを作る
	private AnimatorOverrideController BuildPlaybackController(AnimationClip clip)
	{
		var aoc = new AnimatorOverrideController(playbackTemplate);
		var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
		aoc.GetOverrides(overrides);
		for (int i = 0; i < overrides.Count; i++)
		{
			if (overrides[i].Key != null && overrides[i].Key.humanMotion)
				overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, clip);
		}
		aoc.ApplyOverrides(overrides);
		return aoc;
	}

	// カメラFBXにクリップを流す
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

	// カメラアニメFBX内の「カメラ位置ノード」を探す
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

	// ------------------------------------------------------------
	// カットシーン中の演出(白フラッシュ・消滅球体)
	// ------------------------------------------------------------
	private void UpdateCutsceneEffects()
	{
		float elapsedFrames = (Time.time - cutsceneStartTime) * clipFPS;

		// 指定フレームに達したら白フラッシュ(昇順前提)
		while (nextFlashIndex < flashFrames.Length && elapsedFrames >= flashFrames[nextFlashIndex])
		{
			TriggerFlash();
			nextFlashIndex++;
		}

		// フラッシュの戻り(白→透明)
		if (flashImage != null && flashImage.color.a > 0f)
		{
			float a = flashImage.color.a - Time.deltaTime / Mathf.Max(flashFadeTime, 0.01f) * flashMaxAlpha;
			flashImage.color = new Color(1f, 1f, 1f, Mathf.Max(0f, a));
		}

		// 消滅球体の開始
		if (!sphereTriggered && elapsedFrames >= sphereStartFrame)
		{
			sphereTriggered = true;
			StartCoroutine(SphereSwallow());
		}
	}

	// 画面全体を白く光らせる(初回にオーバーレイCanvasを生成)
	private void TriggerFlash()
	{
		if (flashImage == null)
		{
			flashCanvasGO = new GameObject("BossEndFlash");
			Canvas canvas = flashCanvasGO.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = 30000; // 最前面

			GameObject imgGO = new GameObject("White");
			imgGO.transform.SetParent(flashCanvasGO.transform, false);
			flashImage = imgGO.AddComponent<Image>();
			RectTransform rt = flashImage.rectTransform;
			rt.anchorMin = Vector2.zero;
			rt.anchorMax = Vector2.one;
			rt.offsetMin = Vector2.zero;
			rt.offsetMax = Vector2.zero;
		}
		flashImage.color = new Color(1f, 1f, 1f, flashMaxAlpha);
	}

	// 球体がボスを包むように広がる → 包んだ間にボスを消す → しぼんで消える
	private IEnumerator SphereSwallow()
	{
		Vector3 center = GetBossFeet();

		if (spherePrefab != null)
		{
			vanishSphere = Instantiate(spherePrefab, center, Quaternion.identity);
		}
		else
		{
			// 球を自動生成。sphereMaterial(ホログラム等)があればそれを貼り、
			// 無ければ sphereColor の単色球(仮ビジュアル)
			vanishSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			vanishSphere.name = "BossVanishSphere";
			Destroy(vanishSphere.GetComponent<Collider>());
			vanishSphere.transform.position = center;
			MeshRenderer mr = vanishSphere.GetComponent<MeshRenderer>();
			if (sphereMaterial != null)
			{
				mr.sharedMaterial = sphereMaterial;
			}
			else
			{
				vanishSphereMat = new Material(Shader.Find("Sprites/Default"));
				vanishSphereMat.color = sphereColor;
				mr.sharedMaterial = vanishSphereMat;
			}
			mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			mr.receiveShadows = false;
		}
		vanishSphere.transform.localScale = Vector3.zero;

		// 広がる: 早く→ゆっくり(3乗イーズアウト。勢いよく出て、最大サイズへ滑らかに収まる)
		float t = 0f;
		while (t < sphereExpandTime)
		{
			t += Time.deltaTime;
			float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / sphereExpandTime), 3f);
			vanishSphere.transform.localScale = Vector3.one * (sphereMaxRadius * k * 2f);
			yield return null;
		}
		vanishSphere.transform.localScale = Vector3.one * (sphereMaxRadius * 2f);

		// 完全に包まれている間にボスを消す(球がしぼんだ時には居ない)。
		// 球は加算半透明で中のボスが透けて見えるため、消す瞬間に白フラッシュを重ねて隠す
		TriggerFlash();
		if (boss != null) boss.gameObject.SetActive(false);

		float h = 0f;
		while (h < sphereHoldTime)
		{
			h += Time.deltaTime;
			yield return null;
		}

		// しぼむ: ためて一気に(3乗イーズイン。じわっと縮み始めて加速し、シュッと消える)
		t = 0f;
		while (t < sphereShrinkTime)
		{
			t += Time.deltaTime;
			float k = Mathf.Pow(Mathf.Clamp01(t / sphereShrinkTime), 3f);
			vanishSphere.transform.localScale = Vector3.one * (sphereMaxRadius * (1f - k) * 2f);
			yield return null;
		}

		Destroy(vanishSphere);
		vanishSphere = null;
		if (vanishSphereMat != null)
		{
			Destroy(vanishSphereMat);
			vanishSphereMat = null;
		}
	}

	// ボスの足元(レンダラー境界の底面中心)。球体の膨らむ中心に使う
	private Vector3 GetBossFeet()
	{
		if (boss == null) return transform.position;
		Bounds b = new Bounds(boss.transform.position, Vector3.zero);
		bool has = false;
		foreach (Renderer r in boss.GetComponentsInChildren<Renderer>())
		{
			if (!has) { b = r.bounds; has = true; }
			else b.Encapsulate(r.bounds);
		}
		if (!has) return boss.transform.position;
		return new Vector3(b.center.x, b.min.y, b.center.z);
	}

	// 演出まわりの後片付け
	private void CleanupEffects()
	{
		if (flashCanvasGO != null) Destroy(flashCanvasGO);
		flashCanvasGO = null;
		flashImage = null;
		if (vanishSphere != null) Destroy(vanishSphere);
		vanishSphere = null;
		if (vanishSphereMat != null) Destroy(vanishSphereMat);
		vanishSphereMat = null;
	}

	void OnDestroy()
	{
		CleanupEffects();
		if (playing)
		{
			playing = false;
			if (cameraInstance != null) Destroy(cameraInstance);
			if (brain != null && blendSaved) brain.m_DefaultBlend = savedBlend;
			if (puppetVcam != null) Destroy(puppetVcam.gameObject);
			RestorePlayer();
		}
	}
}
