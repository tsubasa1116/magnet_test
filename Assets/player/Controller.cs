using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Controller : MonoBehaviour
{
	//Animator animator;
	Quaternion targetRotation;
	Rigidbody rb;
	float jumpForce = 10;
	public bool isJumping;

	// ★追加：プレイヤーの体力
	public int hp = 3;

	// ★追加：ジャンプ判定用のスフィアコライダーを登録する変数
	[SerializeField] SphereCollider jumpCollider;

	// Start is called before the first frame update
	void Start()
	{
		// コンポーネント関連付け
		//TryGetComponent(out animator);

		// 初期化
		rb = GetComponent<Rigidbody>();
		isJumping = false;
		targetRotation = transform.rotation;
	}
	// Update is called once per frame
	void Update()
	{
		// var : 型推論を行うキーワード C++のautoに相当

		// 入力ベクトルの取得
		var horizontal = Input.GetAxis("Horizontal");
		var vertical = Input.GetAxis("Vertical");

		// カメラの水平回転を取得
		var horizontalRotation = Quaternion.AngleAxis(Camera.main.transform.eulerAngles.y, Vector3.up);

		// 入力ベクトルの正規化
		var velocity = horizontalRotation * new Vector3(horizontal, 0, vertical).normalized;

		// 走るかどうかの判定
		var speed = Input.GetKey(KeyCode.LeftShift) ? 10 : 7;

		// 回転速度
		var rotationSpeed = 600 * Time.deltaTime;

		// 移動方向を向く
		if (velocity.magnitude > 0.5f)
		{
			targetRotation = Quaternion.LookRotation(velocity, Vector3.up);
		}
		transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed);

		// 移動速度をアニメーションに反映
		//transform.position += velocity * speed * Time.deltaTime;
		//animator.SetFloat("Speed", velocity.magnitude * speed, 0.1f, Time.deltaTime);

		Vector3 nextPosition = rb.position + velocity * speed * Time.deltaTime;
		rb.MovePosition(nextPosition);

		// ジャンプ
		if (Input.GetKeyDown(KeyCode.Space) && !isJumping)
		{
			rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
			isJumping = true;
		}
	}

	private void OnTriggerEnter(Collider other)
	{
		// トリガー内に何かが入ったとき、それが地面（床）なら
		// 床に "Ground" などのタグをつけておくとより正確
		isJumping = false;
	}

	// ★追加：ダメージを受ける関数
	public void TakeDamage(int damage)
	{
		hp -= damage;
		Debug.Log("プレイヤーがダメージを受けた！ 残りHP: " + hp);

		if (hp <= 0)
		{
			Die();
		}
	}

	// ★追加：死亡処理
	private void Die()
	{
		Debug.Log("プレイヤーがやられた！");
		// プレイヤーを消す、またはゲームオーバーの処理などをここに書く
		// 例: Destroy(gameObject);
	}
}