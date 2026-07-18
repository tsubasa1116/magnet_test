using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

// カットシーンのテスト再生ハーネス(仮)。
//   1 = GameStart / 2 = BossStart / 3 = BossEnd / 0 = 中断
// モデルアニメFBXとカメラアニメFBXをその場に生成して同時再生し、
// 再生中はメインカメラをカメラアニメ内のカメラノードに追従させる。
// Timeline化するまでの動作確認用。
public class CutsceneTest : MonoBehaviour
{
	[System.Serializable]
	public class Cutscene
	{
		public string name;
		public GameObject actorPrefab;   // キャラ側FBX
		public AnimationClip actorClip;  // その中のアニメクリップ
		public GameObject cameraPrefab;  // カメラ側FBX
		public AnimationClip cameraClip; // その中のカメラアニメ
	}

	[Header("カットシーン一覧(キー1/2/3に対応)")]
	[SerializeField] private Cutscene[] cutscenes = new Cutscene[3];

	[Header("再生位置(未指定ならプレイヤー位置、それも無ければ原点)")]
	[SerializeField] private Transform spawnPoint;

	[Header("再生用テンプレート(CutscenePlayback.controllerを割り当てる)")]
	[SerializeField] private RuntimeAnimatorController playbackTemplate;

	private GameObject actorInstance;
	private GameObject cameraInstance;
	private Transform cameraNode;    // カメラアニメ内の追従対象ノード
	private Camera rigCamera;        // カメラアニメ内のCameraコンポーネント(FOVのコピー元)
	private float startTime;
	private float clipLength;
	private bool playing;

	private Camera mainCam;
	private Behaviour brain; // CinemachineBrain(再生中は無効化してカメラを乗っ取る)

