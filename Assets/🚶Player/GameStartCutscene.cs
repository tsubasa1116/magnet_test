using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

// ゲーム開始時に自動再生する本番用カットシーン。
// キャラはmodelMain_v4を生成し、アニメをGeneric(ボーン1:1)で再生する。
// ※Humanoid経由だと深い座りポーズが可動域クランプで浮いてしまうため、
//   あえてリターゲットせずMayaのボーンの動きをそのまま使う。
//   (アバターをnullにするとパス一致でJNTInボーンが直接動く)
// 再生中はプレイヤーを非表示+操作無効にし、
// 終了時はカメラをCinemachineのブレンドでFreeLookへ滑らかに返す。
public class GameStartCutscene : MonoBehaviour
{
	[Header("キャラ側: 演技させるモデル(modelMain_v4)とクリップ")]
	[SerializeField] private GameObject actorPrefab;
	[SerializeField] private AnimationClip actorClip;

	[Header("カメラ側FBXとクリップ")]
	[SerializeField] private GameObject cameraPrefab;
	[SerializeField] private AnimationClip cameraClip;

	[Header("再生原点(未指定ならプレイヤーの足元)")]
	[SerializeField] private Transform spawnPoint;

	[Header("再生用テンプレート(CutscenePlayback.controller)")]
	[SerializeField] private RuntimeAnimatorController playbackTemplate;

	[Header("スキップ許可(既定オフ=スキップ不可。カットシーン中は操作させない方針)")]
	[SerializeField] private bool allowSkip = false;

	[Header("再生中はHUD(スクリーンUI)を隠す")]
	[SerializeField] private bool hideUI = true;

	[Header("終了時にカメラがFreeLookへ戻るブレンド秒数")]
	[SerializeField] private float cameraBlendTime = 1.2f;

	[Header("終了時に俳優をアイドルへ繋ぐクロスフェード秒数")]
	[SerializeField] private float actorIdleBlendTime = 0.4f;

	private GameObject actorInstance;
	private Animator actorAnimator;
	private GameObject cameraInstance;
	private Transform cameraNode;
	private Camera rigCamera;
	private float endTime;
	private bool playing;

	private Camera mainCam;
	private CinemachineBrain brain; // 再生中は無効化してカメラを乗っ取る

	// プレイヤー退避用
	private GameObject player;
	private PlayerMovement playerMovement;
	private AnimationStateController stateController; // 接地確認後に返すため個別管理
	private Rigidbody playerRb;
	private bool rbWasKinematic;
	private bool playerSettled;        // カットシーン裏での事前着地が完了したか
	private Vector3 cutsceneOrigin;    // カットシーンの接地原点
	private float playerBottomOffset;  // プレイヤーroot→コライダー底面の距離(接地判定の射程用)
	private readonly List<Behaviour> disabledBehaviours = new List<Behaviour>();
	private readonly List<Renderer> hiddenRenderers = new List<Renderer>();
	private readonly List<Collider> disabledColliders = new List<Collider>();
	private readonly List<Canvas> hiddenCanvases = new List<Canvas>();

	void Start()
	{
		Play();
	}

	void Update()
	{
		if (!playing) return;

		bool skip = allowSkip && (
			Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
			Input.GetKeyDown(KeyCode.Escape) ||
			Input.GetKeyDown(KeyCode.JoystickButton0) ||
			Input.GetKeyDown(KeyCode.JoystickButton1) ||
			Input.GetKeyDown(KeyCode.JoystickButton7));

		if (skip || Time.time >= endTime) Finish();
	}

	void LateUpdate()
	{
		// カメラアニメのノードにメインカメラを重ねる(Cinemachineは停止中)
		if (playing && mainCam != null && cameraNode != null)
		{
			mainCam.transform.SetPositionAndRotation(cameraNode.position, cameraNode.rotation);

			// ズームのアニメはFOVに入っているのでそれもコピー
			if (rigCamera != null)
				mainCam.fieldOfView = rigCamera.fieldOfView;
		}
	}

