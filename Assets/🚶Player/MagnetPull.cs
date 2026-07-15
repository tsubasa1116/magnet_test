using UnityEngine;

// Catch中(ZRホールド)に、画面中央のRayで「逆極」の対象に作用する。対象の種類で分岐:
//   ・Grapple 点      → 立体機動(ワイヤーで飛びつく)
//   ・RopewayMagnet    → ロープウェイに吸着して運ばれる
//   ・それ以外(物体)  → 手元(handPoint)へ引き寄せる
// 逆極の対応: プレイヤー S極 → "N_Pole" / N極 → "S_Pole"
// ・ZRを離す(Catch終了)で全て解除。物体は保持中に極を切り替えると反発でぶっ飛ばす。
[RequireComponent(typeof(PlayerCatch))]
[RequireComponent(typeof(PlayerStateMachine))]
public class MagnetPull : MonoBehaviour
{
	[Header("参照")]
	[Tooltip("引き寄せた物体がくっつく位置(手のボーンなど)。未指定なら体の前方")]
	[SerializeField] private Transform handPoint;
	[Tooltip("中央Ray用カメラ。未指定なら Camera.main")]
	[SerializeField] private Camera aimCamera;

	[Header("Ray")]
	[SerializeField] private float rayRange = 30f;
	[SerializeField] private LayerMask rayMask = ~0;

	[Header("引き寄せ(磁石っぽい浮遊感)")]
	[Tooltip("小さいほど機敏に吸い寄せ、大きいほどゆっくり漂って近づく")]
	[SerializeField] private float pullSmoothTime = 0.25f;
	[SerializeField] private float maxPullSpeed = 40f;
	[SerializeField] private float attachDistance = 0.3f;
	[Tooltip("引き寄せ中の上下のゆらぎ(浮遊感)。近づくほど弱まる")]
	[SerializeField] private float floatAmplitude = 0.15f;
	[SerializeField] private float floatFrequency = 6f;

	[Header("反発(極切替でぶっ飛ばす)")]
	[SerializeField] private float repelForce = 30f;

	private PlayerCatch catchState;
	private PlayerStateMachine stateMachine;

    [Header("引き寄せエフェクト")]
    [SerializeField] private GameObject nPoleAttractEffect;
    [SerializeField] private GameObject sPoleAttractEffect;
    private GameObject currentAttractEffect;

    [Header("発射エフェクト")]
    [SerializeField] private GameObject nPoleReleaseEffect;
    [SerializeField] private GameObject sPoleReleaseEffect;

    [Header("レーザーエフェクト")]
    [SerializeField] private GameObject nPoleLaserEffect;
    [SerializeField] private GameObject sPoleLaserEffect;

    private GameObject currentLaser;
    private LineRenderer currentLine;

    private Rigidbody held;
	private Vector3 pullVel;           // SmoothDamp用の速度
	private Collider[] heldColliders;  // 保持中に無効化するコライダー
	private bool attached;
	private MagnetState grabbedPole;
	private bool savedUseGravity;
	private bool savedIsKinematic;
	private AuraRing heldAura;   // 掴んだ物のオーラ(持った通知用)
	private Bomb heldBomb;       // 掴んだ物が爆弾なら投擲フラグ用

	// --- ギミック相互作用（引き寄せ対象がギミックなら、物体を引くのではなくこちらが作用する） ---
	private Rigidbody playerRb;            // 自分のRigidbody(ジャンプ台で使う)
	private Grapple currentGrapple;        // 立体機動中の対象
	private RopewayMagnet currentRopeway;  // ロープウェイ吸着中の対象
    private jump currentJumpStand;
    [SerializeField] private float jumpCooldown;

    private bool Interacting => held != null || currentGrapple != null || currentRopeway != null;

	// エフェクト等が参照する：引き寄せ中/保持中の対象(無ければnull)
	public Transform HeldObject => held != null ? held.transform : null;

