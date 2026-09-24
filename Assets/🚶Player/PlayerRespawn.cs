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

    private string respawnBGMName = "Normal";

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

        Debug.Log($"[PlayerRespawn] Awake 初期位置={checkpointPosition}");
        Debug.Log($"[PlayerRespawn] Continue={ContinueFromCheckpoint} Saved={hasSavedCheckpoint}");

        if (ContinueFromCheckpoint && hasSavedCheckpoint)
        {
            checkpointPosition = savedCheckpointPosition;
            checkpointRotation = savedCheckpointRotation;

            transform.SetPositionAndRotation(savedCheckpointPosition, savedCheckpointRotation);

            Debug.Log($"[PlayerRespawn] チェックポイントへ移動={savedCheckpointPosition}");

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            Debug.Log($"[PlayerRespawn] 移動後の実位置={transform.position}");
        }

        Debug.Log($"[PlayerRespawn] Awake終了位置={transform.position}");
        lastPosition = transform.position;
    }

    private void Start()
    {
        if (ContinueFromCheckpoint && hasSavedCheckpoint)
        {
            transform.SetPositionAndRotation(savedCheckpointPosition, savedCheckpointRotation);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            Debug.Log($"[PlayerRespawn] Startで再配置: {savedCheckpointPosition}");
        }

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

    private Vector3 lastPosition;

    private void Update()
    {
        if (transform.position != lastPosition)
        {
            if (PlayerRespawn.ContinueFromCheckpoint)
            {
                Debug.LogError($"[PlayerRespawn] チェックポイント復帰中に位置変更: {lastPosition} → {transform.position}");
                Debug.LogError(System.Environment.StackTrace);
            }

            lastPosition = transform.position;
        }
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

    public void SetCheckpoint(Transform checkpoint, float rotationY)
    {
        checkpointPosition = checkpoint.position;
        checkpointRotation = Quaternion.Euler(0f, rotationY, 0f);

        // コンティニュー(シーン再読込)用にstaticにも控える
        hasSavedCheckpoint = true;
        savedCheckpointPosition = checkpoint.position;
        savedCheckpointRotation = Quaternion.Euler(0f, rotationY, 0f);
        SavedSceneName = SceneManager.GetActiveScene().name;

        Debug.Log($"[PlayerRespawn] チェックポイント保存 Position={checkpointPosition} RotationY={rotationY}");
    }

    public void SetRespawnBGM(string bgmName)
    {
        respawnBGMName = bgmName;
    }

    public void Respawn()
    {
        Debug.Log($"[PlayerRespawn] リスポーン実行 Position={checkpointPosition} Rotation={checkpointRotation.eulerAngles}");

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        transform.SetPositionAndRotation(checkpointPosition, checkpointRotation);

        ragdoll.DisableRagdoll();   // ラグドール解除
        health.Revive();            // HP回復・死亡状態解除

        AudioManager.Instance.PlayBGM(respawnBGMName);
    }
}