	private void Play()
	{
		if (actorPrefab == null || playbackTemplate == null)
		{
			Debug.LogWarning("[GameStartCutscene] FBX参照かPlayback Templateが未設定のため再生をスキップします");
			return;
		}

		// タグ"Player"は旧プレースホルダにも付いているため、PlayerMovementを持つ本体を優先
		playerMovement = FindFirstObjectByType<PlayerMovement>();
		player = playerMovement != null ? playerMovement.gameObject : GameObject.FindGameObjectWithTag("Player");

		// 再生原点: カットシーンはMaya原点=地面で制作されているので、
		// プレイヤーの足元(接地点)を原点にする
		Vector3 origin;
		Quaternion originRot;
		if (spawnPoint != null)
		{
			origin = spawnPoint.position;
			originRot = spawnPoint.rotation;
		}
		else if (player != null)
		{
			origin = FindGroundUnderPlayer();
			originRot = player.transform.rotation;
		}
		else
		{
			origin = Vector3.zero;
			originRot = Quaternion.identity;
		}

		cutsceneOrigin = origin;

		float length = 0f;

		// キャラ側: Generic再生(アバターを外してボーンをパス一致で直接動かす)
		actorInstance = Instantiate(actorPrefab, origin, originRot);
		CopyPlayerLook(actorInstance);
		if (actorClip != null)
		{
			actorAnimator = PlayOn(actorInstance, actorClip, stripAvatar: true);
			length = Mathf.Max(length, actorClip.length);
		}
		else
		{
			Debug.LogWarning("[GameStartCutscene] Actor Clip が未設定のためキャラは棒立ちです");
		}

		// カメラ側
		if (cameraPrefab != null)
		{
			cameraInstance = Instantiate(cameraPrefab, origin, originRot);
			if (cameraClip != null)
			{
				PlayOn(cameraInstance, cameraClip, stripAvatar: false);
				length = Mathf.Max(length, cameraClip.length);
			}
			cameraNode = FindCameraNode(cameraInstance);
			rigCamera = cameraNode != null ? cameraNode.GetComponent<Camera>() : null;
		}

		HidePlayer();

		// 「プレイ開始→カットシーン→プレイ再開」方式:
		// プレイヤーは非表示のままカットシーンの裏で先に落下・着地させておく。
		// (初期配置は埋まり防止で空中にあるため。終了時にはもう落下要素が無い)
		StartCoroutine(SettlePlayerEarly());

		// メインカメラを乗っ取る
		mainCam = Camera.main;
		if (mainCam != null)
		{
			brain = mainCam.GetComponent<CinemachineBrain>();
			if (brain != null) brain.enabled = false;
		}

		float duration = length > 0f ? length : 5f;
		endTime = Time.time + duration;
		playing = true;
		Debug.Log($"[GameStartCutscene] 再生開始 ({duration:F1}秒)");
	}

	// プレイヤーの足元の接地点を求める(自分自身のコライダーは無視)
	private Vector3 FindGroundUnderPlayer()
	{
		Vector3 pos = player.transform.position;
		RaycastHit best = default;
		bool found = false;
		foreach (RaycastHit hit in Physics.RaycastAll(
			pos + Vector3.up * 0.5f, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore))
		{
			if (hit.collider.transform.IsChildOf(player.transform)) continue;
			if (!found || hit.distance < best.distance)
			{
				best = hit;
				found = true;
			}
		}
		return found ? best.point : pos;
	}

