using UnityEngine;

// プレイヤーの表情の種類。状態に応じて顔テクスチャを切り替える
public enum FaceExpression
{
	Normal,  // 通常
	Catch,   // キャッチ/引き寄せ/保持中
	Damage,  // 被弾(無敵時間中)
	Down,    // 死亡
}

// プレイヤーの極(N/S)に合わせて、ボディ/顔のテクスチャを 赤(N) / 青(S) に切り替える。
// PlayerStateMachine.OnStateChanged を購読して、SkinnedMeshRenderer のマテリアルの
// アルベド(_MainTex)と顔のエミッシブ(_EmissionMap)を差し替える。
// さらに、キャラの状態(キャッチ/被弾/死亡)に応じて顔の表情テクスチャも切り替える。
[RequireComponent(typeof(PlayerStateMachine))]
public class PlayerMagnetSkin : MonoBehaviour
{
	[Header("対象レンダラー")]
	[SerializeField] private SkinnedMeshRenderer targetRenderer; // 未指定なら子から取得
	[Tooltip("ボディのマテリアル枠 / 顔のマテリアル枠(逆なら入れ替える)")]
	[SerializeField] private int bodyIndex = 0;
	[SerializeField] private int faceIndex = 1;

	[Header("ボディ アルベド")]
	[SerializeField] private Texture bodyN; // N=赤
	[SerializeField] private Texture bodyS; // S=青

	[Header("顔 アルベド")]
	[SerializeField] private Texture faceN;
	[SerializeField] private Texture faceS;

	[Header("顔 エミッシブ")]
	[SerializeField] private Texture faceEmissionN;
	[SerializeField] private Texture faceEmissionS;

	[Header("表情差分(未割当の表情は通常の顔のまま)")]
	[Tooltip("状態(キャッチ/被弾/死亡)から表情を自動で切り替える")]
	[SerializeField] private bool autoExpression = true;
	[SerializeField] private Texture faceCatchN;
	[SerializeField] private Texture faceCatchS;
	[SerializeField] private Texture faceDamageN;
	[SerializeField] private Texture faceDamageS;
	[SerializeField] private Texture faceDownN;
	[SerializeField] private Texture faceDownS;

	private PlayerStateMachine stateMachine;
	private PlayerHealth health;
	private PlayerCatch catchState;
	private Material[] mats;
	private FaceExpression expression = FaceExpression.Normal;

	// 演出などから手動で表情を変えたい時用(autoExpressionをOFFにして使う)
	public FaceExpression Expression => expression;

	// アルベドはビルトイン(_MainTex)とURP/Lit(_BaseMap)で名前が違うので両方に設定する
	private static readonly int MainTexID = Shader.PropertyToID("_MainTex");
	private static readonly int BaseMapID = Shader.PropertyToID("_BaseMap");
	private static readonly int EmissionMapID = Shader.PropertyToID("_EmissionMap");

	void Awake()
	{
		stateMachine = GetComponent<PlayerStateMachine>();
		health = GetComponent<PlayerHealth>();
		catchState = GetComponent<PlayerCatch>();
		if (targetRenderer == null) targetRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
		if (targetRenderer != null) mats = targetRenderer.materials; // インスタンス化(共有アセットを汚さない)
		stateMachine.OnStateChanged += Apply;
	}

	void Start()
	{
		Apply(stateMachine.CurrentState);
	}

	void Update()
	{
		if (!autoExpression) return;

		// 優先度: 死亡 > 被弾(無敵中) > キャッチ > 通常
		FaceExpression next =
			health != null && health.IsDead ? FaceExpression.Down :
			health != null && health.IsInvincible ? FaceExpression.Damage :
			catchState != null && catchState.IsCatching ? FaceExpression.Catch :
			FaceExpression.Normal;

		SetExpression(next);
	}

	// 表情を切り替える(同じ表情なら何もしない)。カットシーン等から手動で呼んでもOK
	public void SetExpression(FaceExpression e)
	{
		if (expression == e) return;
		expression = e;
		Apply(stateMachine.CurrentState);
	}

	void OnDestroy()
	{
		if (stateMachine != null) stateMachine.OnStateChanged -= Apply;
	}

	private void Apply(MagnetState state)
	{
		bool n = state == MagnetState.N;

		if (IsValid(bodyIndex))
			SetAlbedo(mats[bodyIndex], n ? bodyN : bodyS);

		if (IsValid(faceIndex))
		{
			Material fm = mats[faceIndex];
			SetAlbedo(fm, SelectFaceTexture(n));
			SetTex(fm, EmissionMapID, n ? faceEmissionN : faceEmissionS);
			fm.EnableKeyword("_EMISSION");
		}
	}

	// 現在の表情に対応する顔テクスチャを返す。差分が未割当なら通常の顔にフォールバック
	private Texture SelectFaceTexture(bool n)
	{
		Texture t = null;
		switch (expression)
		{
			case FaceExpression.Catch: t = n ? faceCatchN : faceCatchS; break;
			case FaceExpression.Damage: t = n ? faceDamageN : faceDamageS; break;
			case FaceExpression.Down: t = n ? faceDownN : faceDownS; break;
		}
		return t != null ? t : (n ? faceN : faceS);
	}

	// アルベドをビルトイン/URP両対応で設定
	private static void SetAlbedo(Material m, Texture t)
	{
		SetTex(m, MainTexID, t);
		SetTex(m, BaseMapID, t);
	}

	// 未割当(null)のときは既存テクスチャを残す(うっかり真っ白を防ぐ)
	private static void SetTex(Material m, int id, Texture t)
	{
		if (t != null) m.SetTexture(id, t);
	}

	private bool IsValid(int i) => mats != null && i >= 0 && i < mats.Length;
}
