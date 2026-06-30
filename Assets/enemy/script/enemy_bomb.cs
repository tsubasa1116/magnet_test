using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))] // 磁力などの物理演算で制御するために必要
public class enemy_bomb : MonoBehaviour
{
    private enum EnemyState
    {
        Wait,   // 待機（初期位置にいる）
        Notice, // 発見（立ち止まって驚いている）
        Chase,  // 追跡（今回は原則使用せず、Attack内で距離調整を行います）
        Attack, // 攻撃・距離保持
        Search, // 探索（見失って周囲を探している）
        Return  // 帰還（初期位置に戻っている）
    }

    [Header("パラメータ")]
    [SerializeField] private float maxHp = 100.0f;
    [SerializeField] private float found = 10.0f;
    [SerializeField] private float attackRange  = 0.01f;
    [SerializeField] private float searchTime   = 3.0f;   // プレイヤーを見失った後に探す時間
    [SerializeField] private float searchRadius = 5.0f; // 探索する範囲
    [SerializeField] private float noticeTime   = 1.0f;
    [SerializeField] private float spinSpeed    = 360.0f; // 回転速度（度/秒）
    [SerializeField] private float attackSpeed  = 3.0f;
    [SerializeField] private int attackDamage = 1;

    [Header("浮遊")]
    [SerializeField] private float hoverHeight = 2.0f;     // 地面からの基本の高さ
    [SerializeField] private float hoverRange  = 0.5f;  // ふわふわの揺れ幅
    [SerializeField] private float hoverSpeed  = 2.0f;  // ふわふわの揺れる速度
    [SerializeField] private bool  isHover     = true;

    [Header("磁力")] // 磁力(引力・斥力)の設定
    [SerializeField] private float magnetRadius = 8.0f;  // 磁力などを感知する距離
    [SerializeField] private float magnetForce  = 50.0f;  // 引き寄せる・反発する力

    [Header("参照")]
    [SerializeField] private Transform  targetPlayer;
    [SerializeField] private GameObject markExclamation; // ！マーク
    [SerializeField] private GameObject markQuestion;    // ？マーク

    [Header("エフェクト")]
    [SerializeField] private GameObject explosionEffect;
    [SerializeField] private GameObject enemyHitEffect;

    private float currentHp;
    private NavMeshAgent agent;
    private Rigidbody rb; // 物理演算用

    private Vector3 startPosition;

    private EnemyState currentState = EnemyState.Wait;
    private float searchTimer; // 探索の残り時間を計るタイマー
    private float noticeTimer;

    private bool isMagnetized = false; // 磁力の影響(吹っ飛んでいる最中など)を受けているかどうか
    private float timeOffset;          // 個体ごとにフワフワのタイミングをずらすための乱数

    void Start()
    {
        currentHp = maxHp;
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();

        // 通常時はNavMeshAgentで移動するため物理演算(Rigidbody)はオフにしておく
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false; // 飛行型なので重力の影響をなくす
        }

        // 初期高さをセット
        if (agent != null) agent.baseOffset = hoverHeight;

        startPosition = transform.position;
        timeOffset = Random.Range(0.0f, 100.0f); // 複数の敵がいても動きが揃わないようにする

