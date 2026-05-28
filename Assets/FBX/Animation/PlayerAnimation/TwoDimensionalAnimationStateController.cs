//using UnityEngine;
//public class TwoDimensionalAnimationStateController : MonoBehaviour
//{
//	Animator animator;
//	float velocityZ = 0.0f;
//	float velocityX = 0.0f;
//	public float acceleration = 2.0f;
//	public float deceleration = 2.0f;
//	public float maximumWalkVelocity = 0.5f;
//	public float maximumRunVelocity = 2.0f;

//	void Start()
//	{
//		animator = GetComponent<Animator>();
//	}

//	void Update()
//	{
//		bool forwardPressed = Input.GetKey("w");
//		bool rightPressed = Input.GetKey("d");
//		bool leftPressed = Input.GetKey("a");
//		bool runPressed = Input.GetKey("left shift");
//		float currentMaxVelocity = runPressed ? maximumRunVelocity : maximumWalkVelocity;

//		// 前進
//		if (forwardPressed && velocityZ < currentMaxVelocity)
//			velocityZ += Time.deltaTime * acceleration;
//		// 左
//		if (leftPressed && velocityX > -currentMaxVelocity)
//			velocityX -= Time.deltaTime * acceleration;
//		// 右
//		if (rightPressed && velocityX < currentMaxVelocity)
//			velocityX += Time.deltaTime * acceleration;

//		// 前進キーを離したら減速
//		if (!forwardPressed && velocityZ > 0.0f)
//			velocityZ -= Time.deltaTime * deceleration;
//		if (!forwardPressed && velocityZ < 0.0f)
//			velocityZ = 0.0f;

//		// 左右キーを離したら減速
//		if (!leftPressed && velocityX < 0.0f)
//			velocityX += Time.deltaTime * deceleration;
//		if (!rightPressed && velocityX > 0.0f)
//			velocityX -= Time.deltaTime * deceleration;

//		// 左右の微小値をゼロにスナップ
//		if (!leftPressed && !rightPressed && velocityX != 0.0f && (velocityX > -0.05f && velocityX < 0.05f))
//			velocityX = 0.0f;

//		// 前進の速度制限・スナップ
//		if (forwardPressed && runPressed && velocityZ > currentMaxVelocity)
//			velocityZ = currentMaxVelocity;
//		else if (forwardPressed && velocityZ > currentMaxVelocity)
//		{
//			velocityZ -= Time.deltaTime * deceleration;
//			if (velocityZ > currentMaxVelocity && velocityZ < (currentMaxVelocity + 0.05f))
//				velocityZ = currentMaxVelocity;
//		}
//		else if (forwardPressed && velocityZ < currentMaxVelocity && velocityZ > (currentMaxVelocity - 0.05f))
//			velocityZ = currentMaxVelocity;

//		// 右移動の速度制限・スナップ
//		if (rightPressed && runPressed && velocityX > currentMaxVelocity)
//			velocityX = currentMaxVelocity;
//		else if (rightPressed && velocityX > currentMaxVelocity)
//		{
//			velocityX -= Time.deltaTime * deceleration;
//			if (velocityX > currentMaxVelocity && velocityX < (currentMaxVelocity + 0.05f))
//				velocityX = currentMaxVelocity;
//		}
//		else if (rightPressed && velocityX < currentMaxVelocity && velocityX > (currentMaxVelocity - 0.05f))
//			velocityX = currentMaxVelocity;

//		// 左移動の速度制限・スナップ
//		if (leftPressed && runPressed && velocityX < -currentMaxVelocity)
//			velocityX = -currentMaxVelocity;
//		else if (leftPressed && velocityX < -currentMaxVelocity)
//		{
//			velocityX += Time.deltaTime * deceleration;
//			if (velocityX < -currentMaxVelocity && velocityX > (-currentMaxVelocity - 0.05f))
//				velocityX = -currentMaxVelocity;
//		}
//		else if (leftPressed && velocityX > -currentMaxVelocity && velocityX < (-currentMaxVelocity + 0.05f))
//			velocityX = -currentMaxVelocity;

//		animator.SetFloat("Velocity Z", velocityZ);
//		animator.SetFloat("Velocity X", velocityX);
//	}
//}

using UnityEngine;

public class TwoDimensionalAnimationStateController : MonoBehaviour
{
	static readonly int VelocityZHash = Animator.StringToHash("Velocity Z");
	static readonly int VelocityXHash = Animator.StringToHash("Velocity X");

	[SerializeField] float acceleration = 2.0f;
	[SerializeField] float deceleration = 2.0f;
	[SerializeField] float maximumWalkVelocity = 0.5f;
	[SerializeField] float maximumRunVelocity = 2.0f;

	Animator animator;
	Vector2 velocity;

	void Start()
	{
		animator = GetComponent<Animator>();
	}

	void Update()
	{
		bool forwardPressed = Input.GetKey(KeyCode.W);
		bool leftPressed = Input.GetKey(KeyCode.A);
		bool rightPressed = Input.GetKey(KeyCode.D);
		bool runPressed = Input.GetKey(KeyCode.LeftShift);

		float maxVelocity = runPressed ? maximumRunVelocity : maximumWalkVelocity;

		velocity.y = UpdateAxis(velocity.y, forwardPressed, false, maxVelocity);
		velocity.x = UpdateAxis(velocity.x, rightPressed, leftPressed, maxVelocity);

		animator.SetFloat(VelocityZHash, velocity.y);
		animator.SetFloat(VelocityXHash, velocity.x);
	}

	/// <summary>
	/// 1軸分の速度を更新する
	/// </summary>
	/// <param name="current">現在の速度</param>
	/// <param name="positivePressed">正方向キーが押されているか</param>
	/// <param name="negativePressed">負方向キーが押されているか</param>
	/// <param name="maxVelocity">最大速度</param>
	float UpdateAxis(float current, bool positivePressed, bool negativePressed, float maxVelocity)
	{
		float delta = Time.deltaTime;
		float snapThreshold = 0.05f;

		// 加速
		if (positivePressed) current += delta * acceleration;
		if (negativePressed) current -= delta * acceleration;

		// 減速
		if (!positivePressed && current > 0f) current -= delta * deceleration;
		if (!negativePressed && current < 0f) current += delta * deceleration;

		// 最大速度を超えたら減速してスナップ
		current = ClampToMax(current, maxVelocity, positivePressed, delta, snapThreshold);
		current = ClampToMax(current, -maxVelocity, negativePressed, delta, snapThreshold);

		// ゼロ付近をスナップ
		if (!positivePressed && !negativePressed && Mathf.Abs(current) < snapThreshold)
			current = 0f;

		return current;
	}

	/// <summary>
	/// 速度がlimitを超えていたら減速し、近ければスナップする
	/// </summary>
	float ClampToMax(float current, float limit, bool pressed, float delta, float snapThreshold)
	{
		bool overLimit = limit > 0f ? current > limit : current < limit;
		if (!pressed || !overLimit) return current;

		current += delta * deceleration * (limit > 0f ? -1f : 1f);

		if (Mathf.Abs(current - limit) < snapThreshold)
			current = limit;

		return current;
	}
}