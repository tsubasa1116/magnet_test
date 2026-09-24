using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ThrowableObject : MonoBehaviour
{
    [Header("ダメージ")]
    [SerializeField] private float damage = 100.0f;

    [Header("発射中か")]
    public bool IsThrown = false;

    [Header("発射状態の解除")]
    [Tooltip("発射後、この速さより遅くなったら発射状態を解除する(止まった物に触れただけで敵が倒れないように)")]
    [SerializeField] private float minThrownSpeed = 2.0f;
    [Tooltip("発射直後は加速しきっていないので、この秒数は速さで解除しない")]
    [SerializeField] private float throwGraceTime = 0.2f;

    private Rigidbody rb;
    private float thrownTime;
    private Vector3 lastPosition; // 前の物理ステップの位置(バリアの近くを通ったかの判定用)
    private float hitRadius;      // 自分の大きさ(当たり判定の半径の目安)

    /// <summary>
    /// このオブジェクトのダメージ量
    /// </summary>
    public float Damage => damage;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (!IsThrown) return;

        // 再び掴まれた(キネマティック)・勢いが無くなった物は発射扱いをやめる
        if (rb.isKinematic)
        {
            ResetThrown();
            return;
        }

        // ボスのバリア(大きめの当たり判定)を通ったら当たった扱い(周りの胴体に先に当たって届かない・すり抜けるのを防ぐ)
        Vector3 position = rb.position;
        for (int i = BarrierCollider.Active.Count - 1; i >= 0 && IsThrown; i--)
            BarrierCollider.Active[i].CheckPass(gameObject, lastPosition, position, hitRadius);
        lastPosition = position;
        if (!IsThrown) return;

        if (Time.time - thrownTime >= throwGraceTime
            && rb.linearVelocity.sqrMagnitude < minThrownSpeed * minThrownSpeed)
        {
            ResetThrown();
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!IsThrown) return;

        // ボスに当たった: バリアなら割れる、体・腕ならHPへダメージ(バリア越しは半減・コアむき出しは大ダメージ)
        enemy_Boss boss = collision.collider.GetComponentInParent<enemy_Boss>();
        if (boss != null) boss.OnThrownObjectHit(gameObject, collision.collider);
    }

    /// <summary>
    /// 発射状態にする
    /// </summary>
    public void Throw()
    {
        IsThrown = true;
        thrownTime = Time.time;
        lastPosition = rb.position;
        hitRadius = MeasureRadius();
    }

    // 自分のコライダー全体の大きさから、当たり判定の半径の目安を出す(大きすぎる物は1mで打ち止め)
    private float MeasureRadius()
    {
        Bounds b = default;
        bool found = false;
        foreach (Collider c in GetComponentsInChildren<Collider>())
        {
            if (!c.enabled || c.isTrigger) continue;
            if (!found) { b = c.bounds; found = true; }
            else b.Encapsulate(c.bounds);
        }
        if (!found) return 0.3f;
        Vector3 e = b.extents;
        return Mathf.Min(Mathf.Max(e.x, e.y, e.z), 1.0f);
    }

    /// <summary>
    /// 発射状態を解除する
    /// </summary>
    public void ResetThrown()
    {
        IsThrown = false;
    }
}