    // エネミー専用の吸着位置微調整用
    [Header("敵の吸着位置調整")]
    [Tooltip("X:左右, Y:上下, Z:前後")]
    [SerializeField] private Vector3 enemyAttachOffset = new Vector3(0.0f, -0.2f, -1.0f);
    [Tooltip("引き寄せ中の敵の回転速度")]
    [SerializeField] private float pullRotateSpeed = 4.0f;

    void Awake()
	{
		catchState = GetComponent<PlayerCatch>();
		stateMachine = GetComponent<PlayerStateMachine>();
		playerRb = GetComponent<Rigidbody>();
		if (aimCamera == null) aimCamera = Camera.main;
	}

    void Update()
    {
		// ジャンプの処理
        if (jumpCooldown > 0f) jumpCooldown -= Time.deltaTime;

        if (!catchState.IsCatching)
        {
            EndInteraction();
            return;
        }

        if (currentJumpStand != null && jumpCooldown <= 0f && currentJumpStand.CanLaunch(stateMachine))
        {
            currentJumpStand.Launch(playerRb);
            jumpCooldown = 0.6f;
            return;
        }

        if (!Interacting)
        {
            TryInteract();
        }
        else if (stateMachine.CurrentState != grabbedPole)
        {
            // 保持中に極を切り替えた
            if (held != null)
                Repel();
            else
                EndInteraction();
        }

        if (currentLine != null && held != null)
        {
            currentLine.useWorldSpace = true;

            currentLine.SetPosition(0, HandPos);
            currentLine.SetPosition(1, held.worldCenterOfMass);

            currentLaser.transform.position = Vector3.zero;
        }
        else if (attached)
        {
            DestroyEffect();
        }

        if (held != null)
        {
            Debug.Log($"Held : {held.name}");
        }
    }

    void FixedUpdate()
	{
		// 物体の引き寄せだけ毎物理ステップで動かす（ギミックは各自で動く）
		if (held != null && !attached) PullHeld();
	}

	// 相互作用の終了（ZR離し・極切替）
	private void EndInteraction()
	{
		if (held != null) Release();
		if (currentGrapple != null)
		{
			currentGrapple.StopGrapple();
			currentGrapple = null;
		}
		if (currentRopeway != null)
		{
			currentRopeway.DetachPlayer();
			currentRopeway = null;
		}
	}

	private Transform HandParent => handPoint != null ? handPoint : transform;
	private Vector3 HandPos => handPoint != null
		? handPoint.position
		: transform.position + transform.forward * 0.8f + Vector3.up * 1f;

	private string WantedTag()
		=> stateMachine.CurrentState == MagnetState.S ? "N_Pole" : "S_Pole";

	// 引き寄せ対象を判定して、種類ごとの作用を起動する
	private void TryInteract()
	{
		if (aimCamera == null) return;

        if (held != null || attached) return;

        Ray ray = aimCamera.ScreenPointToRay(
			new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));

		// ギミック(ジャンプ台/ロープウェイ)はトリガーコライダーなので Collide で拾う。
		// 距離順に見て、自分は無視・非対象のトリガーは透過・非対象のソリッド(壁)で遮断。
		RaycastHit[] hits = Physics.RaycastAll(ray, rayRange, rayMask, QueryTriggerInteraction.Collide);
		System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

