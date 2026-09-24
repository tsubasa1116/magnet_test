using UnityEngine;
using UnityEngine.AI;

// Projectからシーンに直接置いた敵を、ボスが召喚した敵と同じように動かすための共通処理。
// prefabはシーン内のプレイヤーを参照できないので(targetPlayerが空になる)、実行時にプレイヤーを探して補う
public static class EnemyTargetFinder
{
    // 狙う相手としてそのまま使えるか(非表示の古いプレイヤーや、HPを持たない親オブジェクトはダメ)
    private static bool IsValidPlayer(Transform target)
    {
        return target != null
            && target.gameObject.activeInHierarchy
            && target.GetComponent<PlayerHealth>() != null;
    }

    // targetPlayer が未設定・無効なら、シーン内の実際のプレイヤー(PlayerHealth)に差し替える
    public static Transform ResolvePlayer(Transform current)
    {
        if (IsValidPlayer(current)) return current;

        PlayerHealth health = Object.FindAnyObjectByType<PlayerHealth>();
        return health != null ? health.transform : current;
    }

    // プレイヤーの子の EffectPoint(レーザーの狙い位置)を探す。無ければ null
    public static Transform FindEffectPoint(Transform player)
    {
        return player != null ? player.Find("EffectPoint") : null;
    }

    // 置いた位置がNavMeshから少し浮いている・ずれているとNavMeshAgentが乗れず一歩も動けないので、
    // 近くのNavMesh上へ移す(ボスの召喚はNavMesh.SamplePositionで位置を決めてから生成している)
    public static bool TryPlaceOnNavMesh(NavMeshAgent agent, float searchRadius)
    {
        if (agent == null || !agent.enabled) return false;
        if (agent.isOnNavMesh) return true;

        if (!NavMesh.SamplePosition(agent.transform.position, out NavMeshHit hit, searchRadius, NavMesh.AllAreas))
            return false;

        return agent.Warp(hit.position);
    }
}
