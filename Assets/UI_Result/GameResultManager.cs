using UnityEngine;

public static class GameResultManager
{
    // インゲームからこの変数に値を代入してシーンを切り替える
    public static int DestroyCount { get; set; } = 0;
    public static int ClearTimeSeconds { get; set; } = 0;

    public static void SetResultData(int destroyCount, int clearTimeSeconds)
    {
        DestroyCount = destroyCount;
        ClearTimeSeconds = clearTimeSeconds;
    }

    public static int CalculateScoreRank()
    {
        int basePoint = (DestroyCount * 100) - ClearTimeSeconds;

        if (basePoint >= 1000) return 3; // Sランク
        if (basePoint >= 500) return 2; // Aランク
        if (basePoint >= 0) return 1; // Bランク
        return 0;                        // Cランク
    }
}