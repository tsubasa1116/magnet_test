using System.Collections.Generic;
using UnityEngine;

// タグではなく「部位」単位でロックオン(注目)させたい物に付ける。
// ボスのバリア・左右の手のように、1体の中に注目点が複数ある場合に使う。
// PlayerAim は lockOnTags のタグ検索に加えて、有効なこのコンポーネントも候補に入れる。
public class LockOnPart : MonoBehaviour
{
	public enum Kind
	{
		Enemy, // 敵色のマーカー
		NPole, // N極色のマーカー
		SPole  // S極色のマーカー
	}

	// 有効(OnEnable〜OnDisable)な注目部位の一覧
	public static readonly List<LockOnPart> Active = new List<LockOnPart>();

	[SerializeField] private Kind kind = Kind.Enemy;
	[Tooltip("遮蔽判定で無視する本体(ボスのルートなど)。部位が本体のコライダーに埋まっていても注目できるようにする。未指定なら自分")]
	[SerializeField] private Transform ownerRoot;
	[Tooltip("コライダーが無い時のマーカーの大きさ")]
	[SerializeField] private float defaultSize = 1.5f;

	// 注目できる条件(分離して消えた手は注目させない等)。未設定なら常に注目可
	public System.Func<bool> Condition;

	// この部位に注目中に注目できなくなった時、代わりに注目を引き継ぐ部位
	// (ボスのバリアが割れたら中のコアへ、など)。未設定なら注目を外す
	public LockOnPart Successor;

	private Collider[] cols;

	public Kind TargetKind => kind;
	public Transform OwnerRoot => ownerRoot != null ? ownerRoot : transform;
	public bool IsAvailable => isActiveAndEnabled && (Condition == null || Condition());

	// 照準点: 配下のコライダーを合わせた中心(アニメで動く手にも追従する)
	public Vector3 Point => TryGetBounds(out Bounds b) ? b.center : transform.position;

	// マーカーの大きさ(配下のコライダーを合わせた大きさ)
	public float Size
	{
		get
		{
			if (!TryGetBounds(out Bounds b)) return defaultSize;
			Vector3 e = b.extents;
			return Mathf.Max(e.x, e.y, e.z) * 2f;
		}
	}

	// 実行時に部位へ付けて設定する
	public static LockOnPart Attach(GameObject go, Kind kind, Transform ownerRoot, System.Func<bool> condition = null)
	{
		LockOnPart t = go.GetComponent<LockOnPart>();
		if (t == null) t = go.AddComponent<LockOnPart>();
		t.kind = kind;
		t.ownerRoot = ownerRoot;
		t.Condition = condition;
		return t;
	}

	void Awake()
	{
		cols = GetComponentsInChildren<Collider>(true);
	}

	void OnEnable()
	{
		if (!Active.Contains(this)) Active.Add(this);
	}

	void OnDisable()
	{
		Active.Remove(this);
	}

	private bool TryGetBounds(out Bounds bounds)
	{
		bounds = default;
		bool found = false;
		if (cols == null) return false;
		foreach (Collider c in cols)
		{
			// 無効化中の判定用コライダーやトリガーは大きさに含めない
			if (c == null || !c.enabled || c.isTrigger || !c.gameObject.activeInHierarchy) continue;
			if (!found) { bounds = c.bounds; found = true; }
			else bounds.Encapsulate(c.bounds);
		}
		return found;
	}
}
