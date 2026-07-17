using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class enemy : MonoBehaviour
{
    public enum EnemyState
    {
        Wait,   // 待機
        Notice, // 発見
        Chase,  // 追跡
        Attack, // 攻撃
        Search, // 索敵
        Return, // 帰還
        MagnetPulled, // 磁力で引き寄せられ・保持されている状態
        MagnetThrown,  // 磁力で発射・落下している状態
        Hit           // 壁などに激突してダウンしている状態
    }

    [Header("基本パラメータ")]
    [SerializeField] private float maxHp = 100.0f;
    [SerializeField] private float found = 7.5f;
    [SerializeField] private float attackRange = 2.0f;
    [SerializeField] private float searchTime = 3.0f;
    [SerializeField] private float searchRadius = 5.0f;
    [SerializeField] private float noticeTime = 1.0f;
    [SerializeField] private float lookBackSpeed = 8.0f;

    [Header("参照")]
    [SerializeField] private Transform targetPlayer;
    [SerializeField] private GameObject markExclamation;
    [SerializeField] private GameObject markQuestion;

    [Header("攻撃")]
    [SerializeField] private Animator anim;
    [SerializeField] private float attackInterval = 2.0f;
    [SerializeField] private int attackDamage = 10;
    private float attackTimer;

    [Header("エフェクト")]
    [SerializeField] private GameObject enemyHitEffect;
    [SerializeField] private GameObject enemyDeathEffect;

    [Header("激突ヒット")]
    [Tooltip("壁や地面に激突したとみなす最小の衝撃（速度）。これより速いスピードでぶつかったらヒットになる")]
    [SerializeField] private float hitImpactThreshold = 5.0f;
    [Tooltip("激突してから起き上がってAI（追跡）に復帰するまでの時間（秒）")]
    [SerializeField] private float hitDuration = 1.5f;
    private float hitTimer;

    private float currentHp;
    private NavMeshAgent agent;
    private Rigidbody rb;
    private Vector3 startPosition;

    private EnemyState currentState = EnemyState.Wait;
    private float searchTimer;
    private float noticeTimer;
    private bool isAttack = false;

    // 吹っ飛ばされた直後に即着地判定されるのを防ぐタイマー
    private float recoveryCooldown = 0f;

    private bool IsAgentActiveAndOnNavMesh => agent != null && agent.enabled && agent.isOnNavMesh;

    public void SetTarget(Transform player) => targetPlayer = player;

    void Start()
    {
        currentHp = maxHp;
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();

        if (rb != null) rb.isKinematic = true;
        startPosition = transform.position;

        if (markExclamation != null) markExclamation.SetActive(false);
        if (markQuestion != null) markQuestion.SetActive(false);
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
        isAttack = false;

        StopAllCoroutines();

        if (agent != null && agent.enabled)
        {
            agent.ResetPath();
            ChangeState(EnemyState.Wait);
        }
    }

    void FixedUpdate()
    {
        // 吹っ飛び・落下中の時だけ着地判定を行う
        if (currentState == EnemyState.MagnetThrown)
        {
            HandleMagneticRecovery();
        }
        // 激突ダウン中（壁からポトッと落ちて、床で起き上がるまで）の処理
        else if (currentState == EnemyState.Hit)
        {
            HandleHitRecovery();
        }
    }

    void Update()
    {
        if (targetPlayer == null) return;

        PlayerHealth health = targetPlayer.GetComponent<PlayerHealth>();
        if (health != null && health.IsDead) return;

        if (attackTimer > 0f) attackTimer -= Time.deltaTime;

        // 磁力で操作されている間、または【激突ダウン中】はAI処理を完全にストップ
        if (currentState == EnemyState.MagnetPulled ||
            currentState == EnemyState.MagnetThrown ||
            currentState == EnemyState.Hit) return;

        if (!IsAgentActiveAndOnNavMesh) return;
        if (targetPlayer == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);

        switch (currentState)
        {
            case EnemyState.Wait:
                if (distanceToPlayer <= found) ChangeState(EnemyState.Notice);
                break;
            case EnemyState.Notice:
                noticeTimer -= Time.deltaTime;
                if (noticeTimer <= 0) ChangeState(EnemyState.Chase);
                break;
            case EnemyState.Chase:
                if (distanceToPlayer <= attackRange) ChangeState(EnemyState.Attack);
                else if (distanceToPlayer > found + 5.0f) ChangeState(EnemyState.Search);
                else agent.SetDestination(targetPlayer.position);
                break;
            case EnemyState.Attack:
                if (distanceToPlayer > attackRange && !isAttack) ChangeState(EnemyState.Chase);
                else
                {
                    agent.isStopped = true;
                    anim.SetBool("run", false);
                    Vector3 directionToPlayer = targetPlayer.position - transform.position;
                    directionToPlayer.y = 0;
                    if (directionToPlayer != Vector3.zero)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(directionToPlayer), Time.deltaTime * lookBackSpeed);
                    }
                    Attack();
                }
                break;
            case EnemyState.Search:
                if (distanceToPlayer <= found) ChangeState(EnemyState.Notice);
                else
                {
                    searchTimer -= Time.deltaTime;
                    if (searchTimer <= 0) ChangeState(EnemyState.Return);
                    else if (IsAgentActiveAndOnNavMesh && agent.remainingDistance < 0.5f) WanderAround();
                }
                break;
            case EnemyState.Return:
                if (distanceToPlayer <= found) ChangeState(EnemyState.Notice);
                else if (IsAgentActiveAndOnNavMesh && agent.remainingDistance < 0.5f) ChangeState(EnemyState.Wait);
                break;
        }
    }

    // ==========================================
    // 磁力システムからの通知受け取り口
    // ==========================================

    public void OnMagnetGrabbed()
    {
        if (agent.enabled) agent.enabled = false;
        ChangeState(EnemyState.MagnetPulled);

        // 吸収中のループアニメーションをON
        if (anim != null) anim.SetBool("isPulled", true);
    }

    public void OnMagnetReleased()
    {
        ChangeState(EnemyState.MagnetThrown);
        recoveryCooldown = 0.2f; // そっと離した場合はすぐ着地判定してOK

        // アニメーションフラグをリセット
        if (anim != null) anim.SetBool("isPulled", false);
    }

    public void OnMagnetRepelled()
    {
        ChangeState(EnemyState.MagnetThrown);
        recoveryCooldown = 0.5f; // 吹っ飛んだ直後に着地させないよう0.5秒の猶予

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

        // 速度が落ちて静止に近づいたら復帰（※激突しなかった場合の通常着地用）
        if (rb.linearVelocity.magnitude < 0.5f)
        {
            RecoverToNavMesh();
        }
    }

    // ★修正：激突ダウン中の処理
    private void HandleHitRecovery()
    {
        hitTimer -= Time.fixedDeltaTime;

        // 壁からずり落ちて床に着き、速度がほぼ静止（0.3未満）した、またはタイマーが切れたら復帰
        // （激突した瞬間の静止バグを避けるため、激突から0.1秒以上経っていることも条件にします）
        bool isStoppedOnGround = rb.linearVelocity.magnitude < 0.3f && hitTimer < (hitDuration - 0.1f);

        if (isStoppedOnGround || hitTimer <= 0f)
        {
            // ここで初めて足元の床（NavMesh）にカチッとスナップさせて起き上がらせる！
            RecoverToNavMesh();
        }
    }

    // NavMeshに復帰する共通処理（最終的な位置の微調整と起き上がり）
    private void RecoverToNavMesh()
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 5.0f, NavMesh.AllAreas))
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            transform.position = hit.position;
            agent.enabled = true;

            // アニメーションフラグをすべてクリーンにリセット
            if (anim != null)
            {
                anim.SetBool("isThrown", false);
                anim.SetBool("isHit", false);
            }

            // 着地したら即座にプレイヤー追跡に戻る
            ChangeState(EnemyState.Chase);
        }
    }

    // ==========================================

    private void ChangeState(EnemyState nextState)
    {
        currentState = nextState;

        // 磁力制御中、および【激突ダウン中】はAIや既存のアニメーションを操作しない
        if (currentState == EnemyState.MagnetPulled ||
            currentState == EnemyState.MagnetThrown ||
            currentState == EnemyState.Hit) return;

        if (!IsAgentActiveAndOnNavMesh) return;

        agent.isStopped = false;

        if (nextState == EnemyState.Wait)
        {
            agent.isStopped = true;
            if (anim != null) { anim.SetBool("walk", false); anim.SetBool("run", false); anim.SetBool("idol", true); }
        }
        else if (nextState == EnemyState.Notice)
        {
            agent.isStopped = true;
            noticeTimer = noticeTime;
            if (markExclamation != null) markExclamation.SetActive(true);
            StartCoroutine(HideMark(markExclamation, noticeTime));
            if (anim != null) { anim.SetBool("walk", false); anim.SetBool("run", false); anim.SetBool("idol", false); }
        }
        else if (nextState == EnemyState.Chase)
        {
            agent.isStopped = false;
            if (anim != null) { anim.SetBool("idol", false); anim.SetBool("walk", false); anim.SetBool("run", true); }
        }
        else if (nextState == EnemyState.Search)
        {
            if (anim != null) { anim.SetBool("run", false); anim.SetBool("idle", false); anim.SetBool("walk", true); }
            if (markQuestion != null) markQuestion.SetActive(true);
            StartCoroutine(HideMark(markQuestion, 1.5f));
            searchTimer = searchTime;
            WanderAround();
        }
        else if (nextState == EnemyState.Return)
        {
            if (anim != null) { anim.SetBool("walk", false); anim.SetBool("run", true); }
            agent.SetDestination(startPosition);
        }
    }

    private void WanderAround()
    {
        if (!IsAgentActiveAndOnNavMesh) return;
        Vector3 randomPos = transform.position + Random.insideUnitSphere * searchRadius;
        if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, searchRadius, 1))
        {
            agent.SetDestination(hit.position);
        }
    }

    private IEnumerator HideMark(GameObject mark, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (mark != null) mark.SetActive(false);
    }

    private void Attack()
    {
        if (attackTimer <= 0f) { AttackEnemy(); attackTimer = attackInterval; }
    }

    [SerializeField, Range(0.0f, 1.0f)] private float damageDelay = 0.3f;
    private void AttackEnemy()
    {
        if (anim != null) anim.SetTrigger("beam");
        StartCoroutine(DelayDamageCoroutine());
        StartCoroutine(WaitAttackAnimation());
    }

    private IEnumerator DelayDamageCoroutine()
    {
        yield return new WaitForSeconds(damageDelay);
        if (targetPlayer == null || currentState != EnemyState.Attack) yield break;
        if (Vector3.Distance(transform.position, targetPlayer.position) <= attackRange + 0.5f)
        {
            PlayerHealth playerHealth = targetPlayer.GetComponent<PlayerHealth>();
            if (playerHealth != null) playerHealth.TakeDamage(attackDamage, transform.position);
        }
    }

    private IEnumerator WaitAttackAnimation()
    {
        isAttack = true;
        yield return anim.WaitForCurrentAnimationEnd();
        isAttack = false;
    }

    public void TakeDamage(float damageAmount)
    {
        currentHp -= damageAmount;
        if (enemyHitEffect != null) Instantiate(enemyHitEffect, transform.position, Quaternion.identity);

        if (currentHp <= 0)
        {
            HitStop.Play(0.12f); // 倒した手応えのヒットストップ
            Die();
        }
    }

    // ★修正：衝突判定（地面での激突ダメージを完全シャットアウト！）
    private void OnCollisionEnter(Collision collision)
    {
        // ① 投げられたオブジェクト（ThrowableObject）が当たった時の既存処理
        ThrowableObject throwable = collision.gameObject.GetComponent<ThrowableObject>();
        if (throwable != null && throwable.IsThrown)
        {
            TakeDamage(throwable.Damage);
            throwable.ResetThrown();
            return;
        }

        // ② 自身が「ぶっ飛んでいる最中（MagnetThrown）」に何かに激突した時の判定
        if (currentState == EnemyState.MagnetThrown)
        {
            if (collision.gameObject.CompareTag("Player")) return;

            float impactForce = collision.relativeVelocity.magnitude;

            // ぶつかった面の角度（法線）を取得
            // normal.y が 0.7 より大きい（＝上を向いている）場合は、なだらかな地面・床とみなす
            Vector3 normal = collision.contacts[0].normal;
            bool isFloor = normal.y > 0.7f;

            if (isFloor)
            {
                // 【地面に着地した場合】
                // ダメージは受けず、ワープもせず、安全にその場にスッと着地復帰させる
                RecoverToNavMesh();
            }
            else
            {
                // 【壁や障害物（横の壁、柱、傾斜の急な崖など）にぶつかった場合】
                // 一定以上のスピードであれば、ダメージ付きの壁激突を発生させる
                if (impactForce >= hitImpactThreshold)
                {
                    TriggerHitCollision(impactForce);
                }
            }
        }
    }

    // ★修正：壁激突時の処理（瞬間移動を無くし、ポトッと物理落下させる）
    private void TriggerHitCollision(float force)
    {
        ChangeState(EnemyState.Hit);
        hitTimer = hitDuration;

        // 1. 変な大跳ね返りをストップ
        // 物理(isKinematic = false)は有効にしたまま、勢いだけをカットして、力なくポトッと下に落ちるようにする
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.down * 0.5f; // 壁から少し剥がすようにわずかな下向き速度を与える
        rb.angularVelocity = Vector3.zero;

        // 2. アニメーション再生（痛がる・やられモーション）
        if (anim != null)
        {
            anim.SetBool("isThrown", false);
            anim.SetBool("isHit", true);
        }

        // 3. 激突ダメージを与える（★お好みで一律10ダメージなら TakeDamage(10f); に書き換えてください）
        TakeDamage(maxHp / 3);

        Debug.Log($"{gameObject.name} が壁に激突！ダメージを与えてポトッと床へ自由落下させます。");
    }

    private void Die()
    {
        if (enemyDeathEffect != null) Instantiate(enemyDeathEffect, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }
}