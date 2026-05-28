using UnityEngine;

public class AnimationStateController : MonoBehaviour
{
	Animator animator;
	float velocity = 0.0f;
	public float acceleration = 0.1f;
	public float deceleration = 0.5f;
	int VelocityHash;

	void Start()
	{
		animator = GetComponent<Animator>();

		VelocityHash = Animator.StringToHash("Velocity");
	}

	void Update()
	{
		bool forwardPressed = Input.GetKey("w");
		bool runPressed = Input.GetKey("left shift");

		if (forwardPressed)
		{
			velocity += Time.deltaTime * acceleration;
		}
		if (!forwardPressed)
		{
			velocity -= Time.deltaTime * deceleration;
		}

		// 計算後に、必ず 0.0 ～ 1.0 の範囲内に収める（制限する）
		velocity = Mathf.Clamp01(velocity);

		animator.SetFloat(VelocityHash, velocity);
	}
}
