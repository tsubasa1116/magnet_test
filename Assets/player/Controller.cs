using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.VFX; // ★追加: VFXを使うために必要

public class Controller : MonoBehaviour
{
    Quaternion targetRotation;
    Rigidbody rb;
    float jumpForce = 10;
    public bool isJumping;
    public int hp = 10;

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
    public float cameraSensitivity = 10f;
    private float cameraPitch = 0f;

    [Header("Effects")]
    [SerializeField] private ParticleSystem dashEffect; // これはParticleSystemのまま（もしダッシュもVFXならVisualEffectに変更してください）
    
    // ★変更: VisualEffect から GameObject に変更し、Prefabをアタッチできるようにする
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private GameObject hitEffectPrefab;
    private bool wasDashing = false;

    [Header("Dash Settings")]
    public float normalSpeed = 7f;
    public float dashSpeed = 12f;

    [Header("Jump Settings")]
    public float airSpeedMultiplier = 0.8f;

    [Header("Gravity Settings")]
    public float gravityMultiplier = 1.5f;

    void Awake()
    {
        controls = new PlayerControls();
        controls.Player.Jump.performed += ctx => PerformJump();
        controls.Player.ManeuverGear.performed += ctx => StartManeuverGear();
        controls.Player.Invert.performed += ctx => PerformInvert();
    }

    void OnEnable() { controls.Enable(); }
    void OnDisable() { controls.Disable(); }

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

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (dashEffect != null)
        {
            var mainModule = dashEffect.main;
            mainModule.loop = true;
            mainModule.simulationSpace = ParticleSystemSimulationSpace.World;

            var emissionModule = dashEffect.emission;
            if (emissionModule.rateOverTime.constant <= 0f)
            {
                emissionModule.rateOverTime = 20f;
            }
        }
    }
    
    void Update()
    {
        if (isMagnetMoving)
        {
            HandleMagnetMovement();
            return; 
        }

        moveInput = controls.Player.Move.ReadValue<Vector2>();
        cameraInput = controls.Player.Camera.ReadValue<Vector2>();
        bool isDash = controls.Player.Dash.IsPressed();

        bool isEffectActive = isDash && moveInput.magnitude > 0.1f;
        if (dashEffect != null)
        {
            if (isEffectActive && !wasDashing)
            {
                dashEffect.Play();
            }
            else if (!isEffectActive && wasDashing)
            {
                dashEffect.Stop();
            }
        }
        wasDashing = isEffectActive;

        HandleMovement(isDash);
    }

    void FixedUpdate()
    {
        if (rb != null && gravityMultiplier > 1f)
        {
            rb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
        }
    }

    // 移動処理
    private void HandleMovement(bool isDash)
    {
        float currentSpeed = isDash ? dashSpeed : normalSpeed;
        if (isJumping) currentSpeed *= airSpeedMultiplier;

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

    private void PerformJump()
    {
        if (isMagnetMoving) StopSwing();
        else if (!isJumping)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            isJumping = true;
        }
    }

    private void PerformInvert()
    {
        if (magnetScript != null)
        {
            int newMode = magnetScript.magnetMode == 1 ? 2 : 1;
            magnetScript.ChangeMode(newMode);
            Debug.Log("極を反転しました: " + (newMode == 1 ? "N極" : "S極"));
        }
    }

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

    private void HandleMagnetMovement()
    {
        Vector3 directionToTarget = (magnetTargetPoint - transform.position).normalized;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(directionToTarget), 600 * Time.deltaTime);

        if (swingJoint != null)
            swingJoint.maxDistance = Mathf.MoveTowards(swingJoint.maxDistance, 0f, magnetSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, magnetTargetPoint) < 5.0f)
            StopSwing();
    }

    private void StopSwing()
    {
        isMagnetMoving = false;
        if (swingJoint != null) Destroy(swingJoint);
    }

    private void OnTriggerEnter(Collider other)
    {
        isJumping = false;
    }

    public void TakeDamage(int damage)
    {
        hp -= damage;
        Debug.Log("プレイヤーがダメージを受けた！ 残りHP: " + hp);

        // ★修正: SendEventではなく標準のPlay()メソッドを呼ぶ
        if (hitEffectPrefab != null)
        {
            Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
        }

        if (hp <= 0) 
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("プレイヤーがやられた！");

        // ★修正: 爆発エフェクトのプレハブは「死亡時」のみ生成する
        if (explosionPrefab != null)
        {
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        }

        // ★任意: プレイヤー自身を非表示にする
        // gameObject.SetActive(false);
    }
}