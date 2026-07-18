using UnityEngine;
using UnityEngine.AI;

// ビルドではシーンロード時にNavMeshAgentの生成がNavMeshSurfaceのロードより
// 先に走ることがあり、その場合「Failed to create agent because there is no valid NavMesh」
// のままエージェントがNavMeshに乗れず、敵が配置座標のまま地面に埋まって動かなくなる。
// (エディタは初期化順が違うため発生せず、ビルドでだけ起きる)
// Start時点ではNavMeshが確実にロード済みなので、乗れていなければ
// 最寄りのNavMesh上へWarpして自己修復する。
public static class NavMeshAgentBootstrap
{
    public static void EnsureOnNavMesh(NavMeshAgent agent, float searchRange = 3f)
    {
        if (agent == null || !agent.enabled || agent.isOnNavMesh) return;

        if (NavMesh.SamplePosition(agent.transform.position, out NavMeshHit hit, searchRange, NavMesh.AllAreas))
            agent.Warp(hit.position);
    }
}
