using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))] // ★追加：物理演算で吹き飛ばすために必要
public class enemy : MonoBehaviour
{
    private enum EnemyState
    {
        Wait,   // 待機（初期位置にいる）
        Notice, // 発見して驚いている（立ち止まっている）
        Chase,  // 追跡（見つけて追いかけている）
        Attack, // 攻撃
        Search, // 見失ってウロウロ探している
        Return  // 諦めて初期位置に帰っている
    }

    [Header("パラメータ")]
    [SerializeField] private float maxHp = 100.0f;
    [SerializeField] private float found = 10.0f;
    [SerializeField] private float attackRange = 2.0f;
    [SerializeField] private float searchTime = 3.0f;   // 見失った後ウロウロする時間
    [SerializeField] private float searchRadius = 5.0f; // ウロウロする範囲
    [SerializeField] private float noticeTime = 1.0f;

    [Header("磁力パラメータ")] // ★追加：磁石の力の設定
    [SerializeField] private float magnetRadius = 8.0f;  // ボムなどを感知する距離
    [SerializeField] private float magnetForce = 50.0f;  // 引き合う・反発する力

    [Header("参照")]
    [SerializeField] private Transform targetPlayer;
    [SerializeField] private GameObject markExclamation; // ！
    [SerializeField] private GameObject markQuestion;    // ？

    private float currentHp;
    private NavMeshAgent agent;
    private Rigidbody rb; // ★追加：物理演算用

    private Vector3 startPosition;

    private EnemyState currentState = EnemyState.Wait;
    private float searchTimer; // ウロウロの残り時間を計るタイマー
    private float noticeTimer;

    private bool isMagnetized = false; // ★追加：磁力で吹っ飛んでいる最中かどうか

    void Start()
    {
        currentHp = maxHp;
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>(); // ★追加

        // 普段はNavMeshAgentで動くので物理(Rigidbody)はオフにしておく
        if (rb != null) rb.isKinematic = true;

        startPosition = transform.position;

        if (markExclamation != null) markExclamation.SetActive(false);
        if (markQuestion != null) markQuestion.SetActive(false);
    }

    void FixedUpdate() // ★追加：物理的な力の判定はFixedUpdateで行う
    {
        MagneticInteraction();
    }

