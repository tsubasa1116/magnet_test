using System.Collections.Generic;
using UnityEngine;

public class CombatStateManager : MonoBehaviour
{
    public static CombatStateManager Instance;

    // 現在プレイヤーを追跡・交戦中の敵の集合
    private HashSet<GameObject> engagedEnemies = new HashSet<GameObject>();

    private void Awake()
    {
        Instance = this;
    }

    // 敵が接敵(プレイヤーを発見・追跡開始)したときに呼ぶ
    public void EnterCombat(GameObject enemy)
    {
        bool wasEmpty = engagedEnemies.Count == 0;
        engagedEnemies.Add(enemy);

        if (wasEmpty)
        {
            // 誰もいなかった状態から初めて交戦開始 → バトルBGMへ
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayBGMWithFade("Enemy", 1.0f, true, false);
        }
    }

    // 敵が死亡した、または追跡を諦めた(見失った)ときに呼ぶ
    public void ExitCombat(GameObject enemy)
    {
        engagedEnemies.Remove(enemy);

        if (engagedEnemies.Count == 0)
        {
            // 全ての敵との交戦が終わった時だけフィールドBGMに戻す
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayBGMWithFade("Field", 1.5f, true, true);
        }
    }
}