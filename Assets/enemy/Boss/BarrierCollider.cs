using UnityEngine;
using System.Collections.Generic;

// ボスのバリアの当たり判定。プレイヤーが投げた物が当たったら enemy_Boss.OnBarrierHit を呼ぶ
// (バリアは1回当てると割れて、しばらくコアがむき出しになる。ダメージの仕様は enemy_Boss 側)。
// 当たり判定は見た目の球より大きめに取る: 投げた物の軌跡が大きめの球を通ったら当たった扱いにするので、
// 周りの胴体に先に当たって届かない・すり抜けるといった取りこぼしが起きにくい
public class BarrierCollider : MonoBehaviour
{
    // 有効なバリア一覧(飛んでいる物がバリアの近くを通ったかを ThrowableObject が毎物理ステップ調べる)
    public static readonly List<BarrierCollider> Active = new List<BarrierCollider>();

    [Header("参照設定")]
    public enemy_Boss bossScript;

    [Header("当たり判定(見た目より大きめ)")]
    [Tooltip("見た目の球の半径に対する当たり判定の倍率")]
    public float hitRadiusScale = 1.5f;
    [Tooltip("さらにこの距離(m)まで近づいたら当たった扱いにする")]
    public float proximityMargin = 0.5f;

    private SphereCollider sphere;

    private void Awake()
    {
        sphere = GetComponent<SphereCollider>();
    }

    private void OnEnable()
    {
        if (!Active.Contains(this)) Active.Add(this);
    }

    private void OnDisable()
    {
        Active.Remove(this);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (bossScript != null) bossScript.OnBarrierHit(collision.gameObject);
    }

    // 飛んでいる物がこの物理ステップで from → to と動いた。
    // その軌跡が大きめに取ったバリアの球を通ったら、当たった扱いにする
    public void CheckPass(GameObject thrown, Vector3 from, Vector3 to, float thrownRadius)
    {
        if (bossScript == null || sphere == null || !sphere.enabled) return;

        Vector3 center = transform.TransformPoint(sphere.center);
        Vector3 s = transform.lossyScale;
        float radius = sphere.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)) * hitRadiusScale;

        // 線分(from→to)上でバリア中心に一番近い点までの距離
        Vector3 seg = to - from;
        float t = seg.sqrMagnitude > 0.0001f
            ? Mathf.Clamp01(Vector3.Dot(center - from, seg) / seg.sqrMagnitude)
            : 0f;
        float dist = Vector3.Distance(center, from + seg * t);

        if (dist <= radius + thrownRadius + proximityMargin) bossScript.OnBarrierHit(thrown);
    }
}
