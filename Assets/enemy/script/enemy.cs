using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))] // 物理演算で吹き飛ばすために必要
public class enemy : MonoBehaviour
{
    private enum EnemyState
    {
        Wait,   // 待機（初期位置にいる）
        Notice, // 発見（立ち止まって警戒している）
        Chase,  // 追跡（プレイヤーを追いかけている）
        Attack, // 攻撃
        Search, // 索敵（周囲をうろうろ探している）
        Return  // 帰還（元の初期位置に戻っている）
    }

    [Header("基本パラメータ")]
    [SerializeField] private float maxHp = 100.0f;
    [SerializeField] private float found = 10.0f;
    [SerializeField] private float attackRange = 2.0f;
    [SerializeField] private float searchTime = 3.0f;   // 索敵している時間
    [SerializeField] private float searchRadius = 5.0f; // 索敵範囲
    [SerializeField] private float noticeTime = 1.0f;
    [SerializeField] private float lookBackSpeed = 8.0f; // プレイヤー注視の振り返る速度

    [Header("磁力パラメータ")] // 磁力の影響力の設定
    [SerializeField] private float magnetRadius = 8.0f;  // 磁力を感知する距離
    [SerializeField] private float magnetForce = 50.0f;  // 磁力の強さ

    [Header("参照")]
    [SerializeField] private Transform targetPlayer;
    [SerializeField] private GameObject markExclamation; // ！マーク
    [SerializeField] private GameObject markQuestion;    // ？マーク

    [Header("攻撃")]
    [SerializeField] private Animator anim;
    [SerializeField] private float attackInterval = 2.0f;
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float attackTimer;

    [Header("エフェクト")]
    [SerializeField] private GameObject enemyHitEffect;

    private float currentHp;
    private NavMeshAgent agent;
    private Rigidbody rb; // 物理演算用

    private Vector3 startPosition;

    private EnemyState currentState = EnemyState.Wait;
    private float searchTimer; // 索敵の残り時間をカウントするタイマー
    private float noticeTimer;

    private bool isMagnetized = false; // 磁力で制御されているかどうかを判定
    private bool isAttack = false; // 攻撃アニメーション中かどうか


    void Start()
    {
        currentHp = maxHp;
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();

        // 初期はNavMeshAgentで移動するため物理(Rigidbody)はオフにしておく
        if (rb != null) rb.isKinematic = true;

        startPosition = transform.position;

        if (markExclamation != null) markExclamation.SetActive(false);
        if (markQuestion != null) markQuestion.SetActive(false);
    }

    void FixedUpdate() // 物理演算に関する処理はFixedUpdateで行う
    {
        MagneticInteraction();
    }