        if (markExclamation != null) markExclamation.SetActive(false);
        if (markQuestion != null) markQuestion.SetActive(false);
    }

    void FixedUpdate()
    {
        MagneticInteraction();
    }

    void Update()
    {
        // NavMeshAgentが有効な間のみふわふわ浮かせる
        if (agent.enabled)
        {
            if(isHover) agent.baseOffset = hoverHeight + Mathf.Sin((Time.time + timeOffset) * hoverSpeed) * hoverRange;
        }

        // 磁力で飛ばされている間はAIをストップする
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
                isHover = false; // 発見して驚いている間はフワフワを止める
                if (noticeTimer <= 0) ChangeState(EnemyState.Attack); // Noticeの後は直接Attack（距離調整）へ
                break;

            case EnemyState.Attack:
                if (distanceToPlayer > found + 5.0f)
                {
                    ChangeState(EnemyState.Search);
                }
                else
                {
                    Vector3 targetCenterPos = targetPlayer.position + Vector3.up * 0.8f;
                    Vector3 dirToPlayer = targetCenterPos - transform.position;

                    if (dirToPlayer.sqrMagnitude > 0.01f)
                    {
                        // 回転処理
                        Quaternion lookRotation = Quaternion.LookRotation(dirToPlayer.normalized);
                        float spinAngle = (Time.time * spinSpeed) % 360.0f;
                        transform.rotation = lookRotation * Quaternion.Euler(0, 180, spinAngle);

                        // 3次元での直線追尾（体当たり）
                        transform.position = Vector3.MoveTowards(transform.position, targetCenterPos, attackSpeed * Time.deltaTime);
                    }

                    //// プレイヤーに接近した時の判定
                    //if (Vector3.Distance(transform.position, targetCenterPos) <= 1.0f)
                    //{
                    //    Attack();
                    //}
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

    // 磁力をN極・S極として感知して力を受ける処理
    private void MagneticInteraction()
    {
        // 自身のタグを確認
        bool isMyN = gameObject.CompareTag("N_Pole");
        bool isMyS = gameObject.CompareTag("S_Pole");

        if (!isMyN && !isMyS) return; // 磁石に対応していなければ無視

        Collider[] colliders = Physics.OverlapSphere(transform.position, magnetRadius);
        bool feelingMagnet = false;
        Vector3 totalForce = Vector3.zero;

        // くっついている対象を記録
        Transform attachedTarget = null;
        float minDistance = float.MaxValue;

        foreach (Collider col in colliders)
        {
            if (col.gameObject == gameObject) continue; // 自身は除外

            // 相手が enemy (敵) なら干渉しない（お好みで変更可能）
            if (col.GetComponent<enemy>() != null || col.GetComponent<enemy_Sky>() != null) continue;

            bool isOtherN = col.CompareTag("N_Pole");
            bool isOtherS = col.CompareTag("S_Pole");

            if (isOtherN || isOtherS)
            {
                // 相手への方向と距離
                Vector3 dirToOther = col.transform.position - transform.position;
                float distance = dirToOther.magnitude;

                // 距離が近すぎる場合は0を防止
                float safeDistance = distance < 0.5f ? 0.5f : distance;

                // 距離が近いほど強く引っ張られるようにする
                float force = magnetForce * (1.0f + (magnetRadius - safeDistance) / magnetRadius);

                // 違う極（引き寄せる・くっつく）
                if ((isMyN && isOtherS) || (isMyS && isOtherN))
                {
                    feelingMagnet = true;

                    // 十分に近ければ「まとわりつく」状態にするための判定
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
                        // 離れていれば通常通り引っ張られる（エージェントのみに加算）
                        totalForce += dirToOther.normalized * force;
                    }
                }
                // 同じ極（反発する）
                else if ((isMyN && isOtherN) || (isMyS && isOtherS))
                {
                    // エージェントへの反発力のみ加算
                    totalForce -= dirToOther.normalized * force;
                    feelingMagnet = true;
                }
            }
        }

        // 力を受けている際の切り替え (NavMeshAgentとRigidbodyの切り替え)
        if (feelingMagnet)
        {
            if (agent.enabled)
            {
                agent.enabled = false;   // 移動AIを一時停止
                rb.isKinematic = false;  // 物理演算をオン
                isMagnetized = true;

                // 吹っ飛んでいきすぎないように、空気抵抗を一時的に追加
                rb.linearDamping = 0.5f;
            }

            // くっつく（まとわりつく）処理
            if (attachedTarget != null)
            {
                // 磁力で親などに引っ張られる際には質量(mass)を一時的に極小にして極端な反発を防ぐ
                rb.mass = 0.01f;

                // 目標の中心に向けて常に強力な引力で引き寄せる（くっつく）
                Vector3 stickDir = attachedTarget.position - transform.position;

                // オーバーシュート（突き抜け）を防ぐため、速度を制限しつつ引き寄せる
                rb.linearVelocity = stickDir.normalized * 5f;

                // 強制的にスナップ（重力などを無視して吸い付く）させる。VelocityChangeを使って継続的に適用。
                rb.AddForce(stickDir.normalized * (magnetForce * 5f), ForceMode.Acceleration);
            }
            else
            {
                // 磁力でくっついていない場合は元の質量に戻す（デフォルトが1の場合）
                rb.mass = 1.0f;

                if (totalForce.magnitude > 0.1f)
                {
                    // ForceMode.VelocityChange (質量無視で即座に変更) を使ってグッと引き寄せる
                    rb.AddForce(totalForce * Time.fixedDeltaTime, ForceMode.VelocityChange);
                }
            }
        }
        else if (isMagnetized)
        {
            // 磁力の影響範囲から外れ、速度が落ち着いたら通常のAI(NavMesh)に戻す
            if (rb.linearVelocity.magnitude < 0.5f)
            {
                rb.mass = 1.0f; // 質量を元の値に戻す
                rb.isKinematic = true;
                isMagnetized = false;

                // NavMesh(歩ける床)の上にちゃんと着地できているか確認してから
                // 飛行型は少し上空にいてもNavMeshを探せるよう検索半径を広めに取る(2.0f -> +hoverHeight)
                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position, out hit, 2.0f + hoverHeight, NavMesh.AllAreas))
                {
                    transform.position = hit.position; // x, z が正しい位置に戻り、y だけ NavMesh の高さになるが、直後の Update で baseOffset が適用されて再び浮く
                    agent.enabled = true;
                }
            }
        }
    }

    // ステート切り替え処理
    private void ChangeState(EnemyState nextState)
    {
        currentState = nextState;
        if (isMagnetized) return;

        if (nextState == EnemyState.Attack)
        {
            // 突撃中はNavMeshの「高さ固定」の呪縛を解くため、絶対にオフにする
            agent.enabled = false;
            isHover = false;

            // 物理演算(Rigidbody)がトランスフォームの書き換えの邪魔をしないように固定する
            if (rb != null) rb.isKinematic = true;
        }
        else
        {
            // Attack 以外のステートではNavMeshAgentを復帰させる
            if (!agent.enabled)
            {
                // エージェントを再起動する前に、現在の位置から一番近いNavMeshの床にスナップさせる（バグ防止）
                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position, out hit, 5.0f, NavMesh.AllAreas))
                {
                    transform.position = hit.position;
                }
                agent.enabled = true;
            }
            agent.isStopped = false;
            isHover = true;  // フワフワを再開
        }

        if (!agent.enabled) return; // 磁力で飛んでいる時はエラー防止

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

    // 探索中にランダムな位置を目的地に設定する処理
    private void WanderAround()
    {
        if (!agent.enabled) return;
        Vector3 randomPos = transform.position + Random.insideUnitSphere * searchRadius;
        NavMeshHit hit;

        // 飛行型は上下の判定範囲が広い可能性があるので、少し広めに NavMesh を探す
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

    private void Attack() 
    {
        var playerController = targetPlayer.GetComponent<Controller>();
        if (playerController != null)
        {
            playerController.TakeDamage(attackDamage);
        }
        Die();
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

    private void OnCollisionEnter(Collision collision)
    {
        // 突撃でダメージ判定
        if (currentState != EnemyState.Attack) return;

        // ぶつかった相手がプレイヤーなら攻撃して自爆
        var playerController = collision.gameObject.GetComponent<Controller>();
        if (playerController != null)
        {
            Attack();
            if (explosionEffect != null)    Instantiate(explosionEffect, transform.position, Quaternion.identity);
        }
    }
}