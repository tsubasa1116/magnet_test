using System.Collections.Generic;
using UnityEngine;

// 引き寄せ中の磁石エフェクト（ブレワイのマグネキャッチ風）。
// ・対象の周りをオレンジの脈動するリング(磁力場)が回る
// ・両腕から対象へ、ゆらめく磁力ビームが伸びる
// ・対象がオレンジに発光(任意のLight)
// LineRenderer をコードで生成して描画する。
public class MagnetZapEffect : MonoBehaviour
{
	[Header("参照")]
	[SerializeField] private MagnetPull magnetPull;     // 未指定なら同オブジェクトから取得
	[Tooltip("ビームの起点(両腕の手/腕先のTransform)")]
	[SerializeField] private Transform[] armPoints;

	[Header("見た目")]
	[SerializeField] private Material boltMaterial;     // 未指定なら Sprites/Default で生成
	[Tooltip("マグネキャッチのオレンジ")]
	[SerializeField] private Color color = new Color(1f, 0.5f, 0.08f, 1f);
	[SerializeField] private float pulseSpeed = 5f;     // 脈動の速さ
	[SerializeField, Range(0f, 0.6f)] private float pulseAmount = 0.18f;

	[Header("磁力場リング")]
	[SerializeField] private int ringCount = 3;
	[SerializeField] private float ringRadius = 0.6f;
	[SerializeField] private float ringWidth = 0.04f;
	[SerializeField] private int ringSegments = 40;
	[SerializeField] private float ringRotateSpeed = 90f; // 度/秒

	[Header("腕からのビーム")]
	[SerializeField] private float beamWidth = 0.05f;
	[SerializeField] private int beamSegments = 18;
	[SerializeField] private float beamWave = 0.12f;      // ゆらめき幅
	[SerializeField] private float beamWaveSpeed = 8f;

	[Header("発光")]
	[SerializeField] private bool useGlowLight = true;
	[SerializeField] private float lightIntensity = 4f;
	[SerializeField] private float lightRange = 4f;

	private readonly List<LineRenderer> beams = new List<LineRenderer>();
	private readonly List<LineRenderer> rings = new List<LineRenderer>();
	private Light glow;

	void Start()
	{
		if (magnetPull == null) magnetPull = GetComponent<MagnetPull>();
		if (boltMaterial == null) boltMaterial = new Material(Shader.Find("Sprites/Default"));

		if (armPoints != null)
			foreach (var _ in armPoints) beams.Add(CreateLine(beamWidth));
		for (int i = 0; i < ringCount; i++) rings.Add(CreateLine(ringWidth));

		if (useGlowLight)
		{
			var go = new GameObject("MagnetGlow");
			go.transform.SetParent(transform, false);
			glow = go.AddComponent<Light>();
			glow.type = LightType.Point;
			glow.color = color;
			glow.range = lightRange;
			glow.intensity = 0f;
			glow.enabled = false;
		}
	}

	private LineRenderer CreateLine(float w)
	{
		var go = new GameObject("MagnetBolt");
		go.transform.SetParent(transform, false);
		var lr = go.AddComponent<LineRenderer>();
		lr.material = boltMaterial;
		lr.useWorldSpace = true;
		lr.numCapVertices = 4;
		lr.loop = false;
		lr.textureMode = LineTextureMode.Stretch;
		lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		lr.receiveShadows = false;
		lr.startWidth = lr.endWidth = w;
		lr.enabled = false;
		return lr;
	}

	void LateUpdate()
	{
		Transform target = magnetPull != null ? magnetPull.HeldObject : null;
		bool active = target != null;

		// 全体の脈動(0.x〜1.x)
		float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
		Color c = color;
		c.a *= Mathf.Lerp(0.7f, 1f, (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f);

		// --- 腕 → 対象 のゆらめくビーム ---
		for (int i = 0; i < beams.Count; i++)
		{
			var lr = beams[i];
			if (!active || armPoints == null || i >= armPoints.Length || armPoints[i] == null)
			{
				lr.enabled = false;
				continue;
			}
			lr.enabled = true;
			lr.startColor = lr.endColor = c;
			lr.startWidth = lr.endWidth = beamWidth * pulse;
			DrawBeam(lr, armPoints[i].position, target.position);
		}

		// --- 対象を包む磁力場リング ---
		for (int i = 0; i < rings.Count; i++)
		{
			var lr = rings[i];
			if (!active) { lr.enabled = false; continue; }
			lr.enabled = true;
			lr.startColor = lr.endColor = c;
			lr.startWidth = lr.endWidth = ringWidth * pulse;

			// リングごとに異なる向きで回転させる
			float spin = Time.time * ringRotateSpeed;
			Quaternion rot = Quaternion.Euler(
				spin * (i + 1) * 0.6f,
				spin * (i + 1),
				i * (180f / Mathf.Max(1, ringCount)));
			DrawRing(lr, target.position, ringRadius * pulse, rot);
		}

		// --- 発光 ---
		if (glow != null)
		{
			glow.enabled = active;
			if (active)
			{
				glow.transform.position = target.position;
				glow.color = color;
				glow.intensity = lightIntensity * pulse;
				glow.range = lightRange;
			}
		}
	}

	// 始点→終点を、サイン波で横に揺らした「流れる磁力ビーム」で描く（端は固定）
	private void DrawBeam(LineRenderer lr, Vector3 start, Vector3 end)
	{
		int count = Mathf.Max(2, beamSegments + 1);
		lr.positionCount = count;

		Vector3 dir = end - start;
		Vector3 axis = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
		Vector3 perp = Vector3.Cross(axis, Vector3.up);
		if (perp.sqrMagnitude < 0.001f) perp = Vector3.right;
		perp.Normalize();
		Vector3 perp2 = Vector3.Cross(axis, perp);

		for (int i = 0; i < count; i++)
		{
			float t = i / (float)(count - 1);
			Vector3 p = Vector3.Lerp(start, end, t);
			float env = Mathf.Sin(t * Mathf.PI); // 両端は0で固定
			float o1 = Mathf.Sin(t * Mathf.PI * 4f + Time.time * beamWaveSpeed) * beamWave * env;
			float o2 = Mathf.Cos(t * Mathf.PI * 6f + Time.time * beamWaveSpeed * 1.3f) * beamWave * 0.6f * env;
			p += perp * o1 + perp2 * o2;
			lr.SetPosition(i, p);
		}
	}

	// 中心の周りに円(リング)を描く
	private void DrawRing(LineRenderer lr, Vector3 center, float radius, Quaternion rot)
	{
		int count = Mathf.Max(4, ringSegments + 1);
		lr.positionCount = count;
		for (int i = 0; i < count; i++)
		{
			float a = (i / (float)ringSegments) * Mathf.PI * 2f;
			Vector3 local = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
			lr.SetPosition(i, center + rot * local);
		}
	}
}
