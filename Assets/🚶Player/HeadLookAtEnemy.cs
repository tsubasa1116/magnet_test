using UnityEngine;

// プレイヤーの顔(頭)を最寄りの敵(Tag: Enemy)に向ける。
// 条件:
//   ・一定範囲(range)内に敵がいる時だけ向ける
//   ・敵がプレイヤーの体の正面から maxAngle 以内にいる時だけ向ける
//     (体が敵と反対側を向いている＝背後 なら向けない)
//
// Humanoid の LookAt IK を使うため、Animator の対象レイヤーで
// 「IK Pass」を有効にする必要がある。
[RequireComponent(typeof(Animator))]
public class HeadLookAtEnemy : MonoBehaviour
{
	[Header("探索")]
	[SerializeField] private float range = 10f;                 // この範囲内の敵のみ対象
	[SerializeField] private string enemyTag = "Enemy";

	[Tooltip("体の正面からこの角度以内にいる敵だけ見る。これより外(背後など)は見ない")]
	[SerializeField, Range(0f, 180f)] private float maxAngle = 130f;

	[Header("IK ウェイト")]
	[SerializeField, Range(0f, 1f)] private float lookWeight = 1f;   // 全体の効き
	[SerializeField, Range(0f, 1f)] private float bodyWeight = 0.3f; // 体の追従(小さめ)
	[SerializeField, Range(0f, 1f)] private float headWeight = 0.8f; // 頭
	[SerializeField, Range(0f, 1f)] private float eyesWeight = 0f;   // 目(Eyeボーンがあれば)
	[SerializeField, Range(0f, 1f)] private float clampWeight = 0.5f; // 可動域制限(大きいほど無理に向かない)
	[Tooltip("向き先が変わった時に振り向き切るまでの時間(秒)。最初ゆっくり→速く→最後ゆっくり(イーズイン・アウト)")]
	[SerializeField] private float transitionDuration = 0.4f;

	[Tooltip("敵のどこを見るか(足元からのオフセット。頭の高さあたり)")]
	[SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);

	private Animator animator;
	private Transform target;       // 現在見ている敵
	private Transform prevTarget;   // 直前の対象(切替検出用)
	private float currentWeight;    // 実効ウェイト
	private Vector3 currentLookPos; // 実効の注視点(ワールド座標)
	private Vector3 startLookPos;   // 今回の振り向き開始時の注視点
	private float startWeight;      // 今回の振り向き開始時のウェイト
	private float transT;           // 振り向きの進行度 0→1

	void Awake()
	{
		animator = GetComponent<Animator>();
		currentLookPos = startLookPos = ForwardLookPoint();
		transT = 1f;
	}

	void Update()
	{
		target = FindLookTarget();

		// 向き先(対象)が切り替わったら、新しい振り向きを「今の状態」を起点に開始
		if (target != prevTarget)
		{
			startLookPos = currentLookPos;
			startWeight = currentWeight;
			transT = 0f;
			prevTarget = target;
		}

		// 進行度を 0→1 に進め、SmoothStep でイーズイン・アウト(最初ゆっくり→速く→最後ゆっくり)
		transT = Mathf.MoveTowards(transT, 1f, Time.deltaTime / Mathf.Max(transitionDuration, 0.0001f));
		float e = Mathf.SmoothStep(0f, 1f, transT);

		// 目標(敵が動けば追従して更新される / 敵なしは正面)
		Vector3 endPos = target != null ? target.position + targetOffset : ForwardLookPoint();
		float endWeight = target != null ? lookWeight : 0f;

		// 開始値 → 目標値 をイーズイン・アウトで補間。
		// 振り向き完了後(transT=1)は端値そのものになり、動く敵にも遅れず追従する。
		currentLookPos = Vector3.Lerp(startLookPos, endPos, e);
		currentWeight = Mathf.Lerp(startWeight, endWeight, e);
	}

	// 敵がいない時に見る「正面の点」(頭の高さ・少し前方)
	private Vector3 ForwardLookPoint()
	{
		return transform.position + transform.forward * 5f + Vector3.up * targetOffset.y;
	}

	// 範囲内かつ正面側にいる、最も近い敵を返す(いなければ null)
	private Transform FindLookTarget()
	{
		GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);

		Transform best = null;
		float bestSqr = range * range;

		Vector3 forward = transform.forward;
		forward.y = 0f;

		foreach (var enemy in enemies)
		{
			if (enemy == null) continue;

			Vector3 to = enemy.transform.position - transform.position;
			if (to.sqrMagnitude > bestSqr) continue; // 範囲外、または既知の最寄りより遠い

			Vector3 flat = to;
			flat.y = 0f;
			if (flat.sqrMagnitude < 0.0001f) continue;

			// 体の正面からの角度で前後を判定
			if (Vector3.Angle(forward, flat) > maxAngle) continue; // 背後/横すぎは見ない

			bestSqr = to.sqrMagnitude;
			best = enemy.transform;
		}

		return best;
	}

	// IK Pass が有効なレイヤーごとに呼ばれる
	void OnAnimatorIK(int layerIndex)
	{
		animator.SetLookAtWeight(currentWeight, bodyWeight, headWeight, eyesWeight, clampWeight);
		// 注視点は常に補間後の位置を使う（ウェイトでフェードする）
		animator.SetLookAtPosition(currentLookPos);
	}

	// 範囲を可視化(選択時)
	void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.yellow;
		Gizmos.DrawWireSphere(transform.position, range);
	}
}