	void Update()
	{
		if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) Play(0);
		if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) Play(1);
		if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) Play(2);
		if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0)) StopCutscene();

		if (playing && Time.time - startTime >= clipLength) StopCutscene();
	}

	void LateUpdate()
	{
		// カメラアニメのノードにメインカメラを重ねる(Cinemachineは停止中)
		if (playing && mainCam != null && cameraNode != null)
		{
			mainCam.transform.SetPositionAndRotation(cameraNode.position, cameraNode.rotation);

			// ズーム(焦点距離)のアニメはFOVに入っているので、それもコピーする
			if (rigCamera != null)
				mainCam.fieldOfView = rigCamera.fieldOfView;
		}
	}

	private void Play(int index)
	{
		if (cutscenes == null || index >= cutscenes.Length)
		{
			Debug.LogWarning($"[CutsceneTest] カットシーン{index + 1}が未設定");
			return;
		}
		Cutscene cs = cutscenes[index];
		if (cs == null || cs.actorPrefab == null)
		{
			Debug.LogWarning($"[CutsceneTest] カットシーン{index + 1}のFBX参照が空(Inspectorを確認)");
			return;
		}

		// 参照が別型に化けている(Inspectorで Type mismatch 表示)場合に例外で落ちないための防御。
		// その場合は Inspector で FBX本体/クリップをドラッグして刺し直せば直る
		GameObject actorPf = cs.actorPrefab as GameObject;
		AnimationClip actorCl = cs.actorClip as AnimationClip;
		GameObject cameraPf = cs.cameraPrefab as GameObject;
		AnimationClip cameraCl = cs.cameraClip as AnimationClip;
		if (actorPf == null)
		{
			Debug.LogError($"[CutsceneTest] {cs.name}: Actor Prefab が型不一致か未解決。InspectorでFBX本体を刺し直してください");
			return;
		}

		StopCutscene();

		// 再生位置: spawnPoint > プレイヤー > 原点
		Vector3 pos = Vector3.zero;
		Quaternion rot = Quaternion.identity;
		if (spawnPoint != null)
		{
			pos = spawnPoint.position;
			rot = spawnPoint.rotation;
		}
		else
		{
			GameObject player = GameObject.FindGameObjectWithTag("Player");
			if (player != null)
			{
				pos = player.transform.position;
				rot = player.transform.rotation;

				// プレイヤーのピボットは体の中心なので、そのまま生成すると浮く。
				// 足元の接地点まで下ろす(自分のコライダーは無視)
				float bestDist = float.MaxValue;
				foreach (RaycastHit hit in Physics.RaycastAll(
					pos + Vector3.up * 0.5f, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore))
				{
					if (hit.collider.transform.IsChildOf(player.transform)) continue;
					if (hit.distance < bestDist)
					{
						bestDist = hit.distance;
						pos = hit.point;
					}
				}
			}
		}

		float length = 0f;

		// キャラ側
		actorInstance = Instantiate(actorPf, pos, rot);
		if (actorCl != null)
		{
			// アバターを外してGenericのパス一致でボーンを直接動かす
			// (Humanoid経由だと深い座りポーズが浮くため)
			PlayOn(actorInstance, actorCl, stripAvatar: true);
			length = Mathf.Max(length, actorCl.length);
		}
		else
		{
			Debug.LogWarning($"[CutsceneTest] {cs.name}: Actor Clip が未設定/型不一致のためキャラは棒立ちです");
		}

		// カメラ側
		if (cameraPf != null)
		{
			cameraInstance = Instantiate(cameraPf, pos, rot);
			if (cameraCl != null)
			{
				PlayOn(cameraInstance, cameraCl, stripAvatar: false);
				length = Mathf.Max(length, cameraCl.length);
			}
			cameraNode = FindCameraNode(cameraInstance);
			rigCamera = cameraNode != null ? cameraNode.GetComponent<Camera>() : null;
			Debug.Log($"[CutsceneTest] カメラ追従ノード: {(cameraNode != null ? GetPath(cameraNode) : "なし")} FOVコピー: {(rigCamera != null ? "あり" : "なし")}");
		}
		else
		{
			Debug.LogWarning($"[CutsceneTest] {cs.name}: Camera Prefab が未設定/型不一致のためカメラは動きません");
		}

		// メインカメラを乗っ取る
		mainCam = Camera.main;
		if (mainCam != null)
		{
			brain = mainCam.GetComponent<CinemachineBrain>();
			if (brain != null) brain.enabled = false;
		}

		startTime = Time.time;
		clipLength = length > 0f ? length : 5f;
		playing = true;
		Debug.Log($"[CutsceneTest] 再生開始: {cs.name} ({clipLength:F1}秒) "
			+ $"actorClip={(actorCl != null ? $"{actorCl.name}({actorCl.length:F2}s)" : "なし")} "
			+ $"cameraClip={(cameraCl != null ? $"{cameraCl.name}({cameraCl.length:F2}s)" : "なし")}");

		// 1秒後にカメラノードが実際に動いたか自動判定(アニメ評価の生存確認)
		StartCoroutine(VerifyMotion());
	}

	private IEnumerator VerifyMotion()
	{
		if (cameraNode == null) yield break;
		Vector3 startPos = cameraNode.position;
		yield return new WaitForSeconds(1f);
		if (!playing || cameraNode == null) yield break;

		float moved = Vector3.Distance(cameraNode.position, startPos);
		Debug.Log(moved > 0.01f
			? $"[CutsceneTest] ✔ カメラアニメ評価OK(1秒で{moved:F2}m移動)"
			: "[CutsceneTest] ✘ カメラアニメが評価されていない(1秒間動きなし)");
	}

	public void StopCutscene()
	{
		if (actorInstance != null) Destroy(actorInstance);
		if (cameraInstance != null) Destroy(cameraInstance);
		cameraNode = null;
		rigCamera = null;

		if (brain != null) brain.enabled = true; // カメラをCinemachineへ返す
		brain = null;
		playing = false;
	}

	// 通常のAnimator再生でクリップを流す。
	// テンプレートコントローラの中身をAnimatorOverrideControllerで差し替える方式(最も標準的な経路)
	// stripAvatar=trueでアバターを外し、Genericのパス一致でボーンを直接動かす
	private void PlayOn(GameObject go, AnimationClip clip, bool stripAvatar)
	{
		if (playbackTemplate == null)
		{
			Debug.LogError("[CutsceneTest] Playback Template が未設定です。InspectorにCutscenePlayback.controllerを刺してください");
			return;
		}

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
			overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, clip);
		aoc.ApplyOverrides(overrides);

		animator.runtimeAnimatorController = aoc;
		animator.Rebind();
		animator.Update(0f);
	}

	// デバッグ表示用: ノードの階層パス
	private static string GetPath(Transform t)
	{
		string path = t.name;
		while (t.parent != null)
		{
			t = t.parent;
			path = t.name + "/" + path;
		}
		return path;
	}

	void OnDestroy() => StopCutscene();

	// 一時デバッグ: リグの階層構造と、クリップ適用で実際に動くノードを出力する
	private static void DumpDebug(GameObject rig, AnimationClip clip)
	{
		var sb = new System.Text.StringBuilder("[CutsceneTest] リグ階層:\n");
		foreach (Transform t in rig.GetComponentsInChildren<Transform>(true))
		{
			string comps = string.Join(",", System.Array.ConvertAll(
				t.GetComponents<Component>(), c => c.GetType().Name));
			sb.AppendLine($"  {GetPath(t)}  [{comps}]");
		}
		Debug.Log(sb.ToString());

		if (clip == null) return;

		// t=0 と t=1秒 をサンプリングして、位置/回転が変わるノードを検出
		Transform[] nodes = rig.GetComponentsInChildren<Transform>(true);
		Vector3[] p0 = new Vector3[nodes.Length];
		Quaternion[] r0 = new Quaternion[nodes.Length];
		clip.SampleAnimation(rig, 0f);
		for (int i = 0; i < nodes.Length; i++)
		{
			p0[i] = nodes[i].position;
			r0[i] = nodes[i].rotation;
		}
		clip.SampleAnimation(rig, Mathf.Min(1f, clip.length));

		var moved = new System.Text.StringBuilder("[CutsceneTest] クリップで動いたノード: ");
		bool any = false;
		for (int i = 0; i < nodes.Length; i++)
		{
			bool posChanged = (nodes[i].position - p0[i]).sqrMagnitude > 0.0001f;
			bool rotChanged = Quaternion.Angle(nodes[i].rotation, r0[i]) > 0.1f;
			if (posChanged || rotChanged)
			{
				moved.Append(GetPath(nodes[i]))
					.Append(posChanged && rotChanged ? "(移動+回転)" : posChanged ? "(移動)" : "(回転)")
					.Append("  ");
				any = true;
			}
		}
		if (!any) moved.Append("なし(このクリップは階層を動かしていない=FBX側にアニメデータが無い可能性)");
		Debug.Log(moved.ToString());
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
