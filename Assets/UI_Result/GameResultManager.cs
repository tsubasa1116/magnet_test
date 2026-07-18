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

	public static int CalculateScore()
	{
		return 5000 - (ClearTimeSeconds * 10) + (DestroyCount * 50);
	}

	public static int CalculateScoreRank()
	{
		int score = CalculateScore();

		if (score >= 3700) return 3; // S
		if (score >= 3200) return 2; // A
		if (score >= 2600) return 1; // B
		return 0;                    // C
	}
}