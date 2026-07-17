using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
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

	private GameObject actorInstance;
	private GameObject cameraInstance;
	private Transform cameraNode; // カメラアニメ内の追従対象ノード
	private PlayableGraph actorGraph;
	private PlayableGraph cameraGraph;
	private float endTime;
	private bool playing;

	private Camera mainCam;
	private Behaviour brain; // CinemachineBrain(再生中は無効化してカメラを乗っ取る)

	void Update()
	{
		if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) Play(0);
		if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) Play(1);
		if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) Play(2);
		if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0)) StopCutscene();

		if (playing && Time.time >= endTime) StopCutscene();
	}

	void LateUpdate()
	{
		// カメラアニメのノードにメインカメラを重ねる(Cinemachineは停止中)
		if (playing && mainCam != null && cameraNode != null)
			mainCam.transform.SetPositionAndRotation(cameraNode.position, cameraNode.rotation);
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
			}
		}

		float length = 0f;

		// キャラ側
		actorInstance = Instantiate(cs.actorPrefab, pos, rot);
		if (cs.actorClip != null)
		{
			PlayClip(actorInstance, cs.actorClip, out actorGraph);
			length = Mathf.Max(length, cs.actorClip.length);
		}

		// カメラ側
		if (cs.cameraPrefab != null)
		{
			cameraInstance = Instantiate(cs.cameraPrefab, pos, rot);
			if (cs.cameraClip != null)
			{
				PlayClip(cameraInstance, cs.cameraClip, out cameraGraph);
				length = Mathf.Max(length, cs.cameraClip.length);
			}
			cameraNode = FindCameraNode(cameraInstance);
		}

		// メインカメラを乗っ取る
		mainCam = Camera.main;
		if (mainCam != null)
		{
			brain = mainCam.GetComponent<CinemachineBrain>();
			if (brain != null) brain.enabled = false;
		}

		endTime = Time.time + (length > 0f ? length : 5f);
		playing = true;
		Debug.Log($"[CutsceneTest] 再生開始: {cs.name} ({length:F1}秒)");
	}

	public void StopCutscene()
	{
		if (actorGraph.IsValid()) actorGraph.Destroy();
		if (cameraGraph.IsValid()) cameraGraph.Destroy();
		if (actorInstance != null) Destroy(actorInstance);
		if (cameraInstance != null) Destroy(cameraInstance);
		cameraNode = null;

		if (brain != null) brain.enabled = true; // カメラをCinemachineへ返す
		brain = null;
		playing = false;
	}

	void OnDestroy() => StopCutscene();

	// コントローラ無しでAnimatorに直接クリップを流す
	private static void PlayClip(GameObject go, AnimationClip clip, out PlayableGraph graph)
	{
		Animator animator = go.GetComponent<Animator>();
		if (animator == null) animator = go.AddComponent<Animator>();
		AnimationPlayableUtilities.PlayClip(animator, clip, out graph);
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
