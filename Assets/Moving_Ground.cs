using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingGround : MonoBehaviour
{
	[Header("設定")]
	public float moveSpeed = 30.0f;            // ベース速度（補助）
	[Tooltip("moveSpeed に掛ける倍率。Inspectorで微調整可")]
	public float speedMultiplier = 1.0f;
	[Tooltip("指定秒数でターゲットYに到達させる（>0で優先）。短くすると速く落ちる。")]
	public float dropDuration = 0.2f;
	[Tooltip("有効にすると瞬間でターゲットYへ移動してロックする")]
	public bool instantDrop = false;
	[Tooltip("最低落下速度（これより遅くならない）。単位: ユニット/秒")]
	public float minDropSpeed = 100f; // ここを大きくすると確実に速くなる

	public Transform targetPoint; // 使用しないが残しておく（互換性）

	[Header("当たり判定")]
	public float checkRadius = 1.5f; // 地面上のオブジェクト検出半径
	public float checkHeightOffset = 1.0f; // 検出中心の高さオフセット（地面の上方）

	[Header("敵処理")]
	[Tooltip("敵を即倒すときの検出半径")]
	public float enemyCheckRadius = 1.5f;
	[Tooltip("敵に与えるダメージ（大きければ即死）")]
	public float enemyDamage = 9999f;

	// 内部
	private bool isMoving = false;
	private bool hasLanded = false; // 一度着地したら二度と動かさないフラグ
	private magnet playerMagnet;
	private Transform playerTransform;
	private Rigidbody rb;

	// 前回の磁石モードを保持（ボタン押下の瞬間検出用）
	private int prevMagnetMode = 0;

	// 到着目標 Y を開始時に固定する（これが到達先）
	private float landingTargetY = 0f; // 常に 0 に固定
	private bool landingTargetYSet = false;

	// タグ定義
	private const string N_TAG = "N_Pole";
	private const string S_TAG = "S_Pole";

	void Start()
	{
		// Rigidbody キャッシュ
		rb = GetComponent<Rigidbody>();

		// プレイヤー取得（magnet 参照のみ）
		GameObject player = GameObject.FindGameObjectWithTag("Player");
		if (player != null)
		{
			playerMagnet = player.GetComponentInChildren<magnet>();
			playerTransform = player.transform;
		}

		if (playerMagnet != null)
		{
			prevMagnetMode = playerMagnet.magnetMode;
		}

		// 初期位置が既に Y=0 なら着地済みとする（かつ完全固定）
		if (Mathf.Approximately(transform.position.y, 0f))
		{
			LockInPlace();
		}
	}

	void Update()
	{
		// すでに固定済みなら以後一切動かさない（早期リターン）
		if (hasLanded) return;

		// 必要な参照が無ければ何もしない
		if (playerMagnet == null || Camera.main == null) return;

		// 現在の磁石モード
		int currentMode = playerMagnet.magnetMode;

		// レイでこの地面を見ているか（子コライダー対応）
		Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
		RaycastHit hit;
		bool isLooking = false;
		if (Physics.Raycast(ray, out hit, 100f))
		{
			MovingGround hitGround = hit.collider.GetComponentInParent<MovingGround>();
			if (hitGround == this) isLooking = true;
		}

		// この地面上に「磁石モードと反対のタグ」を持つオブジェクトがあるかチェック
		bool hasOppositePoleOnThisGround = CheckForOppositePole(currentMode);

		// 挙動: ボタン押下時に見ていた or 有効中に見ている、かつ反対極が地面上にあれば開始
		if (!isMoving)
		{
			if (prevMagnetMode == 0 && currentMode != 0 && isLooking && hasOppositePoleOnThisGround)
			{
				StartMovingAndLockTargetY();
			}
			else if (currentMode != 0 && isLooking && hasOppositePoleOnThisGround)
			{
				StartMovingAndLockTargetY();
			}
		}

		// Y軸移動処理
		if (isMoving)
		{
			MoveYAxisToTarget();

			// 追加: 移動中に周囲の enemy を検出して即倒す
			KillEnemiesAtPosition();
		}

		// prev を更新（次フレームの判定用）
		prevMagnetMode = currentMode;
	}

	// 開始時に落下目標 Y を決めてロックする（ここでは常に 0）
	void StartMovingAndLockTargetY()
	{
		if (landingTargetYSet) return;
		landingTargetY = 0f; // 常に地面Y=0
		landingTargetYSet = true;
		isMoving = true;
	}

	// 地面上にプレイヤーの磁石モードと「反対」のタグを持つオブジェクトがあるか調べる
	bool CheckForOppositePole(int currentMode)
	{
		string requiredTag = null;
		if (currentMode == 1) requiredTag = S_TAG;
		else if (currentMode == 2) requiredTag = N_TAG;
		else return false; // 無効モードなら false

		Vector3 center = transform.position + Vector3.up * checkHeightOffset;
		Collider[] cols = Physics.OverlapSphere(center, checkRadius);
		foreach (var c in cols)
		{
			if (c == null) continue;
			if (c.gameObject.CompareTag(requiredTag))
			{
				return true;
			}
		}

		return false;
	}

	// 移動中に周囲の enemy を即倒す
	void KillEnemiesAtPosition()
	{
		Collider[] cols = Physics.OverlapSphere(transform.position, enemyCheckRadius);
		foreach (var c in cols)
		{
			if (c == null) continue;
			enemy e = c.GetComponentInParent<enemy>();
			if (e != null)
			{
				e.TakeDamage(enemyDamage);
			}
		}
	}

	// Y軸のみを landingTargetY に向かって移動させる（到達時間指定 or 即時対応）
	void MoveYAxisToTarget()
	{
		float targetY = landingTargetY;
		Vector3 pos = transform.position;
		float distance = Mathf.Abs(pos.y - targetY);

		// instantDrop が有効なら一瞬で移動してロック
		if (instantDrop)
		{
			transform.position = new Vector3(pos.x, targetY, pos.z);
			LockInPlace();
			return;
		}

		float effectiveSpeed;
		if (dropDuration > 0f)
		{
			effectiveSpeed = distance / dropDuration;
			effectiveSpeed *= Mathf.Max(0.01f, speedMultiplier);
		}
		else
		{
			effectiveSpeed = moveSpeed * Mathf.Max(0.01f, speedMultiplier);
		}

		effectiveSpeed = Mathf.Max(effectiveSpeed, minDropSpeed);

		float newY = Mathf.MoveTowards(pos.y, targetY, effectiveSpeed * Time.deltaTime);
		transform.position = new Vector3(pos.x, newY, pos.z);

		if (Mathf.Abs(newY - targetY) <= 0.01f)
		{
			transform.position = new Vector3(pos.x, targetY, pos.z);
			LockInPlace();
		}
	}

	// 一度着地したら完全に固定する（物理がある場合は isKinematic + FreezeAll）
	void LockInPlace()
	{
		if (hasLanded) return;

		hasLanded = true;
		isMoving = false;

		if (rb != null)
		{
			rb.isKinematic = true;
			rb.constraints = RigidbodyConstraints.FreezeAll;
		}

		// 着地後、同オブジェクト（子含む）にある jump スクリプトを有効化する
		jump jumpScript = GetComponentInChildren<jump>();
		if (jumpScript != null)
		{
			jumpScript.enabled = true;
		}

		// このスクリプトは不要なので無効化して以後の判定を止める
		this.enabled = false;
	}
}