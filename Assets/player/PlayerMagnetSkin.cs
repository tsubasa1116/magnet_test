using UnityEngine;

// プレイヤーの極(N/S)に合わせて、ボディ/顔のテクスチャを 赤(N) / 青(S) に切り替える。
// PlayerStateMachine.OnStateChanged を購読して、SkinnedMeshRenderer のマテリアルの
// アルベド(_MainTex)と顔のエミッシブ(_EmissionMap)を差し替える。
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

	private PlayerStateMachine stateMachine;
	private Material[] mats;

	private static readonly int MainTexID = Shader.PropertyToID("_MainTex");
	private static readonly int EmissionMapID = Shader.PropertyToID("_EmissionMap");

	void Awake()
	{
		stateMachine = GetComponent<PlayerStateMachine>();
		if (targetRenderer == null) targetRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
		if (targetRenderer != null) mats = targetRenderer.materials; // インスタンス化(共有アセットを汚さない)
		stateMachine.OnStateChanged += Apply;
	}

	void Start()
	{
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
			SetTex(mats[bodyIndex], MainTexID, n ? bodyN : bodyS);

		if (IsValid(faceIndex))
		{
			Material fm = mats[faceIndex];
			SetTex(fm, MainTexID, n ? faceN : faceS);
			SetTex(fm, EmissionMapID, n ? faceEmissionN : faceEmissionS);
			fm.EnableKeyword("_EMISSION");
		}
	}

	// 未割当(null)のときは既存テクスチャを残す(うっかり真っ白を防ぐ)
	private static void SetTex(Material m, int id, Texture t)
	{
		if (t != null) m.SetTexture(id, t);
	}

	private bool IsValid(int i) => mats != null && i >= 0 && i < mats.Length;
}
