using UnityEngine;

// ヒットストップ演出。どこからでも HitStop.Play(秒数) で呼べる。
// 完全停止ではなく「ほぼ停止のスロー(5%) → なめらかに通常速度へ復帰」で、
// カメラ(Cinemachine)もゲーム内時間で動いているため一緒にスローになる。
// 同時に画面シェイクも発生させる。
// ・重複して呼ばれた場合は長い方を優先
// ・ポーズ中(timeScale=0)は何もしない / 演出中に外部がtimeScaleを触ったら手を引く
public class HitStop : MonoBehaviour
{
	private const float SlowScale = 0.05f;    // 停止中のスロー倍率(0=完全停止、上げるほどスローモーション寄り)
	private const float RecoverTime = 0.15f;  // スローから通常速度へ戻すランプ時間(実時間秒)
	private const float ShakeAmplitude = 0.12f; // ヒットストップに付随する画面シェイクの強さ(m)

	private static HitStop instance;

	private bool active;
	private float holdUntil = -1f;     // 実時間でいつまでスローを維持するか
	private float prevTimeScale = 1f;  // 復帰時に戻す値
	private float lastApplied = -1f;   // 自分が最後に設定したtimeScale(外部変更の検知用)

	public static void Play(float duration)
	{
		if (duration <= 0f) return;

		if (instance == null)
		{
			var go = new GameObject("HitStop");
			DontDestroyOnLoad(go);
			instance = go.AddComponent<HitStop>();
		}
		instance.Begin(duration);
	}

	private void Begin(float duration)
	{
		// 自分以外の理由で時間が止まっている(ポーズ等)なら手を出さない
		if (Time.timeScale <= 0f && !active) return;

		if (!active) prevTimeScale = Time.timeScale;
		active = true;
		holdUntil = Mathf.Max(holdUntil, Time.realtimeSinceStartup + duration);
		Apply(SlowScale);

		// 衝撃の画面シェイク(スロー復帰までかけて減衰)
		ScreenShake.Play(ShakeAmplitude, duration + RecoverTime);
	}

	private void Apply(float scale)
	{
		Time.timeScale = scale;
		lastApplied = scale;
	}

	void Update()
	{
		if (!active) return;

		// 演出中に外部(ポーズ等)がtimeScaleを変更したら、上書きせず手を引く
		if (!Mathf.Approximately(Time.timeScale, lastApplied))
		{
			active = false;
			holdUntil = -1f;
			return;
		}

		float now = Time.realtimeSinceStartup;
		if (now < holdUntil) return; // スロー維持中

		// スロー → 通常速度へなめらかに復帰
		float t = (now - holdUntil) / RecoverTime;
		if (t >= 1f)
		{
			Apply(prevTimeScale);
			active = false;
			holdUntil = -1f;
		}
		else
		{
			Apply(Mathf.Lerp(SlowScale, prevTimeScale, t));
		}
	}
}