    void Update()
    {
        if (attackTimer > 0f)
        {
            attackTimer -= Time.deltaTime;
        }

        // 磁力で飛ばされている間はAIの思考（追跡など）をストップさせる
        if (isMagnetized) return;

        if (targetPlayer == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);

        // 現在の状態によって行うことを変える
        switch (currentState)
        {
            // 待機
            case EnemyState.Wait:
                if (distanceToPlayer <= found) ChangeState(EnemyState.Notice);
                break;
            // 発見
            case EnemyState.Notice:
                noticeTimer -= Time.deltaTime;
                if (noticeTimer <= 0) ChangeState(EnemyState.Chase);
                break;
            // 追跡
            case EnemyState.Chase:
                if (distanceToPlayer <= attackRange) ChangeState(EnemyState.Attack);
                else if (distanceToPlayer > found + 5.0f) ChangeState(EnemyState.Search);
                else agent.SetDestination(targetPlayer.position);
                break;
            // 攻撃
            case EnemyState.Attack:
                // 攻撃範囲から出たら追跡に戻る
                if (distanceToPlayer > attackRange && !isAttack)
                {
                    ChangeState(EnemyState.Chase);
                }
                else
                {
                    agent.isStopped = true;
                    anim.SetBool("run", false);
                    // 常にプレイヤーの方向を向くようにする
                    Vector3 directionToPlayer = targetPlayer.position - transform.position;
                    directionToPlayer.y = 0; // Y軸無視

                    if (directionToPlayer != Vector3.zero)
                    {
                        Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * lookBackSpeed);
                    }

                    Attack();
                }
                break;
            // 索敵
            case EnemyState.Search:
                if (distanceToPlayer <= found) ChangeState(EnemyState.Notice);
                else
                {
                    searchTimer -= Time.deltaTime;
                    if (searchTimer <= 0) ChangeState(EnemyState.Return);
                    else if (agent.remainingDistance < 0.5f) WanderAround();
                }
                break;
            // 帰還
            case EnemyState.Return:
                if (distanceToPlayer <= found) ChangeState(EnemyState.Notice);
                else if (agent.remainingDistance < 0.5f) ChangeState(EnemyState.Wait);
                break;
        }
    }

    // 磁力の影響を受け、引き合ったり反発したりする処理
    private void MagneticInteraction()
    {
        // 自身のタグを確認
        bool isMyN = gameObject.CompareTag("N_Pole");
        bool isMyS = gameObject.CompareTag("S_Pole");

        if (!isMyN && !isMyS) return; // 磁力に対応していなければ処理しない

        Collider[] colliders = Physics.OverlapSphere(transform.position, magnetRadius);
        bool feelingMagnet = false;
        Vector3 totalForce = Vector3.zero;

        // 引っ付いている対象を記録
        Transform attachedTarget = null;
        float minDistance = float.MaxValue;

        foreach (Collider col in colliders)
        {
            if (col.gameObject == gameObject) continue; // 自身は除外

            // 相手が enemy (敵) なら引き合わない（反発のみにしたい場合など）
            if (col.GetComponent<enemy>() != null) continue;

            bool isOtherN = col.CompareTag("N_Pole");
            bool isOtherS = col.CompareTag("S_Pole");

            if (isOtherN || isOtherS)
            {
                // 自分から相手へのベクトルと距離
                Vector3 dirToOther = col.transform.position - transform.position;
                float distance = dirToOther.magnitude;

                // 近すぎる場合は0による除算などを防止
                float safeDistance = distance < 0.5f ? 0.5f : distance;

                // 距離が近いほど強く、遠いほど弱くなるようにする
                float force = magnetForce * (1.0f + (magnetRadius - safeDistance) / magnetRadius);

                // 違う極（引き合う）
                if ((isMyN && isOtherS) || (isMyS && isOtherN))
                {
                    feelingMagnet = true;

                    // 一定の距離に近づけば「まとわりつく」状態にするための判定
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
                        // まだ遠い場合は通常通り引き合う（ターゲットの方向に力が加わる）
                        totalForce += dirToOther.normalized * force;
                    }
                }
                // 同じ極（反発する）
                else if ((isMyN && isOtherN) || (isMyS && isOtherS))
                {
                    // ターゲットへの反対方向へ力のみ加える
                    totalForce -= dirToOther.normalized * force;
                    feelingMagnet = true;
                }
            }
        }

        // 磁力を受けている際の切り替え (NavMeshAgentからRigidbodyへの切り替え)
        if (feelingMagnet)
        {
            if (agent.enabled)
            {
                agent.enabled = false;   // 移動AIを一時停止
                rb.isKinematic = false;  // 物理演算をオン
                isMagnetized = true;

                // 吹っ飛んだりしすぎないために、空気抵抗を一時的に上げる
                rb.linearDamping = 0.5f;
            }

            // 引っ付く処理
            if (attachedTarget != null)
            {
                // 磁力の親などに影響を与えすぎないように質量(mass)を一時的に極小にして抵抗力をなくす
                rb.mass = 0.01f;

                // 相手の中心にむかって強制的に力で引っ張り続ける
                Vector3 stickDir = attachedTarget.position - transform.position;

                // すり抜けなどを防ぐため、速度を制限しつつ強制移動
                rb.linearVelocity = stickDir.normalized * 5f;

                // 強引に引っ張る(壁などを無視して突き抜けないようにAddForceを使う)
                rb.AddForce(stickDir.normalized * (magnetForce * 5f), ForceMode.Acceleration);
            }
            else
            {
                // 特に張り付いていない場合は質量を元に戻す
                rb.mass = 1.0f;

                if (totalForce.magnitude > 0.1f)
                {
                    // ForceMode.VelocityChange (質量無視で即座に加算) を使ってグワッと引き寄せる
                    rb.AddForce(totalForce * Time.fixedDeltaTime, ForceMode.VelocityChange);
                }
            }
        }
        else if (isMagnetized)
        {
            // 磁力の影響範囲から外れ、かつ速度が落ち着いたらAI(NavMesh)に戻る
            if (rb.linearVelocity.magnitude < 0.5f)
            {
                rb.mass = 1.0f; // 質量を元に戻す
                rb.isKinematic = true;
                isMagnetized = false;

                // NavMesh(移動床)の上にちゃんと着地できているか確認してから戻す
                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position, out hit, 2.0f, NavMesh.AllAreas))
                {
                    transform.position = hit.position;
                    agent.enabled = true;
                }
            }
        }
    }

    // ステートの切り替え処理
    private void ChangeState(EnemyState nextState)
    {
        currentState = nextState;
        if (!agent.enabled) return; // 物理で飛んでいる時はエラー防止

        agent.isStopped = false;

        if (nextState == EnemyState.Wait)
        {// 待機
            agent.isStopped = true;
            if (anim != null)
            {
                anim.SetBool("walk", false);
                anim.SetBool("run",  false);
                anim.SetBool("idol",  true);
            }
            
        }
        else if (nextState == EnemyState.Notice)
        {// 発見
            agent.isStopped = true;
            noticeTimer = noticeTime;
            if (markExclamation != null) markExclamation.SetActive(true);
            StartCoroutine(HideMark(markExclamation, noticeTime));
            if (anim != null)
            {
                anim.SetBool("walk", false);
                anim.SetBool("run",  false);
                anim.SetBool("idol", false);
            }
        }
        else if (nextState == EnemyState.Chase)
        {// 追跡
            agent.isStopped = false;
            if (anim != null)
            {
                anim.SetBool("idol", false);
                anim.SetBool("walk", false);
                anim.SetBool("run",   true);
            }
        }
        else if (nextState == EnemyState.Search)
        {// 索敵
            if (anim != null)
            {
                anim.SetBool("run",  false);
                anim.SetBool("idle", false);
                anim.SetBool("walk",  true);
            }
            if (markQuestion != null) markQuestion.SetActive(true);
            StartCoroutine(HideMark(markQuestion, 1.5f));
            searchTimer = searchTime;
            WanderAround();
        }
        else if (nextState == EnemyState.Return)
        {// 帰還
            if (anim != null)
            {
                anim.SetBool("walk", false);
                anim.SetBool("run", true);
            }
            agent.SetDestination(startPosition);
        }
    }

    private void WanderAround()
    {
        if (!agent.enabled) return;
        Vector3 randomPos = transform.position + Random.insideUnitSphere * searchRadius;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomPos, out hit, searchRadius, 1))
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
        if (attackTimer <= 0f)
        {
            AttackEnemy();
            attackTimer = attackInterval;
        }
    }

    //[SerializeField, Range(0.0f, 1.0f)] private float animStartPer = 0.2f;
    [SerializeField, Range(0.0f, 1.0f)] private float damageDelay = 0.3f;
    private void AttackEnemy()
    {
        if (anim != null)
        {
            anim.SetTrigger("beam");
            //anim.Play("Attack_v1", 0, animStartPer);
        }

        StartCoroutine(DelayDamageCoroutine());
        StartCoroutine(WaitAttackAnimation());
    }

    private IEnumerator DelayDamageCoroutine()
    {
        // 指定した秒数だけ、処理を一時停止する
        yield return new WaitForSeconds(damageDelay);

        // 待っている間に敵が倒されたり、プレイヤーが消えたりしていないかチェック
        if (targetPlayer == null || currentState != EnemyState.Attack) yield break;

        // パンチが振り下ろされた瞬間にまだ射程内にいるか再確認する
        float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);
        if (distanceToPlayer <= attackRange + 0.5f) // 少しだけ判定に猶予を持たせる
        {
            Controller playerControl = targetPlayer.GetComponent<Controller>();
            if (playerControl != null)
            {
                playerControl.hp -= attackDamage;
                Debug.Log("ぬ");
            }
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

        // ダメージを受けたときのエフェクトを再生
        if (enemyHitEffect != null) Instantiate(enemyHitEffect, transform.position, Quaternion.identity);

        if (currentHp <= 0) Die();
    }

    private void Die()
    {
        Destroy(gameObject);
    }
}