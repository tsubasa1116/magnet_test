using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))] // 磁力などの物理演算で制御するために必要
public class enemy_bomb : MonoBehaviour, IMagnetEnemy
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
    [SerializeField] private int attackDamage = 10;

    [Header("浮遊")]
    [SerializeField] private float hoverHeight = 2.0f;  // 地面からの基本の高さ
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

    [Header("磁力で掴まれた時(持ち続けると手元で爆発)")]
    [Tooltip("手元に届いてから爆発するまでの秒数。この間に投げないとプレイヤーが大ダメージを受ける")]
    [SerializeField] private float heldFuseTime = 4.0f;
    [Tooltip("手元で爆発した時にプレイヤーが受けるダメージ")]
    [SerializeField] private int heldExplosionDamage = 30;
    [Tooltip("点滅(通常色⇔白)の間隔(秒)。手元に届いた直後はこの間隔")]
    [SerializeField] private float blinkIntervalStart = 0.4f;
    [Tooltip("点滅(通常色⇔白)の間隔(秒)。爆発直前はこの間隔まで速くなる")]
    [SerializeField] private float blinkIntervalEnd = 0.04f;
    [Tooltip("点滅で白く光る時のマテリアル。未指定なら真っ白な無発光マテリアルを自動で作る")]
    [SerializeField] private Material flashMaterial;
    [Tooltip("吹っ飛ばされてから何にも当たらなかった時に自爆するまでの秒数")]
    [SerializeField] private float thrownFuseTime = 3.0f;
    [Tooltip("そっと離された直後、プレイヤーに触れても爆発しない猶予(秒)")]
    [SerializeField] private float releaseGraceTime = 1.0f;

    private float currentHp;
    private NavMeshAgent agent;
    private Rigidbody rb; // 物理演算用

    private Vector3 startPosition;

    private EnemyState currentState = EnemyState.Wait;
    private float searchTimer; // 探索の残り時間を計るタイマー
    private float noticeTimer;

    private bool isMagnetized = false; // 磁力の影響(吹っ飛んでいる最中など)を受けているかどうか
    private bool hasFoundPlayer = false;
    private float timeOffset;          // 個体ごとにフワフワのタイミングをずらすための乱数

    private Animator anim;

    // ボスに召喚された敵は索敵範囲に関係なく、最初からプレイヤーを狙い続ける
    private bool alwaysAggro = false;

    private bool isDead = false; // 同じフレームに複数回当たっても二重に倒れないように

    // 磁力での状態
    private bool isHeld = false;           // プレイヤーに掴まれている
    private bool isAttached = false;       // 手元に届いている(ここから導火線が減っていく)
    private bool isThrownByPlayer = false; // 吹っ飛ばされている(何かに当たると爆発)
    private float fuseTimer;
    private float blinkTimer;
    private bool isFlashing = false;       // 点滅で白くなっている最中か
    private float ignorePlayerUntil;       // そっと離された直後はプレイヤーに触れても爆発しない
    private Renderer[] blinkRenderers;     // 点滅させる見た目(元々表示されているメッシュだけ)
    private Material[][] normalMaterials;  // 点滅用: 通常のマテリアル
    private Material[][] flashMaterials;   // 点滅用: 白のマテリアル

    private static Material defaultFlashMaterial; // flashMaterial未指定時に全爆弾で共有する白

    public void SetTarget(Transform player)
    {
        targetPlayer = player;
    }

    public void OnSummoned(Transform player)
    {
        targetPlayer = player;
        alwaysAggro = true;
    }

    private bool CanSeePlayer(float distanceToPlayer) => alwaysAggro || distanceToPlayer <= found;
    private bool LostPlayer(float distanceToPlayer) => !alwaysAggro && distanceToPlayer > found + 5.0f;
    void Start()
    {
        currentHp = maxHp;
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();

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

        // 点滅対象: 表示中のメッシュだけ(非表示の当たり判定用メッシュや！？マークは触らない)
        var renderers = new List<Renderer>();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r.enabled && (r is MeshRenderer || r is SkinnedMeshRenderer)) renderers.Add(r);
        }
        blinkRenderers = renderers.ToArray();

        // 点滅は「通常色⇔白」なので、白に差し替えるマテリアルを用意しておく
        // (爆弾のシェーダーグラフには色のプロパティが無いため、色替えではなく差し替えで白くする)
        Material white = flashMaterial != null ? flashMaterial : GetDefaultFlashMaterial();
        normalMaterials = new Material[blinkRenderers.Length][];
        flashMaterials = new Material[blinkRenderers.Length][];
        for (int i = 0; i < blinkRenderers.Length; i++)
        {
            normalMaterials[i] = blinkRenderers[i].sharedMaterials;
            flashMaterials[i] = new Material[normalMaterials[i].Length];
            for (int j = 0; j < flashMaterials[i].Length; j++) flashMaterials[i][j] = white;
        }

        anim.SetBool("Idol", true);
    }

    private static Material GetDefaultFlashMaterial()
    {
        if (defaultFlashMaterial == null)
        {
            // Sprites/Default は常にビルドに含まれる無発光シェーダー(ロックオンマーカーと同じ)
            defaultFlashMaterial = new Material(Shader.Find("Sprites/Default")) { color = Color.white };
        }
        return defaultFlashMaterial;
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
        StopAllCoroutines();

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            ChangeState(EnemyState.Wait);
        }
    }

    void FixedUpdate()
    {
        MagneticInteraction();
    }

    void Update()
    {
        // 掴まれている間: 手元に届いたら導火線が減り、点滅がだんだん速くなる。尽きたら手元で爆発
        if (isHeld)
        {
            if (isAttached) UpdateHeldFuse();
            return;
        }

        // 吹っ飛ばされている間はAIを止める(何かに当たると爆発。当たらなくても時間で自爆)
        if (isThrownByPlayer)
        {
            fuseTimer -= Time.deltaTime;
            if (fuseTimer <= 0f) Explode(0);
            return;
        }

        if (targetPlayer == null) return;

        PlayerHealth health = targetPlayer.GetComponent<PlayerHealth>();
        if (health != null && health.IsDead) return;

        // NavMeshAgentが有効な間のみふわふわ浮かせる
        if (agent.enabled)
            if(isHover) agent.baseOffset = hoverHeight + Mathf.Sin((Time.time + timeOffset) * hoverSpeed) * hoverRange;

        // 磁力で飛ばされている間はAIをストップする
        if (isMagnetized) return;

        float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);

        switch (currentState)
        {
            case EnemyState.Wait:
                if (CanSeePlayer(distanceToPlayer)) ChangeState(EnemyState.Notice);
                break;

            case EnemyState.Notice:
                noticeTimer -= Time.deltaTime;
                isHover = false; // 発見して驚いている間はフワフワを止める
                if (noticeTimer <= 0) ChangeState(EnemyState.Attack); // Noticeの後は直接Attack（距離調整）へ
                break;

            case EnemyState.Attack:
                if (LostPlayer(distanceToPlayer))
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
                        transform.rotation = lookRotation * Quaternion.Euler(0, 0, spinAngle);

                        // 3次元での直線追尾（体当たり）
                        transform.position = Vector3.MoveTowards(transform.position, targetCenterPos, attackSpeed * Time.deltaTime);

                        anim.SetBool("Idol", false);
                        anim.SetBool("Move", true);
                    }

                    // プレイヤーに接近した時の判定
                    if (Vector3.Distance(transform.position, targetCenterPos) <= 1.0f)
                    {
                        Attack();
                    }
                }
                break;

            case EnemyState.Search:
                if (CanSeePlayer(distanceToPlayer)) ChangeState(EnemyState.Notice);
                else
                {
                    searchTimer -= Time.deltaTime;
                    if (searchTimer <= 0) ChangeState(EnemyState.Return);
                    else if (agent.remainingDistance < 0.5f)
                    {
                        WanderAround();
                    }
                }
                break;

            case EnemyState.Return:
                if (CanSeePlayer(distanceToPlayer)) ChangeState(EnemyState.Notice);
                else if (agent.remainingDistance < 0.5f)
                {
                    ChangeState(EnemyState.Wait);
                    anim.SetBool("Idol", true);
                    anim.SetBool("Move", false);
                }
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
            // NavMeshの外で有効化した時は止める・動かすができない(エラーになる)ので乗っている時だけ
            if (agent.isOnNavMesh) agent.isStopped = false;
            isHover = true;  // フワフワを再開
        }

        if (!agent.enabled) return; // 磁力で飛んでいる時はエラー防止

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
                CombatStateManager.NotifyEnter(gameObject);
            }

            if (markExclamation != null)
                markExclamation.SetActive(true);

            StartCoroutine(HideMark(markExclamation, noticeTime));
        }
        else if (nextState == EnemyState.Search)
        {
            if (hasFoundPlayer)
            {
                hasFoundPlayer = false;
                CombatStateManager.NotifyExit(gameObject);
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

    // 体当たり: 爆発してプレイヤーにダメージ
    private void Attack()
    {
        Explode(attackDamage);
    }

    // 爆発してやられる。playerDamage > 0 ならプレイヤーにダメージを与える
    private void Explode(int playerDamage)
    {
        if (isDead) return;

        if (playerDamage > 0)
        {
            PlayerHealth playerHealth = targetPlayer != null
                ? targetPlayer.GetComponent<PlayerHealth>()
                : FindAnyObjectByType<PlayerHealth>();
            if (playerHealth != null) playerHealth.TakeDamage(playerDamage, transform.position);
        }

        Die(); // 爆発エフェクトはDieで出す
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isDead || isHeld) return;

        bool hitPlayer = collision.gameObject.CompareTag("Player");

        // 吹っ飛ばされている最中: プレイヤー以外の何かに当たったら爆発
        if (isThrownByPlayer)
        {
            if (!hitPlayer) TakeDamage(currentHp);
            return;
        }

        // プレイヤーに当たったら爆発してダメージ(そっと離された直後は除く)
        if (hitPlayer)
        {
            if (Time.time >= ignorePlayerUntil) Attack();
            return;
        }

        // 投げられた物が当たったら爆発
        ThrowableObject throwable = collision.gameObject.GetComponent<ThrowableObject>();
        if (throwable != null && throwable.IsThrown)
        {
            // 一度だけダメージを与える
            throwable.ResetThrown();
            TakeDamage(currentHp);
        }
    }

    // ==========================================
    // 磁力システムからの通知受け取り口
    // ==========================================

    // 爆弾は敵用の持ち方(手の前で向き合わせる)ではなく、物体と同じく手元にそのまま持つ
    public bool HoldAsEnemy => false;

    public void OnMagnetGrabbed()
    {
        isHeld = true;
        isAttached = false;
        isThrownByPlayer = false;

        StopAllCoroutines();
        if (agent.enabled) agent.enabled = false;
        isHover = false;

        // 引き寄せ中・保持中はアニメを止める(アニメが位置を書き戻して手元に来ないのを防ぐ)
        if (anim != null) anim.enabled = false;
    }

    public void OnMagnetAttached()
    {
        // 手元に届いたら導火線に点火。持ち続けると手元で爆発する
        isAttached = true;
        fuseTimer = heldFuseTime;
        blinkTimer = blinkIntervalStart;
    }

    public void OnMagnetReleased()
    {
        // そっと離された: 導火線を止めて、その場から再びプレイヤーを狙う
        EndHeld();
        ignorePlayerUntil = Time.time + releaseGraceTime;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        ChangeState(EnemyState.Notice);
    }

    public void OnMagnetRepelled()
    {
        // 吹っ飛ばされた: 何かに当たったら爆発する
        EndHeld();
        isThrownByPlayer = true;
        fuseTimer = thrownFuseTime;
    }

    // 掴まれた状態を終える(点滅を止めて通常の見た目・アニメに戻す)
    private void EndHeld()
    {
        isHeld = false;
        isAttached = false;
        SetFlash(false);
        if (anim != null) anim.enabled = true;
    }

    // 掴まれている間の導火線。残りが少ないほど速く点滅し、尽きたら手元で爆発して大ダメージ
    private void UpdateHeldFuse()
    {
        fuseTimer -= Time.deltaTime;
        if (fuseTimer <= 0f)
        {
            Explode(heldExplosionDamage);
            return;
        }

        float remaining01 = Mathf.Clamp01(fuseTimer / heldFuseTime);
        blinkTimer -= Time.deltaTime;
        if (blinkTimer <= 0f)
        {
            blinkTimer = Mathf.Lerp(blinkIntervalEnd, blinkIntervalStart, remaining01);
            SetFlash(!isFlashing);
        }
    }

    // 点滅: true で白、false で通常の見た目
    private void SetFlash(bool white)
    {
        isFlashing = white;
        if (blinkRenderers == null) return;
        for (int i = 0; i < blinkRenderers.Length; i++)
        {
            if (blinkRenderers[i] != null)
                blinkRenderers[i].sharedMaterials = white ? flashMaterials[i] : normalMaterials[i];
        }
    }

    public void TakeDamage(float damageAmount)
    {
        if (isDead) return;

        currentHp -= damageAmount;

        // ダメージを受けたときのエフェクトを再生
        //if (enemyHitEffect != null) Instantiate(enemyHitEffect, transform.position, Quaternion.identity);

        if (currentHp <= 0)
        {
            HitStop.Play(0.12f); // 倒した手応えのヒットストップ(自爆時は入らない)
            Die();
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        if (hasFoundPlayer)
        {
            hasFoundPlayer = false;
        }

        CombatStateManager.NotifyExit(gameObject);
        if (explosionEffect != null) Instantiate(explosionEffect, transform.position, Quaternion.identity);
        //audioSource.PlayOneShot(explosionSE); 
        Destroy(gameObject/*, explosionSE.length*/);
        GameManager.Instance.AddKill();
    }
}