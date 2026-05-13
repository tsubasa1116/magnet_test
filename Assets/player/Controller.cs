using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Controller : MonoBehaviour
{
	// 既存の変数
	Quaternion targetRotation;
	Rigidbody rb;
	float jumpForce = 10;
	public bool isJumping;

	public int hp = 3;

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
	
	// ★追加：カメラ操作用の変数
	private Vector2 cameraInput;
	public Transform cameraTransform;   // カメラ（またはカメラの親のピボット）をInspectorで登録
	public float cameraSensitivity = 200f;
	private float cameraPitch = 0f;     // 上下回転の蓄積

	void Awake()
	{
		controls = new PlayerControls();

		// ジャンプの入力
		controls.Player.Jump.performed += ctx => 
		{
			PerformJump();
		};

		// ★修正：自動生成されたプロパティ名に合わせて _3DManeuverGear を使う
		controls.Player.ManeuverGear.performed += ctx =>
		{
			StartManeuverGear();
		};

		// ★追加：Invert（極の入れ替え）の入力
		controls.Player.Invert.performed += ctx =>
		{
			PerformInvert();
		};
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

		// カメラが設定されていない場合はメインカメラを自動取得
		if (cameraTransform == null && Camera.main != null)
		{
			cameraTransform = Camera.main.transform;
		}
	}
	
	void Update()
	{
		if (isMagnetMoving)
		{
			Vector3 directionToTarget = (magnetTargetPoint - transform.position).normalized;
			transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(directionToTarget), 600 * Time.deltaTime);

			if (swingJoint != null)
			{
				swingJoint.maxDistance = Mathf.MoveTowards(swingJoint.maxDistance, 0f, magnetSpeed * Time.deltaTime);
			}

			// 十分に近づいたら解除
			if (Vector3.Distance(transform.position, magnetTargetPoint) < 5.0f)
			{
				StopSwing();
			}
			return; 
		}

		// ★追加：キーボードからのジャンプ入力
		if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
		{
			PerformJump();
		}

		// ★修正：FindActionを使わず、直接 Move プロパティから値を読み取る
		moveInput = controls.Player.Move.ReadValue<Vector2>();

		// ★追加：キーボードのWASDでの移動入力を加算 (優先)
		if (Keyboard.current != null)
		{
			float keyboardX = 0f;
			float keyboardY = 0f;

			if (Keyboard.current.dKey.isPressed) keyboardX += 1f;
			if (Keyboard.current.aKey.isPressed) keyboardX -= 1f;
			if (Keyboard.current.wKey.isPressed) keyboardY += 1f;
			if (Keyboard.current.sKey.isPressed) keyboardY -= 1f;

			if (keyboardX != 0f || keyboardY != 0f)
			{
				moveInput = new Vector2(keyboardX, keyboardY).normalized;
			}
		}
		
		// カメラのY軸回転を基準にして移動方向を決定
		float camYaw = cameraTransform != null ? cameraTransform.eulerAngles.y : Camera.main.transform.eulerAngles.y;
		var horizontalRotation = Quaternion.AngleAxis(camYaw, Vector3.up);
		var velocity = horizontalRotation * new Vector3(moveInput.x, 0, moveInput.y).normalized;

		var speed = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed ? 10 : 7;
		var rotationSpeed = 600 * Time.deltaTime;

		if (velocity.magnitude > 0.5f)
		{
			targetRotation = Quaternion.LookRotation(velocity, Vector3.up);
		}
		transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed);

		Vector3 nextPosition = rb.position + velocity * speed * Time.deltaTime;
		rb.MovePosition(nextPosition);
	}

	// ★追加：両方から呼び出せるようにジャンプ処理を関数化
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

	// ★追加：磁極の反転処理
	private void PerformInvert()
	{
		if (magnetScript != null)
		{
			// 現在のモードが 1(N極) なら 2(S極) へ、そうでなければ 1(N極) に切り替える
			int newMode = magnetScript.magnetMode == 1 ? 2 : 1;
			
			// magnetスクリプトのメソッドを呼び出してモードを変更
			magnetScript.ChangeMode(newMode);
			
			Debug.Log("極を反転しました: " + (newMode == 1 ? "N極" : "S極"));
		}
	}

	// ★追加：カメラの回転処理関数
	private void HandleCameraRotation()
	{
		if (cameraTransform == null) return;

		// Time.deltaTimeを掛けることで、フレームレートに依存せず一定の速度で回転するようになります。
		// 感度(cameraSensitivity)は Inspector で 150～300 などの大きめの値に設定してみてください。
		float currentSensitivity = cameraSensitivity * Time.deltaTime;

		// 左右の回転（Y軸）
		cameraTransform.Rotate(Vector3.up, cameraInput.x * currentSensitivity, Space.World);

		// 上下の回転（X軸） - 制限をかけるため別計算
		cameraPitch -= cameraInput.y * currentSensitivity;
		cameraPitch = Mathf.Clamp(cameraPitch, -60f, 60f); // 上下向きすぎ防止

		// 新しい回転を適用
		cameraTransform.localEulerAngles = new Vector3(cameraPitch, cameraTransform.localEulerAngles.y, 0f);
	}

	// ★変更：立体機動の開始処理を関数化
	private void StartManeuverGear()
	{
		if (isMagnetMoving) return;

		Transform camT = cameraTransform != null ? cameraTransform : Camera.main.transform;
		
		// 画面中央（カメラの向き）にRayを飛ばす
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