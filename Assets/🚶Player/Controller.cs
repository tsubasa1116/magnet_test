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

    // 磁石変数
    [SerializeField] float magnetSpeed = 10f;

    [Header("立体機動")]
    [SerializeField] float grappleRange = 30f;
    private magnet magnetScript;

    // コントローラー入力用の変数
    private PlayerControls controls;
    private Vector2 moveInput;
    private Vector2 cameraInput;

    // カメラ操作用の変数
    public Transform cameraTransform;
    public float cameraSensitivity = 10f;
    private float cameraPitch = 0f;

    [Header("エフェクト")]
    [SerializeField] private GameObject runEffect;
    [SerializeField] private GameObject hitEffect;
    [SerializeField] private GameObject nPoleChangeEffect;
    [SerializeField] private GameObject sPoleChangeEffect;
    [SerializeField] private GameObject downEffect;

    [Header("ダッシュ")]
    public float normalSpeed = 7f;
    public float dashSpeed = 12f;
    private GameObject currentRunEffect;
    private Coroutine stopRunEffectCoroutine;

    [Header("ジャンプ")]
    public float airSpeedMultiplier = 0.8f;

    [Header("重力")]
    public float gravityMultiplier = 1.5f;

    // ロープウェイ吸着中
    [HideInInspector] public bool isOnRopeway = false;

    void Awake()
    {
        controls = new PlayerControls();
        controls.Player.Jump.performed += ctx => PerformJump();
        controls.Player.MagnetONOFF.performed += ctx => StartManeuverGear();
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

        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        moveInput = controls.Player.Move.ReadValue<Vector2>();
        cameraInput = controls.Player.Camera.ReadValue<Vector2>();
        bool isDash = controls.Player.Dash.IsPressed();

        HandleRunEffect(isDash);

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
        if (isOnRopeway)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

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

    private void HandleRunEffect(bool isDash)
    {
        if (isDash)
        {
            if (stopRunEffectCoroutine != null)
            {
                StopCoroutine(stopRunEffectCoroutine);
                stopRunEffectCoroutine = null;
            }

            if (currentRunEffect == null && runEffect != null)
            {
                currentRunEffect = Instantiate(runEffect, transform.position, Quaternion.identity, transform);

                currentRunEffect.transform.localPosition = Vector3.zero;
            }
        }
        // ダッシュ終了
        else
        {
            if (currentRunEffect != null && stopRunEffectCoroutine == null)
            {
                stopRunEffectCoroutine = StartCoroutine(StopRunEffect());
            }
        }
    }

    private IEnumerator StopRunEffect()
    {
        yield return new WaitForSeconds(0.5f);

        if (currentRunEffect != null)
        {
            currentRunEffect.transform.SetParent(null);

            ParticleSystem ps = currentRunEffect.GetComponent<ParticleSystem>();

            if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            Destroy(currentRunEffect, 2.0f);
            currentRunEffect = null;
        }

        stopRunEffectCoroutine = null;
    }

    private void PerformJump()
    {
        if (isOnRopeway)
            return;

        if (!isJumping)
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

            // N極になった時
            if (newMode == 1)
            {
                Debug.Log("極を反転しました: N極");

                if (nPoleChangeEffect != null)
                {
                    GameObject effect = Instantiate(nPoleChangeEffect, transform.position, Quaternion.identity, transform);
                    Destroy(effect, 1.5f); // 一定時間後に削除
                }
            }
            // S極になった時
            else
            {
                Debug.Log("極を反転しました: S極");

                if (sPoleChangeEffect != null)
                {
                    GameObject effect = Instantiate(sPoleChangeEffect, transform.position, Quaternion.identity, transform);
                    Destroy(effect, 1.5f); // 一定時間後に削除
                }
            }
        }
    }

    // 立体機動処理
    private void StartManeuverGear()
    {
        Transform camT = cameraTransform != null ? cameraTransform : Camera.main.transform;

        Ray ray = new Ray(camT.position, camT.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, grappleRange))
        {
            Grapple grapple = hit.collider.GetComponent<Grapple>();

            if (grapple != null)
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
                    grapple.StartGrapple(gameObject);
                    grapple.StartGrappleEffect();
                }
            }
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

        if (hitEffect != null) Instantiate(hitEffect, transform.position, Quaternion.identity);

        if (hp <= 0) Die();
    }

    private void Die()
    {
        Debug.Log("プレイヤーがやられた！");

        if (downEffect != null) Instantiate(downEffect, transform.position, Quaternion.identity);

        // プレイヤー自身を非表示にする
        gameObject.SetActive(false);
    }
}