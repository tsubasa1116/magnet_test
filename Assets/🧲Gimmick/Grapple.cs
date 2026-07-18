using UnityEngine;

public class Grapple : MonoBehaviour
{
    #region Effect

    [Header("Attract Effect")]

    [SerializeField]private GameObject nPoleObjectAttractEffect;
    [SerializeField]private GameObject sPoleObjectAttractEffect;

    [Header("Laser")]
    [SerializeField]private GameObject nPoleLaserEffect;
    [SerializeField]private GameObject sPoleLaserEffect;

    #endregion

    [Header("Physics")]
    [SerializeField]private GrapplePhysics physics = new GrapplePhysics();
    [SerializeField]private float grappleDuration = 2.0f;

    private float grappleTimer;
    private PlayerMovement movement;
    private Rigidbody playerRb;
    private GameObject player;

    private GameObject currentLaser;
    private LineRenderer currentLine;
    private GameObject currentEffect;

    private bool initialized;

    public bool IsGrappling
    {
        get
        {
            return physics.IsGrappling;
        }
    }

    private void Awake()
    {

    }

    /// Grapple開始
    public void StartGrapple(GameObject targetPlayer)
    {
        if (physics.IsGrappling) return;

        if (targetPlayer == null) return;

        player = targetPlayer;

        playerRb = player.GetComponent<Rigidbody>();

        if (playerRb == null) return;

        movement = player.GetComponent<PlayerMovement>();

        physics.Initialize(playerRb);

        physics.Begin(transform);

        initialized = true;

        if (movement != null) movement.IsOnGrapple = true;

        StartGrappleEffect();
        grappleTimer = grappleDuration;
    }

    /// Grapple終了
    public void StopGrapple()
    {
        if (!physics.IsGrappling) return;

        physics.End();

        if (movement != null) movement.IsOnGrapple = false;

        StopGrappleEffect();

        initialized = false;

        player = null;
        playerRb = null;
        movement = null;
    }

    private void Update()
    {
        if (!initialized) return;

        if (!physics.IsGrappling) return;

        grappleTimer -= Time.deltaTime;

        if (grappleTimer <= 0f)
        {
            StopGrapple();
            return;
        }

        UpdatePlayerRotation();

        UpdateEffect();

        UpdateLaser();
    }

    private void FixedUpdate()
    {
        if (!initialized) return;

        if (!physics.IsGrappling) return;

        // PlayerMovementが保持する入力を使うため、キーボードとゲームパッドの両方で操作できる。
        Vector2 moveInput = movement != null ? movement.MoveInput : Vector2.zero;

        physics.FixedTick(moveInput, Camera.main != null ? Camera.main.transform : null);
    }

    /// プレイヤーをアンカー方向へ向ける
    private void UpdatePlayerRotation()
    {
        if (player == null) return;

        Vector3 direction = physics.Anchor.position - player.transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        player.transform.rotation = Quaternion.RotateTowards(player.transform.rotation, targetRotation, 720f * Time.deltaTime);
    }

    /// 吸着エフェクト更新
    private void UpdateEffect()
    {
        if (currentEffect == null) return;

        Vector3 direction = (player.transform.position - transform.position).normalized;

        currentEffect.transform.position = transform.position + direction * 0.4f;

        currentEffect.transform.rotation = Quaternion.LookRotation(direction);
    }

    /// レーザー更新
    private void UpdateLaser()
    {
        if (currentLine == null) return;

        currentLine.SetPosition(0, transform.position);

        currentLine.SetPosition(1, player.transform.position);
    }

    /// エフェクト開始
    private void StartGrappleEffect()
    {
        //------------------------------------------------
        // 吸着エフェクト
        //------------------------------------------------
        if (currentEffect == null)
        {
            GameObject effectPrefab = null;

            if (CompareTag("S_Pole"))
                effectPrefab = sPoleObjectAttractEffect;
            else
                effectPrefab = nPoleObjectAttractEffect;

            if (effectPrefab != null)
            {
                currentEffect = Instantiate(effectPrefab, transform.position, Quaternion.identity, transform);
            }
        }

        //------------------------------------------------
        // レーザー
        //------------------------------------------------
        if (currentLaser == null)
        {
            GameObject laserPrefab = null;

            if (CompareTag("S_Pole"))
                laserPrefab = nPoleLaserEffect;
            else
                laserPrefab = sPoleLaserEffect;

            if (laserPrefab != null)
            {
                currentLaser = Instantiate(laserPrefab);

                currentLine = currentLaser.GetComponentInChildren<LineRenderer>();

                if (currentLine != null && player != null)
                {
                    currentLine.positionCount = 2;

                    currentLine.SetPosition(0, transform.position);
                    currentLine.SetPosition(1, player.transform.position);
                }
            }
        }
    }

    /// エフェクト終了
    private void StopGrappleEffect()
    {
        if (currentLaser != null)
        {
            Destroy(currentLaser);
            currentLaser = null;
            currentLine = null;
        }

        if (currentEffect != null)
        {
            Destroy(currentEffect);
            currentEffect = null;
        }
    }

    /// 現在のプレイヤー
    public GameObject Player
    {
        get
        {
            return player;
        }
    }

    /// プレイヤーRigidbody
    public Rigidbody PlayerRigidbody
    {
        get
        {
            return playerRb;
        }
    }

    /// GrapplePhysics取得
    public GrapplePhysics Physics
    {
        get
        {
            return physics;
        }
    }

    private void OnDisable()
    {
        StopGrapple();
    }

    private void OnDestroy()
    {
        StopGrapple();
    }
}