		foreach (var hit in hits)
		{
			Collider col = hit.collider;
			if (col.transform.IsChildOf(transform)) continue; // 自分は無視

			if (!col.CompareTag(WantedTag()))
			{
				if (col.isTrigger) continue; // 非対象のトリガーは視線を遮らない
				return;                       // 非対象のソリッド(壁等)で遮られる
			}

			// ① 立体機動：Grapple 点
			Grapple grapple = col.GetComponentInParent<Grapple>();
			if (grapple != null)
			{
				grabbedPole = stateMachine.CurrentState;
				grapple.StartGrapple(gameObject); // 内部で StartGrappleEffect も呼ばれる
				currentGrapple = grapple;
				return;
			}

			// ③ ロープウェイ吸着
			RopewayMagnet ropeway = col.GetComponentInParent<RopewayMagnet>();
			if (ropeway != null)
			{
				grabbedPole = stateMachine.CurrentState;
				ropeway.AttachPlayer(gameObject);
				currentRopeway = ropeway;
				return;
			}

			// ④ それ以外：通常の物体引き寄せ
			Rigidbody rb = col.attachedRigidbody;
			if (rb != null) Grab(rb);
			return;
		}
	}

	private void Grab(Rigidbody rb)
	{
  if (held != null)
    {
        Debug.Log("Grabキャンセル");
        return;
    }

        held = rb;

        // 敵だったら「引き寄せられた」ことを通知
        enemy enemyScript = rb.GetComponentInParent<enemy>();
        if (enemyScript != null) enemyScript.OnMagnetGrabbed();

        attached = false;
		pullVel = Vector3.zero;
		grabbedPole = stateMachine.CurrentState;

		// 引き寄せ中はキネマティックにして確実に・滑らかに動かす
		savedUseGravity = rb.useGravity;
		savedIsKinematic = rb.isKinematic;
		rb.isKinematic = true;
		rb.useGravity = false;
		rb.linearVelocity = Vector3.zero;
		rb.angularVelocity = Vector3.zero;

		// 保持中はコライダーを無効化（プレイヤーや地面を押さない）
		heldColliders = rb.GetComponentsInChildren<Collider>();
		foreach (var c in heldColliders) c.enabled = false;

		// オーラ/爆弾との連携
		heldAura = rb.GetComponentInChildren<AuraRing>();
		heldBomb = rb.GetComponent<Bomb>();
		if (heldBomb != null) heldBomb.isThrown = false;

        // レーザー生成
        if (currentLaser == null)
        {
            Debug.Log("レーザー生成");

            GameObject laserPrefab = stateMachine.CurrentState == MagnetState.N ? nPoleLaserEffect : sPoleLaserEffect;

            if (laserPrefab != null)
            {
                currentLaser = Instantiate(laserPrefab, null);
				currentLine = currentLaser.GetComponentInChildren<LineRenderer>();

                if (currentLine != null)
                {
                    currentLine.useWorldSpace = true;
                    currentLine.positionCount = 2;

                    currentLine.SetPosition(0, HandPos);
                    currentLine.SetPosition(1, held.worldCenterOfMass);
                }

                Debug.Log(currentLine);
            }
        }

        // プレイヤー側エフェクト
        if (currentAttractEffect == null)
        {
            Debug.Log("吸引エフェクト生成");

            GameObject attractPrefab = stateMachine.CurrentState == MagnetState.N ? nPoleAttractEffect : sPoleAttractEffect;

            if (attractPrefab != null)
            {
                currentAttractEffect = Instantiate(attractPrefab, HandParent);

                currentAttractEffect.transform.localPosition = Vector3.zero;
                currentAttractEffect.transform.localRotation = Quaternion.identity;
            }
        }
    }

    private void PullHeld()
    {
        if (held == null) return;

        bool isEnemy = held.GetComponentInParent<enemy>() != null;

        // ユーザー様の設計通り、引き寄せ中の目標地点はピュアに手元（HandPos）にする
        Vector3 targetPos = HandPos;

        // 移動の計算基準を Rigidbody の物理位置ではなく、確実なトランスフォーム位置にする
        Vector3 currentPos = held.transform.position;
        Vector3 to = targetPos - currentPos;

        // トランスフォーム基準で距離を測り、くっつき判定
        if (to.magnitude <= attachDistance)
        {
            Attach();
            return;
        }

        // エネミー引き寄せ中の滑らかな横回転処理
        if (isEnemy)
        {
            Vector3 lookDir = -HandParent.forward;
            lookDir.y = 0;
            if (lookDir == Vector3.zero) lookDir = -transform.forward;

            Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.up);

            // トランスフォームの回転を直接補間
            held.transform.rotation = Quaternion.Slerp(
                held.transform.rotation,
                targetRot,
                Time.fixedDeltaTime * pullRotateSpeed
            );
        }

        // 上下のゆらぎ（浮遊感）
        float distFactor = Mathf.Clamp01(to.magnitude / 3f);
        Vector3 bob =
            Vector3.up *
            Mathf.Sin(Time.time * floatFrequency) *
            floatAmplitude *
            distFactor;

        Vector3 target = targetPos + bob;

        // MovePosition を使わず、直接トランスフォームの位置を MoveTowards で近づける！
        // これにより物理同期の遅延（ゴースト現象）が100%解消され、リアルタイムに吸い寄せられます。
        float speed = maxPullSpeed * Time.fixedDeltaTime;
        held.transform.position = Vector3.MoveTowards(currentPos, target, speed);
    }

    private void Attach()
    {
        attached = true;
        held.transform.SetParent(HandParent); // 手に追従

        bool isEnemy = held.GetComponentInParent<enemy>() != null;
        if (isEnemy)
        {
            // 手にくっついた瞬間のローカル座標を、設定したオフセットに直接固定！
            // これで重心の計算に惑わされず、ピタッと狙った位置に固定されます。
            held.transform.localPosition = enemyAttachOffset;

            Quaternion rotationY = Quaternion.Euler(0f, 180f, 0f);

            // 必要であれば、吸い寄せた時に敵の回転をプレイヤーの正面に向ける
            held.transform.localRotation = rotationY;
        }
        else
        {
            // 【通常オブジェクトの場合】これまでの挙動通り、手元（原点）にぴったりくっつける
            held.transform.localPosition = Vector3.zero;
        }

        if (heldAura != null) heldAura.SetHeld(true);

        DestroyEffect();
    }

    // ZRを離した：物理を元に戻して落とす
    private void Release()
	{
        DestroyEffect();

        if (attached) held.transform.SetParent(null);
		RestoreColliders();
		if (heldAura != null) heldAura.SetHeld(false);
		held.isKinematic = savedIsKinematic;
		held.useGravity = savedUseGravity;

        // 敵だったら「そっと離された」ことを通知
        enemy enemyScript = held.GetComponentInParent<enemy>();
        if (enemyScript != null) enemyScript.OnMagnetReleased();

        ClearHeld();
	}

	// 極切替：自分の向いている方向へぶっ飛ばす
	private void Repel()
	{
        DestroyEffect();

        // 発射エフェクトを再生
        PlayReleaseEffect();

        // もし掴んでいる物が ThrowableObject なら Throw() を呼ぶ
        ThrowableObject throwable = held.GetComponent<ThrowableObject>();

        if (throwable != null)	throwable.Throw();

        Vector3 dir = transform.forward;
		if (attached) held.transform.SetParent(null);
		RestoreColliders();
		if (heldAura != null) heldAura.SetHeld(false);
		if (heldBomb != null) heldBomb.isThrown = true;
		held.isKinematic = false;
		held.useGravity = true;
		held.AddForce(dir * repelForce, ForceMode.Impulse);

        // 敵だったら「吹っ飛ばされた」ことを通知
        enemy enemyScript = held.GetComponentInParent<enemy>();
        if (enemyScript != null) enemyScript.OnMagnetRepelled();

        ClearHeld();
	}

	private void RestoreColliders()
	{
		if (heldColliders == null) return;
		foreach (var c in heldColliders)
			if (c != null) c.enabled = true;
	}

	private void ClearHeld()
	{
		held = null;
		heldColliders = null;
		heldAura = null;
		heldBomb = null;
		attached = false;
		pullVel = Vector3.zero;
	}

    public void SetCurrentJumpStand(jump stand)
    {
        currentJumpStand = stand;
    }

    private void PlayReleaseEffect()
    {
        if (HandParent == null) return;

        GameObject effectPrefab = stateMachine.CurrentState == MagnetState.N ? nPoleReleaseEffect : sPoleReleaseEffect;

        if (effectPrefab != null)
        {
            GameObject effect = Instantiate(effectPrefab, HandParent.position, HandParent.rotation);
            Destroy(effect, 2f);   // 2秒後に削除
        }
    }

    void DestroyEffect()
    {
        if (currentLaser != null)
        {
            Destroy(currentLaser);
            currentLaser = null;
            currentLine = null;
        }

        if (currentAttractEffect != null)
        {
            Destroy(currentAttractEffect);
            currentAttractEffect = null;
        }
    }

}
