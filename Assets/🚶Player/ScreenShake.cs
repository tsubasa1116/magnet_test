using UnityEngine;

// 画面シェイク。どこからでも ScreenShake.Play(強さ, 時間) で呼べる。
// Cinemachineがカメラ位置を確定させた後(描画直前)にオフセットを乗せる方式なので、
// vcamやシーンの構成に手を入れずにどのシーンでも揺れる。
// timeScaleに依存しない実時間駆動(ヒットストップ中でも揺れる)。
public class ScreenShake : MonoBehaviour
{
	private static ScreenShake instance;

	private float endAt = -1f;   // 実時間での終了時刻
	private float duration;      // 減衰計算用
	private float amplitude;     // 揺れ幅(m)

	public static void Play(float amplitude, float duration)
	{
		if (amplitude <= 0f || duration <= 0f) return;

		if (instance == null)
		{
			var go = new GameObject("ScreenShake");
			DontDestroyOnLoad(go);
			instance = go.AddComponent<ScreenShake>();
		}
		instance.Begin(amplitude, duration);
	}

	private void Begin(float amp, float dur)
	{
		float end = Time.realtimeSinceStartup + dur;
		// 進行中の揺れより強い/長い方を採用
		amplitude = endAt > Time.realtimeSinceStartup ? Mathf.Max(amplitude, amp) : amp;
		if (end > endAt)
		{
			endAt = end;
			duration = dur;
		}
	}

	void OnEnable() => Application.onBeforeRender += ApplyShake;
	void OnDisable() => Application.onBeforeRender -= ApplyShake;

	// LateUpdateでCinemachineがカメラを動かした後、描画の直前に呼ばれる。
	// ここで足したオフセットは次フレームでCinemachineが上書きするので蓄積しない。
	private void ApplyShake()
	{
		if (endAt < 0f) return;

		float remain = endAt - Time.realtimeSinceStartup;
		if (remain <= 0f)
		{
			endAt = -1f;
			return;
		}

		Camera cam = Camera.main;
		if (cam == null) return;

		float decay = duration > 0f ? Mathf.Clamp01(remain / duration) : 0f; // 1→0で減衰
		float a = amplitude * decay;

		cam.transform.position += Random.insideUnitSphere * a;
		// わずかなロール回転を混ぜると衝撃感が増す
		cam.transform.rotation *= Quaternion.Euler(0f, 0f, Random.Range(-1f, 1f) * a * 10f);
	}
}
