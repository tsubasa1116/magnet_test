using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Controller : MonoBehaviour
{
    Quaternion targetRotation;
    Rigidbody rb;
    float jumpForce = 10;
    public bool isJumping;
    public int hp = 100;

    [SerializeField] SphereCollider jumpCollider;

    // 磁石・立体機動用の変数
    [SerializeField] float magnetRange = 30f;
    [SerializeField] float magnetSpeed = 10f;
    private bool isMagnetMoving = false;
    private Vector3 magnetTargetPoint;
    private SpringJoint swingJoint;
    private magnet magnetScript;

    // コントローラー入力用の変数
    private PlayerControls controls;
    private Vector2 moveInput;
    private Vector2 cameraInput;

    // カメラ操作用の変数
    public Transform cameraTransform;
    public float cameraSensitivity = 10f; // ★マウス/スティック共通の感度
    private float cameraPitch = 0f;

    // ダッシュ用の変数
    [Header("Dash Settings")]
    public float normalSpeed = 7f;
    public float dashSpeed = 12f;

    void Awake()
    {
        controls = new PlayerControls();

        // 1発だけ押されたときの処理 (Buttonアクション)
        controls.Player.Jump.performed += ctx => PerformJump();
        controls.Player.ManeuverGear.performed += ctx => StartManeuverGear();
        controls.Player.Invert.performed += ctx => PerformInvert();
    }

    void OnEnable()
    {
        controls.Enable();
    }

    void OnDisable()
    {
        controls.Disable();
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        magnetScript = GetComponentInChildren<magnet>();
        isJumping = false;
        targetRotation = transform.rotation;

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        // マウスカーソルを画面中央にロックして非表示にする
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
    void Update()
    {
        if (isMagnetMoving)
        {
            HandleMagnetMovement();
            return; 
        }

        // 入力値の取得 (Valueアクションと状態判定)
        moveInput = controls.Player.Move.ReadValue<Vector2>();
        cameraInput = controls.Player.Camera.ReadValue<Vector2>();
        bool isDash = controls.Player.Dash.IsPressed();

        // カメラと移動の実行
        HandleCameraRotation();
        HandleMovement(isDash);
    }

    // 移動処理
    private void HandleMovement(bool isDash)
    {
        float currentSpeed = isDash ? dashSpeed : normalSpeed;

        // カメラの向きを基準に移動方向を決定
        float camYaw = cameraTransform != null ? cameraTransform.eulerAngles.y : Camera.main.transform.eulerAngles.y;
        var horizontalRotation = Quaternion.AngleAxis(camYaw, Vector3.up);
        var velocity = horizontalRotation * new Vector3(moveInput.x, 0, moveInput.y).normalized;

        if (velocity.magnitude > 0.1f)
        {
            targetRotation = Quaternion.LookRotation(velocity, Vector3.up);
        }
        
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 600 * Time.deltaTime);

        Vector3 nextPosition = rb.position + velocity * currentSpeed * Time.deltaTime;
        rb.MovePosition(nextPosition);
    }

    // カメラ回転処理
    private void HandleCameraRotation()
    {
        if (cameraTransform == null) return;

        // マウスの場合は値が大きく、右スティックの場合は値が小さいため、デバイスによって感度の補正をかける
        bool isMouse = controls.Player.Camera.activeControl?.device is Pointer;
        float sensitivityMultiplier = isMouse ? 0.05f : (10f * Time.deltaTime);

        float finalYaw = cameraInput.x * cameraSensitivity * sensitivityMultiplier;
        float finalPitch = cameraInput.y * cameraSensitivity * sensitivityMultiplier;

        // 左右回転適用 (Y軸)
        cameraTransform.Rotate(Vector3.up, finalYaw, Space.World);

        // 上下回転適用 (X軸)
        cameraPitch -= finalPitch;
        cameraPitch = Mathf.Clamp(cameraPitch, -60f, 60f);

        cameraTransform.localEulerAngles = new Vector3(cameraPitch, cameraTransform.localEulerAngles.y, 0f);
    }

    // ジャンプ処理
    private void PerformJump()
    {
        if (isMagnetMoving)
        {
            StopSwing();
        }
        else if (!isJumping)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            isJumping = true;
        }
    }

    // 極反転処理
    private void PerformInvert()
    {
        if (magnetScript != null)
        {
            int newMode = magnetScript.magnetMode == 1 ? 2 : 1;
            magnetScript.ChangeMode(newMode);
            Debug.Log("極を反転しました: " + (newMode == 1 ? "N極" : "S極"));
        }
    }

    // 立体機動の開始
    private void StartManeuverGear()
    {
        if (isMagnetMoving) return;

        Transform camT = cameraTransform != null ? cameraTransform : Camera.main.transform;
        Ray ray = new Ray(camT.position, camT.forward);
        
        if (Physics.Raycast(ray, out RaycastHit hit, magnetRange))
        {
            bool isSPole = hit.collider.CompareTag("S_Pole");
            bool isNPole = hit.collider.CompareTag("N_Pole");
            bool canGrapple = false;

            if (magnetScript != null)
            {
                if (magnetScript.magnetMode == 1 && isSPole) canGrapple = true;
                if (magnetScript.magnetMode == 2 && isNPole) canGrapple = true;
            }

            if (canGrapple)
            {
                isMagnetMoving = true;
                magnetTargetPoint = hit.point;
                
                swingJoint = gameObject.AddComponent<SpringJoint>();
                swingJoint.autoConfigureConnectedAnchor = false;
                swingJoint.connectedAnchor = magnetTargetPoint;

                float distanceFromPoint = Vector3.Distance(transform.position, magnetTargetPoint);
                swingJoint.maxDistance = distanceFromPoint * 0.8f; 
                swingJoint.minDistance = 0f;

                swingJoint.spring = 10f;
                swingJoint.damper = 5f;
                swingJoint.massScale = 4.5f;

                rb.AddForce(camT.forward * 10f, ForceMode.Impulse);
            }
        }
    }

    // 立体機動中の移動処理
    private void HandleMagnetMovement()
    {
        Vector3 directionToTarget = (magnetTargetPoint - transform.position).normalized;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(directionToTarget), 600 * Time.deltaTime);

        if (swingJoint != null)
        {
            swingJoint.maxDistance = Mathf.MoveTowards(swingJoint.maxDistance, 0f, magnetSpeed * Time.deltaTime);
        }

        if (Vector3.Distance(transform.position, magnetTargetPoint) < 5.0f)
        {
            StopSwing();
        }
    }

    private void StopSwing()
    {
        isMagnetMoving = false;
        if (swingJoint != null)
        {
            Destroy(swingJoint);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        isJumping = false;
    }

    public void TakeDamage(int damage)
    {
        hp -= damage;
        Debug.Log("プレイヤーがダメージを受けた！ 残りHP: " + hp);
        if (hp <= 0) Die();
    }

    private void Die()
    {
        Debug.Log("プレイヤーがやられた！");
    }
}