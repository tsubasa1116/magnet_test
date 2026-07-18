using UnityEngine;

public class Checkpoint : MonoBehaviour

{

	public enum CheckpointType

	{

		Normal,

		Floor2,

		Boss,

		Door,

		Enemy,

		FloorChange

	}

	[SerializeField] private CheckpointType checkpointType;

	[SerializeField] private enemy_Boss boss;

	//[SerializeField] private Mission mission;

	private bool activated = false;

	private void OnTriggerEnter(Collider other)

	{

		if (activated) return;

		if (!other.CompareTag("Player")) return;

		activated = true;

		Debug.Log($"[Checkpoint] {checkpointType} に到達 ({gameObject.name})");

		PlayerRespawn respawn = other.GetComponent<PlayerRespawn>();

		if (respawn != null)

		{

			respawn.SetCheckpoint(transform);

			Debug.Log("[Checkpoint] リスポーン地点を更新");

		}

		switch (checkpointType)

		{

			case CheckpointType.Normal:

				Debug.Log("[Checkpoint] Normal：処理なし");

				break;

			case CheckpointType.Floor2:

				Debug.Log("[Checkpoint] Floor2：ミッション『2Fへ移動』表示");

				//        mission?.ClearAllMissions();

				//        mission?.SetMission(1);

				break;

			case CheckpointType.Boss:

				Debug.Log("[Checkpoint] Boss：ミッション『ボス』『バリア』表示・ボス起動");

				//mission?.ClearAllMissions();

				//mission?.SetMission(2);

				//mission?.SetMission(3);

				if (boss != null)

				{

					boss.isStartAction = true;

					Debug.Log("[Checkpoint] ボス行動開始");

				}

				break;

			case CheckpointType.Door:

				//    Debug.Log("[Checkpoint] Door：ミッション『ドア』表示");

				//    mission?.ClearAllMissions();

				//    mission?.SetMission(4);

				break;

			case CheckpointType.Enemy:

				//    Debug.Log("[Checkpoint] Enemy：ミッション『敵撃破』表示");

				//    mission?.ClearAllMissions();

				//    mission?.SetMission(5);

				break;

			case CheckpointType.FloorChange:

				//    Debug.Log("[Checkpoint] FloorChange：ミッション『部屋移動』表示");

				//    mission?.ClearAllMissions();

				//    mission?.SetMission(6);

				break;

		}

	}

}