	// モデルFBXにはゲーム用マテリアルが入っていないので、
	// ゲーム中のプレイヤーと同じ見た目(マテリアル)を俳優へコピーする
	private void CopyPlayerLook(GameObject actor)
	{
		if (player == null || actor == null) return;

		SkinnedMeshRenderer[] playerRenderers = player.GetComponentsInChildren<SkinnedMeshRenderer>(true);
		if (playerRenderers.Length == 0) return;

		foreach (SkinnedMeshRenderer ar in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
		{
			SkinnedMeshRenderer match = null;
			foreach (SkinnedMeshRenderer pr in playerRenderers)
			{
				if (pr.name == ar.name) { match = pr; break; }
			}
			// 名前一致が無ければ先頭のレンダラーで代用(同一モデル前提)
			ar.sharedMaterials = (match != null ? match : playerRenderers[0]).sharedMaterials;
		}
	}

	private void HidePlayer()
	{
		// HUDを隠す(カットシーン映像に集中させる)
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

		// 着地高さの計算用: root位置からコライダー底面までの距離を測っておく
		// (コライダー無効化前でないと測れない)
		float minY = float.PositiveInfinity;
		foreach (Collider c in player.GetComponentsInChildren<Collider>())
			if (c.enabled && !c.isTrigger) minY = Mathf.Min(minY, c.bounds.min.y);
		playerBottomOffset = float.IsPositiveInfinity(minY) ? 0f : player.transform.position.y - minY;

		// 操作系スクリプトを止める。PlayerInput も止めて入力そのものを遮断する
		// (移動・エイム・磁石だけでなく、カメラ操作の入力も受け付けないようにする)
		foreach (Behaviour b in new Behaviour[] {
			player.GetComponentInChildren<UnityEngine.InputSystem.PlayerInput>(),
			player.GetComponentInChildren<PlayerMovement>(),
			player.GetComponentInChildren<PlayerAim>(),
			player.GetComponentInChildren<MagnetPull>() })
		{
			if (b != null && b.enabled)
			{
				b.enabled = false;
				disabledBehaviours.Add(b);
			}
		}

		// アニメ制御は接地確認後に返すため個別に止める
		stateController = player.GetComponentInChildren<AnimationStateController>();
		if (stateController != null && stateController.enabled) stateController.enabled = false;
		else stateController = null;

		// 物理は生かしたまま(事前着地させるため)。剛体だけ覚えておく
		playerRb = player.GetComponent<Rigidbody>();

		// 見た目を消す(カットシーン俳優と二重に見えないように)
		foreach (Renderer r in player.GetComponentsInChildren<Renderer>())
		{
			if (r.enabled)
			{
				r.enabled = false;
				hiddenRenderers.Add(r);
			}
		}
	}

	// カットシーンの裏でプレイヤーを落下・着地させ、着地したら固定する。
	// 通常のプレイ開始時と同じ落下を先に済ませる「プレイ開始→カットシーン→再開」方式
	private IEnumerator SettlePlayerEarly()
	{
		if (player == null)
		{
			playerSettled = true;
			yield break;
		}

		if (playerRb != null && !playerRb.isKinematic)
		{
			float deadline = Time.time + 2f;
			while (Time.time < deadline)
			{
				if (IsPlayerGroundedNow() && Mathf.Abs(playerRb.linearVelocity.y) < 0.05f) break;
				yield return new WaitForFixedUpdate();
			}
		}

		// 着地した位置で固定(カットシーン中に敵や物理に動かされないように)
		if (playerRb != null)
		{
			rbWasKinematic = playerRb.isKinematic;
			playerRb.linearVelocity = Vector3.zero;
			playerRb.angularVelocity = Vector3.zero;
			playerRb.isKinematic = true;
		}

		// 当たり判定も消す(カットシーン中に敵の攻撃を受けないように)
		foreach (Collider c in player.GetComponentsInChildren<Collider>())
		{
			if (c.enabled)
			{
				c.enabled = false;
				disabledColliders.Add(c);
			}
		}

		playerSettled = true;
	}

	// プレイヤーが接地しているか(自分のコライダーは無視)
	private bool IsPlayerGroundedNow()
	{
		foreach (RaycastHit hit in Physics.SphereCastAll(
			player.transform.position + Vector3.up * 0.3f, 0.25f, Vector3.down,
			0.4f + playerBottomOffset, ~0, QueryTriggerInteraction.Ignore))
		{
			if (!hit.collider.transform.IsChildOf(player.transform)) return true;
		}
		return false;
	}

	// 物理(コライダーと剛体)だけ先に返す。冪等なので複数回呼んでも安全
	private void RestorePhysics()
	{
		foreach (Collider c in disabledColliders)
			if (c != null) c.enabled = true;
		disabledColliders.Clear();

		if (playerRb != null)
			playerRb.isKinematic = rbWasKinematic;
	}

	private void RestorePlayer()
	{
		RestorePhysics();

		foreach (Canvas c in hiddenCanvases)
			if (c != null) c.enabled = true;
		hiddenCanvases.Clear();

		foreach (Renderer r in hiddenRenderers)
			if (r != null) r.enabled = true;
		hiddenRenderers.Clear();

		playerRb = null;

		foreach (Behaviour b in disabledBehaviours)
			if (b != null) b.enabled = true;
		disabledBehaviours.Clear();
	}

	private void Finish()
	{
		if (!playing) return;
		playing = false;

		if (cameraInstance != null) Destroy(cameraInstance);
		cameraNode = null;
		rigCamera = null;

		// カメラは現在位置からFreeLookへブレンドで返し、
		// キャラは俳優をアイドルへ繋いでからプレイヤーと入れ替える
		StartCoroutine(BlendCameraBack());
		StartCoroutine(SwapBackToPlayer());
	}

	// 「シュン!」対策:
	// 1. 俳優をその場でアイドルアニメへクロスフェード(ポーズが滑らかに立ちへ移る)
	// 2. プレイヤーのアイドルを俳優と同じ再生位置に合わせてから入れ替える
	//    (同じアニメの同じタイミング同士で交代するので切り替わりが見えない)
	private IEnumerator SwapBackToPlayer()
	{
		bool crossfaded = actorInstance != null && actorAnimator != null && actorIdleBlendTime > 0f
			&& actorAnimator.HasState(0, Animator.StringToHash("Idle"));
		if (crossfaded)
		{
			actorAnimator.CrossFadeInFixedTime("Idle", actorIdleBlendTime, 0);
			yield return new WaitForSeconds(actorIdleBlendTime + 0.05f);
		}

		Animator playerAnimator = player != null ? player.GetComponentInChildren<Animator>() : null;

		// 事前着地(カットシーン裏の落下)がまだなら完了を待つ(スキップ連打対策)
		float settleWait = Time.time + 2.5f;
		while (!playerSettled && Time.time < settleWait)
			yield return null;

		// 向き: 俳優の最終向きへY軸回転だけ合わせる(位置は事前着地した場所をそのまま使う)
		if (player != null && actorInstance != null)
		{
			Transform playerModel = playerAnimator != null ? playerAnimator.transform : player.transform;
			Vector3 pf = Vector3.ProjectOnPlane(playerModel.forward, Vector3.up);
			Vector3 af = Vector3.ProjectOnPlane(actorInstance.transform.forward, Vector3.up);
			if (pf.sqrMagnitude > 0.001f && af.sqrMagnitude > 0.001f)
				player.transform.Rotate(0f, Vector3.SignedAngle(pf, af, Vector3.up), 0f, Space.World);
		}

		// アイドルを「接地状態」で、俳優と同じ再生位置から開始(同ポーズ交代で切り替わりを消す)
		if (playerAnimator != null)
		{
			playerAnimator.SetBool("IsGrounded", true);
			float phase = 0f;
			if (crossfaded && actorAnimator != null)
			{
				AnimatorStateInfo info = actorAnimator.GetCurrentAnimatorStateInfo(0);
				if (info.IsName("Idle")) phase = info.normalizedTime % 1f;
			}
			playerAnimator.Play("Idol_v1", 0, phase);
			playerAnimator.Update(0f);
		}

		if (actorInstance != null) Destroy(actorInstance);
		actorAnimator = null;

		RestorePlayer();

		// 接地判定が安定するのを待ってからアニメ制御を返す(空中アニメの一瞬混入を防ぐ)
		float deadline = Time.time + 0.5f;
		while (Time.time < deadline && playerMovement != null && !playerMovement.IsGrounded)
			yield return null;
		if (stateController != null)
		{
			stateController.enabled = true;
			stateController = null;
		}

		Debug.Log("[GameStartCutscene] 終了(操作可能)");
		enabled = false;
	}

	// カットシーン最終カメラ位置に一時的な仮想カメラを立て、
	// そこからFreeLookへCinemachineのブレンドで滑らかに返す
	private IEnumerator BlendCameraBack()
	{
		CinemachineBrain b = brain;
		brain = null;
		if (b == null) yield break;

		if (mainCam == null || cameraBlendTime <= 0f)
		{
			b.enabled = true;
			yield break;
		}

		GameObject temp = new GameObject("CutsceneExitCamera");
		temp.transform.SetPositionAndRotation(mainCam.transform.position, mainCam.transform.rotation);
		CinemachineVirtualCamera vcam = temp.AddComponent<CinemachineVirtualCamera>();
		vcam.Priority = 9999;
		LensSettings lens = vcam.m_Lens;
		lens.FieldOfView = mainCam.fieldOfView;
		vcam.m_Lens = lens;

		CinemachineBlendDefinition prevBlend = b.m_DefaultBlend;
		b.m_DefaultBlend = new CinemachineBlendDefinition(
			CinemachineBlendDefinition.Style.EaseInOut, cameraBlendTime);
		b.enabled = true;

		yield return null;      // 一時カメラがライブになる(現在の画と同じなので変化なし)
		vcam.Priority = -9999;  // FreeLookへのブレンド開始

		yield return new WaitForSeconds(cameraBlendTime + 0.2f);
		b.m_DefaultBlend = prevBlend;
		Destroy(temp);
	}

	void OnDestroy()
	{
		// シーン破棄などで中断された場合はブレンド無しで即復帰
		if (playing)
		{
			playing = false;
			if (actorInstance != null) Destroy(actorInstance);
			if (cameraInstance != null) Destroy(cameraInstance);
			RestorePlayer();
			if (brain != null) brain.enabled = true;
		}
		if (stateController != null)
		{
			stateController.enabled = true;
			stateController = null;
		}
	}

	// 通常のAnimator再生でクリップを流す(CutsceneTestと同じ経路)。
	// stripAvatar=trueでアバターを外し、Genericのパス一致でボーンを直接動かす
	private Animator PlayOn(GameObject go, AnimationClip clip, bool stripAvatar)
	{
		Animator animator = go.GetComponent<Animator>();
		if (animator == null) animator = go.AddComponent<Animator>();
		animator.enabled = true;
		if (stripAvatar) animator.avatar = null;
		animator.applyRootMotion = true;
		animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

		var aoc = new AnimatorOverrideController(playbackTemplate);
		var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
		aoc.GetOverrides(overrides);
		for (int i = 0; i < overrides.Count; i++)
		{
			// テンプレートのClipステート(元クリップはHumanoidのIdol_v1.anim)だけ差し替える。
			// IdleステートのGenericアイドルは終了時のクロスフェード用に残す
			if (overrides[i].Key != null && overrides[i].Key.humanMotion)
				overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, clip);
		}
		aoc.ApplyOverrides(overrides);

		animator.runtimeAnimatorController = aoc;
		animator.Rebind();
		animator.Update(0f);
		return animator;
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
}
