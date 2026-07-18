using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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

	void Update()
	{
		if (playing)
		{
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

		Debug.Log("[BossEndCutscene] 撃破カットシーン終了(操作可能)");
		enabled = false;
	}

	// ------------------------------------------------------------
	// プレイヤー: 入力/移動を止める(見た目は消さない)
	// ------------------------------------------------------------
	private void FreezePlayer()
	{
		if (hideUI)
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

	void OnDestroy()
	{
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
