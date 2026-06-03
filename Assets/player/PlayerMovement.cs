using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
	[Header("移動設定")]
	[SerializeField] private float moveSpeed = 5f;
	[SerializeField] private float dashSpeed = 12f;

	[Header("ジャンプ設定")]
	[SerializeField] private float jumpForce = 5f;
	[SerializeField] private LayerMask groundLayer;

	private Rigidbody rb;
	private Vector2 moveInput;
	private bool isDashing;
	private Transform cameraTransform;

	void Awake()
	{
		rb = GetComponent<Rigidbody>();
		cameraTransform = Camera.main.transform;
	}

	void FixedUpdate()
	{
		Move();
	}

	// --- 移動 ---

	public void OnMove(InputValue value)
	{
		moveInput = value.Get<Vector2>();
	}

	public void OnDash(InputValue value)
	{
		isDashing = value.isPressed;
	}

	private void Move()
	{
		if (moveInput == Vector2.zero) return;

		Vector3 forward = cameraTransform.forward;
		Vector3 right = cameraTransform.right;
		forward.y = 0f;
		right.y = 0f;
		forward.Normalize();
		right.Normalize();

		Vector3 moveDir = (forward * moveInput.y + right * moveInput.x).normalized;
		float currentSpeed = isDashing ? dashSpeed : moveSpeed;

		rb.linearVelocity = new Vector3(
			moveDir.x * currentSpeed,
			rb.linearVelocity.y,
			moveDir.z * currentSpeed
		);

		transform.rotation = Quaternion.LookRotation(moveDir);
	}

	// --- ジャンプ ---

	public void OnJump(InputValue value)
	{
		if (value.isPressed && IsGrounded())
		{
			rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
		}
	}

	private bool IsGrounded()
	{
		// 足元に小さいRayを飛ばして地面判定
		return Physics.Raycast(transform.position, Vector3.down, 1.1f, groundLayer);
	}
}