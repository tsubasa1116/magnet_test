using UnityEngine;

public class WaveColloder : MonoBehaviour
{
    [Header("衝撃波")]
    [Tooltip("どこまで判定が広がるか（半径）")]
    public float maxRadius = 8.0f;
    [Tooltip("最大半径に到達するまでの時間（秒）")]
    public float expandDuration = 0.6f;
    [Tooltip("最大まで広がった後判定を消すまでの時間（秒）")]
    public float destroy = 0.3f;

    [Header("ジャンプ回避の設定")]
    [Tooltip("衝撃波の高さ")]
    public float waveHeight = 1.0f;
    [Tooltip("衝撃波の厚さ（幅）")]
    public float waveThickness = 1.5f;

    public int damage = 5;

    private float currentRadius = 0.0f;
    private float timer = 0.0f;
    private bool hasHit = false; // 多段ヒットを防ぐためのフラグ

    void Update()
    {
        timer += Time.deltaTime;
        float progress = timer / expandDuration;

        // 現在の衝撃波の先端の半径を計算
        currentRadius = Mathf.Lerp(0.0f, maxRadius, progress);

        if (timer >= expandDuration + destroy)
        {
            Destroy(gameObject);
            return; // これ以上処理しない
        }

        if (hasHit) return;

        // 周囲のコライダーを拾う
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, currentRadius);
        foreach (var hit in hitColliders)
        {
            var player = hit.GetComponent<PlayerHealth>();
            if (player != null)
            {
                // 高さのチェック（プレイヤーのY座標 - 波紋の発生元のY座標）
                float playerHeight = hit.transform.position.y - transform.position.y;
                if (playerHeight > waveHeight)
                {
                    // 波紋より高い位置にいたら
                    continue;
                }

                // 水平方向の距離チェック（高さを無視して、XとZだけで距離を測る）
                Vector3 flatPlayerPos = new Vector3(hit.transform.position.x, 0, hit.transform.position.z);
                Vector3 flatCenterPos = new Vector3(transform.position.x, 0, transform.position.z);
                float distance = Vector3.Distance(flatPlayerPos, flatCenterPos);

                // ドーナツ状の輪っかの中にいるかチェック（プレイヤーが波の 先端---後端 の間にいるか）
                if (distance <= currentRadius && distance >= currentRadius - waveThickness)
                {
                    player.TakeDamage(damage);
                    hasHit = true;
                    break;
                }
            }
        }
    }

    void OnDrawGizmos()
    {
        // 緑色：衝撃波が広がる輪っか
        Gizmos.color = Color.green;
        DrawWireCircle(transform.position, currentRadius); // 先端
        float innerRadius = Mathf.Max(0, currentRadius - waveThickness);
        DrawWireCircle(transform.position, innerRadius);   // 後端

        // 赤色：衝撃波の高さ
        Gizmos.color = Color.red;
        Vector3 heightPos = transform.position + Vector3.up * waveHeight;
        DrawWireCircle(heightPos, currentRadius);
    }

    // 描画補助
    private void DrawWireCircle(Vector3 center, float radius)
    {
        if (radius <= 0.0f) return;
        int segments = 36;
        float angle = 0.0f;
        Vector3 lastPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);

        for (int i = 1; i <= segments; i++)
        {
            angle += (2.0f * Mathf.PI) / segments;
            Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(lastPoint, nextPoint);
            lastPoint = nextPoint;
        }
    }
}