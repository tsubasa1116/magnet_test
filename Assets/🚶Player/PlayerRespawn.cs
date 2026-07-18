using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// プレイヤーの死亡後の流れを管理する。
// 死亡 → ラグドール・スロー演出を respawnDelay 秒見せる → GameOver画面へ遷移。
// GameOver画面で「チェックポイントから」を選ぶとシーンが読み直され、
// static に控えたセーブポイントへ配置してから再開する(自動復活はしない)。
[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerRespawn : MonoBehaviour
{
    [Tooltip("死亡してからゲームオーバー画面へ移るまでの時間(倒れる演出を見せる間)")]
    [SerializeField] private float respawnDelay = 3f;

    // --- シーン再読込をまたいで持ち越すコンティニュー情報 ---
    // GameOver画面のコンティニューでtrueになり、読み直したシーンの初回フレーム後に消費される。
    // GameStartCutsceneはこのフラグを見てオープニングをスキップする
    public static bool ContinueFromCheckpoint { get; private set; }
    public static string SavedSceneName { get; private set; }
    private static bool hasSavedCheckpoint;
    private static Vector3 savedCheckpointPosition;
    private static Quaternion savedCheckpointRotation;

    private Vector3 checkpointPosition;
    private Quaternion checkpointRotation;

    private Rigidbody rb;
    private PlayerHealth health;
    private PlayerRagdoll ragdoll;

    // GameOver画面の「チェックポイントから再開」がシーン読込前に呼ぶ
    public static void BeginContinueFromCheckpoint()
    {
        ContinueFromCheckpoint = true;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        health = GetComponent<PlayerHealth>();
        ragdoll = GetComponent<PlayerRagdoll>();

        checkpointPosition = transform.position;
        checkpointRotation = transform.rotation;

        // コンティニューでシーンが読み直された場合はセーブポイントから開始
        if (ContinueFromCheckpoint && hasSavedCheckpoint)
        {
            checkpointPosition = savedCheckpointPosition;
            checkpointRotation = savedCheckpointRotation;
            transform.SetPositionAndRotation(savedCheckpointPosition, savedCheckpointRotation);
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    private void Start()
    {
        // GameStartCutscene等が全員フラグを読み終わる初回フレームの後に消費する
        if (ContinueFromCheckpoint) StartCoroutine(ClearContinueFlag());
    }

    private IEnumerator ClearContinueFlag()
    {
        yield return null;
        ContinueFromCheckpoint = false;
    }

    private void OnEnable()
    {
        health.OnDied += StartRespawn;
    }

    private void OnDisable()
    {
        health.OnDied -= StartRespawn;
    }

    private void StartRespawn()
    {
        StartCoroutine(RespawnCoroutine());
    }

    // 死亡: 倒れる演出を見せてからGameOver画面へ。
    // 復活するかどうかはGameOver画面(コンティニュー/タイトル)でプレイヤーが選ぶ
    private IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        GameOverTransition transition = FindFirstObjectByType<GameOverTransition>();
        if (transition != null)
        {
            SavedSceneName = SceneManager.GetActiveScene().name;
            transition.GoToGameOver();
        }
        else
        {
            // GameOverManagerが無いシーンでは従来どおりその場で復活する
            Respawn();
        }
    }

    public void SetCheckpoint(Transform point)
    {
        checkpointPosition = point.position;
        checkpointRotation = point.rotation;

        // コンティニュー(シーン再読込)用にstaticにも控える
        hasSavedCheckpoint = true;
        savedCheckpointPosition = point.position;
        savedCheckpointRotation = point.rotation;
        SavedSceneName = SceneManager.GetActiveScene().name;
    }

    public void Respawn()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        transform.SetPositionAndRotation(checkpointPosition, checkpointRotation);

        ragdoll.DisableRagdoll();   // ラグドール解除
        health.Revive();            // HP回復・死亡状態解除
    }
}
