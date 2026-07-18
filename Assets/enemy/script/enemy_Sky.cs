using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class enemy_Sky : MonoBehaviour
{
    public enum EnemyState
    {
        Wait,   // 待機
        Notice, // 発見
        Chase,  // 追跡
        Attack, // 攻撃・距離保持
        Search, // 探索
        Return, // 帰還
        MagnetPulled, // 吸い寄せ・保持状態
        MagnetThrown, // 発射・落下状態
        Hit           // 壁激突ダウン状態
    }

    [Header("パラメータ")]
    [SerializeField] private float maxHp = 100.0f;
    [SerializeField] private float found = 10.0f;
    [SerializeField] private float attackRange = 8.0f;
    [SerializeField] private float distance = 5.0f;
    [SerializeField] private float searchTime = 3.0f;
    [SerializeField] private float searchRadius = 5.0f;
    [SerializeField] private float noticeTime = 1.0f;

    [Header("攻撃")]
    [SerializeField] private float attackInterval = 2.0f;
    [SerializeField] private float beamSpeed = 10.0f;
    private bool isAttack = false;
    private bool hasFoundPlayer = false;
    [Header("浮遊高度")]
    [SerializeField] private float hoverHeight = 2.0f;     // 地面からの基本の高さ
    [SerializeField] private float hoverRange = 0.5f;
    [SerializeField] private float hoverSpeed = 2.0f;

    [Header("環境磁力（ステージ用）")]
    [SerializeField] private float magnetRadius = 8.0f;
    [SerializeField] private float magnetForce = 50.0f;

    [Header("激突ヒット")]
    [Tooltip("壁や地面に激突したとみなす最小の衝撃（速度）")]
    [SerializeField] private float hitImpactThreshold = 5.0f;
    [Tooltip("壁に激突してから復帰するまでの時間")]
    [SerializeField] private float hitDuration = 1.5f;
    private float hitTimer;
    private float recoveryCooldown = 0f;

    [Header("ビジュアル（吸収時の高さバグ対策用）")]
    [Tooltip("敵の3Dモデル（グラフィック）のトランスフォーム。空中で浮いているモデルを、吸収時に親（コライダー）の中心に引き戻すために使用します。未指定の場合はAnimatorがあるオブジェクトを自動で対象にします")]
    [SerializeField] private Transform modelTransform;
    private Vector3 originalModelLocalPosition; // 元の浮遊高度を記録

    [Header("参照")]
    [SerializeField] private Transform targetPlayer;
    [SerializeField] private GameObject markExclamation;
    [SerializeField] private GameObject markQuestion;
    [SerializeField] private Transform effectPoint;

    [Header("レーザー")]
    [SerializeField] private GameObject Laser;    // レーザー
    [SerializeField] private GameObject originPrefab;
    [SerializeField] private Transform firePoint;   // 発射位置
    [SerializeField] private float chargeTime = 0.5f;
    [SerializeField] private float originDelay = 0.2f;

    [Header("エフェクト")]
    [SerializeField] private GameObject enemyHitEffect;
    [SerializeField] private GameObject enemyFloatingEffect;
    [SerializeField] private GameObject enemyDeathEffect;

    private float currentHp;
    private NavMeshAgent agent;
    private Rigidbody rb;
    private Animator anim;

    private Vector3 startPosition;

    private EnemyState currentState = EnemyState.Wait;
    private float searchTimer;
    private float noticeTimer;
    private float attackTimer;

    private bool isMagnetized = false;
    private float timeOffset;

    // 投げられてから強制復帰するまでのセーフティタイマー（無限に飛んでいくのを防ぐ）
    private float thrownSafetyTimer = 0f;
    private const float ThrownSafetyDuration = 3.0f; // 3秒経ったらどこにいても強制着地

    private bool IsAgentActiveAndOnNavMesh => agent != null && agent.enabled && agent.isOnNavMesh;

    public void SetTarget(Transform player)
    {
        targetPlayer = player;
    }

    void Start()
    {
        currentHp = maxHp;

        // ロックオン用のタグ"Enemy"が未設定なら自動で付与する(磁極タグは上書きしない)
        if (gameObject.CompareTag("Untagged")) gameObject.tag = "Enemy";

        agent = GetComponent<NavMeshAgent>();
        NavMeshAgentBootstrap.EnsureOnNavMesh(agent);
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // 浮遊高度初期設定
        if (agent != null) agent.baseOffset = hoverHeight;

        // モデル（ビジュアル）位置の自動取得と初期位置の記録
        if (modelTransform == null && anim != null)
        {
            modelTransform = anim.transform;
        }
        if (modelTransform != null)
        {
            originalModelLocalPosition = modelTransform.localPosition;
        }

        startPosition = transform.position;
        timeOffset = Random.Range(0f, 100f);

        if (markExclamation != null) markExclamation.SetActive(false);
        if (markQuestion != null) markQuestion.SetActive(false);

        if (enemyFloatingEffect != null)
        {
            GameObject effect = Instantiate(enemyFloatingEffect, transform.position, Quaternion.identity, transform);
            effect.transform.localPosition = new Vector3(0, -0.3f, 0);
        }
    }

    private void OnEnable()
    {
        PlayerHealth.OnPlayerDied += OnPlayerDied;
    }

    private void OnDisable()
    {
        PlayerHealth.OnPlayerDied -= OnPlayerDied;
    }

    private void OnPlayerDied()
    {
        attackTimer = 0f;

        StopAllCoroutines();

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            ChangeState(EnemyState.Wait);
        }
    }

    void FixedUpdate()
    {
        if (currentState == EnemyState.MagnetThrown)
        {
            HandleMagneticRecovery();

            // 投げられ中の無限飛散を防ぐセーフティタイマー
            thrownSafetyTimer -= Time.fixedDeltaTime;
            if (thrownSafetyTimer <= 0f)
            {
                Debug.LogWarning($"{gameObject.name} が着地しないため、セーフティが作動し、強制的にNavMeshへ復帰させます。");
                RecoverToNavMesh();
            }
        }
        else if (currentState == EnemyState.Hit)
        {
            HandleHitRecovery();
        }

        if (currentState != EnemyState.MagnetPulled &&
            currentState != EnemyState.MagnetThrown &&
            currentState != EnemyState.Hit)
        {
            MagneticInteraction();
        }
    }

    void Update()
    {
        if (targetPlayer == null) return;

        if (attackTimer > 0f) attackTimer -= Time.deltaTime;

        PlayerHealth health = targetPlayer.GetComponent<PlayerHealth>();
        if (health != null && health.IsDead) return;

        // プレイヤーの磁力操作中、または激突ダウン中はAIや通常の浮遊処理を完全ストップ
        if (currentState == EnemyState.MagnetPulled ||
            currentState == EnemyState.MagnetThrown ||
            currentState == EnemyState.Hit) return;

        if (IsAgentActiveAndOnNavMesh)
        {
            agent.baseOffset = hoverHeight + Mathf.Sin((Time.time + timeOffset) * hoverSpeed) * hoverRange;
        }


        // 磁力で飛ばされている間はAIの思考（追跡など）をストップする
        if (isMagnetized) return;
        if (targetPlayer == null) return;

        // 磁力で飛ばされている間はAIの思考（追跡など）をストップする
        if (isMagnetized) return;
        if (targetPlayer == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);

        switch (currentState)
        {
            case EnemyState.Wait:
                if (distanceToPlayer <= found) ChangeState(EnemyState.Notice);
                break;

            case EnemyState.Notice:
                noticeTimer -= Time.deltaTime;
                if (noticeTimer <= 0) ChangeState(EnemyState.Attack);
                break;

            case EnemyState.Attack:
                if (distanceToPlayer > found + 5.0f)
                {
                    ChangeState(EnemyState.Search);
                }
                else
                {
                    if (distanceToPlayer < distance)
                    {
                        if (IsAgentActiveAndOnNavMesh)
                        {
                            agent.isStopped = false;
                            Vector3 dirAway = (transform.position - targetPlayer.position).normalized;
                            Vector3 retreatPos = transform.position + dirAway * 2.0f;
                            agent.SetDestination(retreatPos);
                        }
                        AimAndAttack();
                    }
                    else if (distanceToPlayer > attackRange)
                    {
                        if (IsAgentActiveAndOnNavMesh)
                        {
                            agent.isStopped = false;
                            agent.SetDestination(targetPlayer.position);
                        }
                    }
                    else
                    {
                        if (IsAgentActiveAndOnNavMesh)
                        {
                            agent.isStopped = true;
                        }
                        AimAndAttack();
                    }
                }
                break;

            case EnemyState.Search:
                if (distanceToPlayer <= found) ChangeState(EnemyState.Notice);
                break;
        }

        // ★通常時（プレイヤーに掴まれていない・吹っ飛ばされていない）のみ元の浮遊高度に戻る補間を行う
        if (modelTransform != null && modelTransform != transform)
        {
            modelTransform.localPosition = Vector3.MoveTowards(
                modelTransform.localPosition,
                originalModelLocalPosition,
                Time.deltaTime * 3f
            );
        }
    }

    // ==========================================
    // 磁力システムインターフェース
    // ==========================================

    public void OnMagnetGrabbed()
    {
        isMagnetized = false;

        // ★【ガクッ対策】吸収された瞬間、見た目の位置が変わらないように「現在のワールド座標」を一時保存
        Vector3 visualWorldPos = modelTransform != null ? modelTransform.position : transform.position;

        if (agent.enabled)
        {
            // agent.baseOffset = 0f; // ←【削除】ここでリセットするとパッと位置が落ちるので廃止
            agent.enabled = false;
        }

        ChangeState(EnemyState.MagnetPulled);

        // ★【ガクッ対策】親（transform）を元の位置に維持しつつ、modelTransformもワールド位置をキープしたままローカル座標のみ0へ滑らかに移行させる準備
        // 一瞬でのリセット（modelTransform.localPosition = Vector3.zero;）を廃止し、手元のスクリプト側の補間に完全に任せます。
        if (modelTransform != null && modelTransform != transform)
        {
            // 親オブジェクトが手元に近づくのに合わせて、モデル位置も自然に中心（Zero）へ吸いつくようになります
            modelTransform.position = visualWorldPos;
        }

        // 実行中のすべての攻撃コルーチンを強制停止
        StopAllCoroutines();
        isAttack = false;

        // 攻撃アニメキャンセル
        if (anim != null)
        {
            anim.ResetTrigger("attack");
            anim.Play("Idle", 0, 0.0f);
            anim.SetBool("isPulled", true);
        }
    }

    public void OnMagnetReleased()
    {
        ChangeState(EnemyState.MagnetThrown);
        recoveryCooldown = 0.2f;
        thrownSafetyTimer = ThrownSafetyDuration;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearDamping = 1.0f; // 空気抵抗
        }

        if (anim != null) anim.SetBool("isPulled", false);
    }

    public void OnMagnetRepelled()
    {
        ChangeState(EnemyState.MagnetThrown);
        recoveryCooldown = 0.5f;
        thrownSafetyTimer = ThrownSafetyDuration;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearDamping = 1.0f; // 空気抵抗
        }

        if (anim != null)
        {
            anim.SetBool("isPulled", false);
            anim.SetBool("isThrown", true);
        }
    }

    private void HandleMagneticRecovery()
    {
        if (recoveryCooldown > 0f)
        {
            recoveryCooldown -= Time.fixedDeltaTime;
            return;
        }

        // 失速してゆっくりになったら地面に復帰
        if (rb.linearVelocity.magnitude < 0.5f)
        {
            RecoverToNavMesh();
        }
    }

    private void HandleHitRecovery()
    {
        hitTimer -= Time.fixedDeltaTime;

        bool isStoppedOnGround = rb.linearVelocity.magnitude < 0.3f && hitTimer < (hitDuration - 0.1f);

        if (isStoppedOnGround || hitTimer <= 0f)
        {
            RecoverToNavMesh();
        }
    }

    private void RecoverToNavMesh()
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 10.0f, NavMesh.AllAreas))
        {
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.linearDamping = 0f; // 空気抵抗をゼロに戻す
            }

            transform.position = hit.position;

            // ★【高度復帰対策】NavMesh復帰時に、AgentのbaseOffsetを初期値にリセット
            if (agent != null)
            {
                agent.baseOffset = hoverHeight;
            }
            agent.enabled = true;

            if (anim != null)
            {
                anim.SetBool("isThrown", false);
                anim.SetBool("isHit", false);
            }

            ChangeState(EnemyState.Attack);
        }
    }

    // ==========================================

    private void AimAndAttack()
    {
        Vector3 lookDir = targetPlayer.position - transform.position;
        lookDir.y = 0;
        if (lookDir != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 5.0f);
        }

        Attack();
    }

    private void Attack()
    {
        if (attackTimer <= 0f)
        {
            FireBeam();
            attackTimer = attackInterval;
        }
    }

    private void FireBeam()
    {
        if (Laser == null || targetPlayer == null) return;

        if (anim != null)
        {
            anim.SetTrigger("attack");

            StartCoroutine(SpawnOriginDelay());
        }
    }

    //アニメーションイベントで特定のフレームから呼び出すようの関数
    public void SpawnBeam()
    {
        if (currentState == EnemyState.MagnetPulled ||
            currentState == EnemyState.MagnetThrown ||
            currentState == EnemyState.Hit) return;

        if (Laser  == null || targetPlayer == null) return;

        // プレイヤーの方向を計算[]
        Vector3 targetPos = effectPoint.position;
        targetPos.y += 0.4f;

        Vector3 direction = (targetPos - transform.position).normalized;
        Vector3 spawnPos = transform.position + direction * 0.8f;
        GameObject firedBeam = Instantiate(Laser, spawnPos, Quaternion.LookRotation(direction));

        Rigidbody beamRb = firedBeam.GetComponent<Rigidbody>();
        if (beamRb != null)
            beamRb.linearVelocity = direction * beamSpeed;
    }

    private IEnumerator SpawnOriginDelay()
    {
        yield return new WaitForSeconds(originDelay);

        SpawnOrigin();
    }

    private GameObject SpawnOrigin()
    {
        if (originPrefab == null || firePoint == null || targetPlayer == null) return null;

        // FirePoint からプレイヤーへ向けて生成し、予告エフェクトが常にプレイヤー側を向くようにする。
        Vector3 directionToPlayer = effectPoint.position - firePoint.position;
        if (directionToPlayer.sqrMagnitude < Mathf.Epsilon) return null;

        GameObject origin = Instantiate(
            originPrefab,
            firePoint.position,
            Quaternion.FromToRotation(Vector3.up, directionToPlayer.normalized),
            firePoint
        );

        return origin;
    }

    private void ChangeState(EnemyState nextState)
    {
        currentState = nextState;

        if (currentState == EnemyState.MagnetPulled ||
            currentState == EnemyState.MagnetThrown ||
            currentState == EnemyState.Hit) return;

        if (!IsAgentActiveAndOnNavMesh) return;

        agent.isStopped = false;

        if (nextState == EnemyState.Wait)
        {
            if (hasFoundPlayer)
            {
                hasFoundPlayer = false;
            }

            if (agent.isOnNavMesh)
                agent.isStopped = true;
        }
        else if (nextState == EnemyState.Notice)
        {
            if (agent.isOnNavMesh)
                agent.isStopped = true;

            noticeTimer = noticeTime;

            if (!hasFoundPlayer)
            {
                hasFoundPlayer = true;
                CombatStateManager.Instance.EnterCombat(this.gameObject);
            }

            if (markExclamation != null)
                markExclamation.SetActive(true);

            StartCoroutine(HideMark(markExclamation, noticeTime));
        }
        else if (nextState == EnemyState.Attack)
        {
            // Update側
        }
        else if (nextState == EnemyState.Search)
        {
            if (hasFoundPlayer)
            {
                hasFoundPlayer = false;
                CombatStateManager.Instance.ExitCombat(this.gameObject);
            }

            if (markQuestion != null)
                markQuestion.SetActive(true);

            StartCoroutine(HideMark(markQuestion, 1.5f));

            searchTimer = searchTime;
            WanderAround();
        }
        else if (nextState == EnemyState.Return)
        {
            if (hasFoundPlayer)
            {
                hasFoundPlayer = false;
            }

            agent.SetDestination(startPosition);
        }
    }

    // 探索中にランダムな位置を目的地に設定する処理
    private void WanderAround()
    {
        if (!IsAgentActiveAndOnNavMesh) return;
        Vector3 randomPos = transform.position + Random.insideUnitSphere * searchRadius;
        NavMeshHit hit;

        if (NavMesh.SamplePosition(randomPos, out hit, searchRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    // マークを一定時間後に消す処理
    private IEnumerator HideMark(GameObject mark, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (mark != null) mark.SetActive(false);
    }

    private void OnCollisionEnter(Collision collision)
    {
        ThrowableObject throwable = collision.gameObject.GetComponent<ThrowableObject>();

        if (throwable != null && throwable.IsThrown)
        {
            TakeDamage(throwable.Damage);
            throwable.ResetThrown();
            return;
        }

        if (currentState == EnemyState.MagnetThrown)
        {
            if (collision.gameObject.CompareTag("Player")) return;

            float impactForce = collision.relativeVelocity.magnitude;

            Vector3 normal = collision.contacts[0].normal;
            bool isFloor = normal.y > 0.7f;

            if (isFloor)
            {
                RecoverToNavMesh();
            }
            else
            {
                if (impactForce >= hitImpactThreshold)
                {
                    TriggerHitCollision(impactForce);
                }
            }
        }
    }

    public void TakeDamage(float damageAmount)
    {
        currentHp -= damageAmount;

        // ダメージを受けたときのエフェクトを再生
        if (enemyHitEffect != null) Instantiate(enemyHitEffect, transform.position, Quaternion.identity);

        if (currentHp <= 0)
        {
            HitStop.Play(0.12f); // 倒した手応えのヒットストップ
            Die();
        }
    }

    private void Die()
    {
        if (hasFoundPlayer)
        {
            hasFoundPlayer = false;
        }

        CombatStateManager.Instance.ExitCombat(this.gameObject);

        if (enemyDeathEffect != null) Instantiate(enemyDeathEffect, transform.position, Quaternion.identity);
        Destroy(gameObject);
        GameManager.Instance.AddKill();
    }

    // 磁力をN極・S極として感知して力を受ける処理
    private void MagneticInteraction()
    {
        bool isMyN = gameObject.CompareTag("N_Pole");
        bool isMyS = gameObject.CompareTag("S_Pole");

        if (!isMyN && !isMyS) return;

        Collider[] colliders = Physics.OverlapSphere(transform.position, magnetRadius);
        bool feelingMagnet = false;
        Vector3 totalForce = Vector3.zero;

        Transform attachedTarget = null;
        float minDistance = float.MaxValue;

        foreach (Collider col in colliders)
        {
            if (col.gameObject == gameObject) continue;
            if (col.GetComponent<enemy>() != null || col.GetComponent<enemy_Sky>() != null) continue;

            bool isOtherN = col.CompareTag("N_Pole");
            bool isOtherS = col.CompareTag("S_Pole");

            if (isOtherN || isOtherS)
            {
                Vector3 dirToOther = col.transform.position - transform.position;
                float distance = dirToOther.magnitude;
                float safeDistance = distance < 0.5f ? 0.5f : distance;
                float force = magnetForce * (1.0f + (magnetRadius - safeDistance) / magnetRadius);

                if ((isMyN && isOtherS) || (isMyS && isOtherN))
                {
                    feelingMagnet = true;
                    if (distance < 2.0f)
                    {
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            attachedTarget = col.transform;
                        }
                    }
                    else
                    {
                        totalForce += dirToOther.normalized * force;
                    }
                }
                else if ((isMyN && isOtherN) || (isMyS && isOtherS))
                {
                    totalForce -= dirToOther.normalized * force;
                    feelingMagnet = true;
                }
            }
        }

        if (feelingMagnet)
        {
            if (agent.enabled)
            {
                agent.enabled = false;
                rb.isKinematic = false;
                isMagnetized = true;
                rb.linearDamping = 0.5f;
            }

            if (attachedTarget != null)
            {
                rb.mass = 0.01f;
                Vector3 stickDir = attachedTarget.position - transform.position;
                rb.linearVelocity = stickDir.normalized * 5f;
                rb.AddForce(stickDir.normalized * (magnetForce * 5f), ForceMode.Acceleration);
            }
            else
            {
                rb.mass = 1.0f;
                if (totalForce.magnitude > 0.1f)
                {
                    rb.AddForce(totalForce * Time.fixedDeltaTime, ForceMode.VelocityChange);
                }
            }
        }
        else if (isMagnetized)
        {
            if (rb.linearVelocity.magnitude < 0.5f)
            {
                rb.mass = 1.0f;
                rb.isKinematic = true;
                isMagnetized = false;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position, out hit, 2.0f + hoverHeight, NavMesh.AllAreas))
                {
                    transform.position = hit.position;
                    if (agent != null) agent.baseOffset = hoverHeight;
                    agent.enabled = true;
                }
            }
        }
    }

    private void TriggerHitCollision(float force)
    {
        ChangeState(EnemyState.Hit);
        hitTimer = hitDuration;

        if (rb != null)
        {
            // ★【大吹っ飛び対策①】壁衝突した瞬間の大バウンドを抑制するため、一度物理計算による速度・回転を完全に殺す
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.linearDamping = 0f; // 跳ね返り中に空気抵抗が邪魔をして異常加速するのを防ぐためにリセット

            rb.isKinematic = false;
            rb.useGravity = true;

            // ★【大吹っ飛び対策②】壁にぶつかった位置から、ポトッと真下に落とすための微弱な下向きの力を加える
            rb.linearVelocity = Vector3.down * 1.5f;
        }

        if (anim != null)
        {
            anim.SetBool("isThrown", false);
            anim.SetBool("isHit", true);
        }

        TakeDamage(maxHp / 3);

        Debug.Log($"{gameObject.name} (飛行) が壁に激突！ポトッと床へ自由落下させます。");
    }
}