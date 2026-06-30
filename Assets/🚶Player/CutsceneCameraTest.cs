using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// テスト用: 指定キー(既定 C)で演出カメラ(cameraTest_v1 等)に切り替え、
// そのカメラワークのアニメを頭から再生する。もう一度押すとゲームカメラに戻る。
// AnimatorController を作らなくても、クリップを直接再生する。
public class CutsceneCameraTest : MonoBehaviour
{
	[SerializeField] private KeyCode key = KeyCode.C;

	[Tooltip("演出カメラ(FBXのCamera)")]
	[SerializeField] private Camera cutsceneCamera;
	[Tooltip("カメラワークを持つAnimator(通常はFBXのルート)。未指定ならcutsceneCameraから取得")]
	[SerializeField] private Animator cutsceneAnimator;
	[Tooltip("再生するカメラワークのクリップ(FBX内のAnimationClip)")]
	[SerializeField] private AnimationClip cutsceneClip;

	private Camera gameplayCamera;
	private PlayableGraph graph;
	private bool active;

	void Start()
	{
		gameplayCamera = Camera.main;
		if (cutsceneAnimator == null && cutsceneCamera != null)
			cutsceneAnimator = cutsceneCamera.GetComponentInParent<Animator>();
		if (cutsceneCamera != null) cutsceneCamera.gameObject.SetActive(false);
	}

	void Update()
	{
		if (Input.GetKeyDown(key))
		{
			if (active) Exit();
			else Enter();
		}
	}

	private void Enter()
	{
		if (cutsceneCamera == null) return;
		active = true;

		// ゲームカメラの描画を止めて、演出カメラに切替
		if (gameplayCamera != null) gameplayCamera.enabled = false;
		cutsceneCamera.gameObject.SetActive(true);

		// AnimatorControllerが無くてもクリップを頭から直接再生
		if (cutsceneAnimator != null && cutsceneClip != null)
		{
			if (graph.IsValid()) graph.Destroy();
			AnimationPlayableUtilities.PlayClip(cutsceneAnimator, cutsceneClip, out graph);
		}
	}

	private void Exit()
	{
		active = false;
		if (graph.IsValid()) graph.Destroy();
		if (cutsceneCamera != null) cutsceneCamera.gameObject.SetActive(false);
		if (gameplayCamera != null) gameplayCamera.enabled = true;
	}

	void OnDestroy()
	{
		if (graph.IsValid()) graph.Destroy();
	}
}
