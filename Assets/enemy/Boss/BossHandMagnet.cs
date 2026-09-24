using UnityEngine;

// ボスの手(左右)を磁力(N/S)の対象にする。enemy_Boss が起動時に手のボーンへ付ける。
// 攻撃で飛んできた手をプレイヤーが逆極で引き寄せると、その場で本体から切り離して
// 分離用の腕(NSオブジェクト)に差し替える。以降は通常のNSオブジェクトと同じく
// 引き寄せ → 保持 → 極切替で発射できる。
public class BossHandMagnet : MonoBehaviour
{
	private enemy_Boss boss;
	private bool isLeft;
	private MagnetState pole;

	public MagnetState Pole => pole;

	// コライダーが属するボスの手を探す。
	// 手のボーン配下のコライダーに加え、腕の根元のRigidbodyにまとまっている腕のコライダーも手として扱う
	public static BossHandMagnet FindFor(Collider col)
	{
		BossHandMagnet hand = col.GetComponentInParent<BossHandMagnet>();
		if (hand != null) return hand;

		Rigidbody body = col.attachedRigidbody;
		return body != null ? body.GetComponentInChildren<BossHandMagnet>() : null;
	}

	public void Setup(enemy_Boss boss, bool isLeft, MagnetState pole)
	{
		this.boss = boss;
		this.isLeft = isLeft;
		this.pole = pole;
	}

	// プレイヤーの極で引き寄せられるか(逆極 かつ 攻撃で手が飛んできている間)
	public bool CanBePulledBy(MagnetState playerPole)
	{
		return boss != null && playerPole != pole && boss.CanGrabHand(isLeft);
	}

	// 本体から切り離し、代わりに掴ませる分離用の腕を返す(切り離せなければnull)
	public Rigidbody DetachForMagnet()
	{
		return boss != null ? boss.DetachHandForMagnet(isLeft) : null;
	}
}
