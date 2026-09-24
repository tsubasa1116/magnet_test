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

    [SerializeField] private float respawnRotationY = 0f;

    //[SerializeField] private Mission mission;

    //private bool activated = false;

    private void OnTriggerEnter(Collider other)
    {
        //if (activated) return;

        if (!other.CompareTag("Player")) return;

        //activated = true;

        Debug.Log($"[Checkpoint] {checkpointType} に到達 ({gameObject.name})");

        PlayerRespawn respawn = other.GetComponent<PlayerRespawn>();

        if (respawn != null)
        {
            respawn.SetCheckpoint(transform, respawnRotationY);
            respawn.SetRespawnBGM(GetBGMName());
            Debug.Log($"[Checkpoint] リスポーン地点を更新 回転Y={respawnRotationY}");
        }

        switch (checkpointType)
        {
            case CheckpointType.Normal:
                Debug.Log("[Checkpoint] Normal：処理なし");
                break;

            case CheckpointType.Floor2:
                Debug.Log("[Checkpoint] Floor2：ミッション『2Fへ移動』表示");
                // mission?.ClearAllMissions();
                // mission?.SetMission(1);
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
                break;

            case CheckpointType.Enemy:
                break;

            case CheckpointType.FloorChange:
                break;
        }
    }

    private string GetBGMName()
    {
        switch (checkpointType)
        {
            case CheckpointType.Boss:
                return "Boss";

            default:
                return "Normal";
        }
    }
}