    void Update()
    {
        // ★追加：磁力で飛ばされている間はAIの思考（追跡など）をストップする
        if (isMagnetized) return;

        if (targetPlayer == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);

        // 今の状態によってやることを変える
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
                if (distanceToPlayer > attackRange) ChangeState(EnemyState.Chase);
                else
                {
                    agent.isStopped = true;
                    Attack();
                }
                break;

            case EnemyState.Search:
                if (distanceToPlayer <= found) ChangeState(EnemyState.Notice);
                else
                {
                    searchTimer -= Time.deltaTime;
                    if (searchTimer <= 0) ChangeState(EnemyState.Return);
                    else if (agent.remainingDistance < 0.5f) WanderAround();
                }
                break;

            case EnemyState.Return:
                if (distanceToPlayer <= found) ChangeState(EnemyState.Notice);
                else if (agent.remainingDistance < 0.5f) ChangeState(EnemyState.Wait);
                break;
        }
    }

    // ★追加：周囲のN極・S極を検知して力を加える処理
    private void MagneticInteraction()
    {
        // 自分自身のタグを確認
        bool isMyN = gameObject.CompareTag("N_Pole");
        bool isMyS = gameObject.CompareTag("S_Pole");

        if (!isMyN && !isMyS) return; // 自分が磁石化していなければ無視

        Collider[] colliders = Physics.OverlapSphere(transform.position, magnetRadius);
        bool feelingMagnet = false;
        Vector3 totalForce = Vector3.zero;

        // ★くっついている対象を記録
        Transform attachedTarget = null;
        float minDistance = float.MaxValue;

        foreach (Collider col in colliders)
        {
            if (col.gameObject == gameObject) continue; // 自分自身は除外

            // 相手が enemy (仲間) だったら干渉しない（無視する）
            if (col.GetComponent<enemy>() != null) continue;

            bool isOtherN = col.CompareTag("N_Pole");
            bool isOtherS = col.CompareTag("S_Pole");

            if (isOtherN || isOtherS)
            {
                // 自分から相手への方向と距離
                Vector3 dirToOther = col.transform.position - transform.position;
                float distance = dirToOther.magnitude;

                // 距離が近すぎる場合の0割防止
                float safeDistance = distance < 0.5f ? 0.5f : distance;

                // 距離が近くなるほど飛躍的に力が強くなるようにする
                float force = magnetForce * (1.0f + (magnetRadius - safeDistance) / magnetRadius);

                // 違う極（引き寄せる・くっつく）
                if ((isMyN && isOtherS) || (isMyS && isOtherN))
                {
                    feelingMagnet = true;

                    // 十分に近ければ「まとわりつく」処理にするための判定
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
                        // まだ遠い時は通常通り引っ張る（敵自身のみに向かう力を加える）
                        totalForce += dirToOther.normalized * force;
                    }
                }
                // 同じ極（反発し合う）
                else if ((isMyN && isOtherN) || (isMyS && isOtherS))
                {
                    // 敵自身への反発力のみ加える
                    totalForce -= dirToOther.normalized * force;
                    feelingMagnet = true;
                }
            }
        }

        // 磁力を受けている際の切り替え (NavMeshAgentとRigidbodyの切り替え)
        if (feelingMagnet)
        {
            if (agent.enabled)
            {
                agent.enabled = false;   // 移動AIを一時停止
                rb.isKinematic = false;  // 物理演算をオン
                isMagnetized = true;

                // 吸い寄せられやすくするために、空気抵抗を一時的に下げる
                rb.drag = 0.5f;
            }

            // 密着状態（まとわりつく）の処理
            if (attachedTarget != null)
            {
                // ★追加：爆弾などに密着した時には質量(mass)を一時的に極小にして相手を押す力をなくす
                rb.mass = 0.01f;

                // 相手の中心に向けて非常に強い力で引っ張り続ける（くっつく）
                Vector3 stickDir = attachedTarget.position - transform.position;

                // バウンド（反発による暴れ）を防ぐため、速度を制限しつつ強く引く
                rb.velocity = stickDir.normalized * 5f;

                // 力技でスリップ（摩擦などを無視して張り付く）させる。VelocityChangeを使って強制的に動かす。
                rb.AddForce(stickDir.normalized * (magnetForce * 5f), ForceMode.Acceleration);
            }
            else
            {
                // ★追加：密着していない時は元の質量に戻す（デフォルトが1の場合）
                rb.mass = 1.0f;

                if (totalForce.magnitude > 0.1f)
                {
                    // ForceMode.VelocityChange (質量無視で即座に加速) を使ってグンッと引き寄せる
                    rb.AddForce(totalForce * Time.fixedDeltaTime, ForceMode.VelocityChange);
                }
            }
        }
        else if (isMagnetized)
        {
            // 磁力の影響範囲から外れ、動きが落ち着いたらAI(NavMesh)に戻す
            if (rb.velocity.magnitude < 0.5f)
            {
                rb.mass = 1.0f; // ★質量を元に戻す
                rb.isKinematic = true;
                isMagnetized = false;

                // NavMesh（歩ける床）の上に着地できているか確認してから復帰
                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position, out hit, 2.0f, NavMesh.AllAreas))
                {
                    transform.position = hit.position;
                    agent.enabled = true;
                }
            }
        }
    }

    // モードを切り替える
    private void ChangeState(EnemyState nextState)
    {
        currentState = nextState;
        if (!agent.enabled) return; // 磁力で飛んでいる時はエラー防止

        agent.isStopped = false;

        if (nextState == EnemyState.Wait)
        {
            agent.isStopped = true;
        }
        else if (nextState == EnemyState.Notice)
        {
            agent.isStopped = true;
            noticeTimer = noticeTime;
            if (markExclamation != null) markExclamation.SetActive(true);
            StartCoroutine(HideMark(markExclamation, noticeTime));
        }
        else if (nextState == EnemyState.Chase)
        {
            agent.isStopped = false;
        }
        else if (nextState == EnemyState.Search)
        {
            if (markQuestion != null) markQuestion.SetActive(true);
            StartCoroutine(HideMark(markQuestion, 1.5f));
            searchTimer = searchTime;
            WanderAround();
        }
        else if (nextState == EnemyState.Return)
        {
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

    private void Attack() { /* 攻撃処理 */ }

    public void TakeDamage(float damageAmount)
    {
        currentHp -= damageAmount;
        if (currentHp <= 0) Die();
    }

    private void Die()
    {
        Destroy(gameObject);
    }
}