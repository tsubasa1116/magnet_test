using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BombManager : MonoBehaviour
{
	// コピー元になる爆弾（プレハブ）を入れる箱
	public GameObject bombPrefab;

	// 爆弾を出現させる場所を決めるための目印
	public Transform spawnPoint;

	// 今、画面に出ている爆弾を覚えておくための変数
	private GameObject currentBomb;

	void Start()
	{
		// ゲームがスタートしたとき、まずは最初の1個目の爆弾を作るよ！
		SpawnBomb();
	}

	void Update()
	{
		// currentBomb が null（空っぽ）になっているかチェック！
		// ※爆弾が Destroy() されて消えると、ここは自動的に空っぽになるんだ。
		if (currentBomb == null)
		{
			// 空っぽになっていたら、新しい爆弾を作る！
			SpawnBomb();
		}
	}

	// 爆弾を新しく作るための処理（まとめ）
	void SpawnBomb()
	{
		// bombPrefab（コピー元）を、spawnPoint（目印）と同じ場所・同じ向きで新しく登場させる！
		currentBomb = Instantiate(bombPrefab, spawnPoint.position, spawnPoint.rotation);
	}
}