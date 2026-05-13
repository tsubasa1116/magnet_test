using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class magnet : MonoBehaviour
{
	public float magnetStrength = 10f;  // 引き寄せる強さ
	public float range = 300f;           // 磁石が届く範囲

	public float throwForce = 50f; // ★追加：放り投げる（反発）強さ

	// 磁石のモード 0:無し 1:N極 2:S極
	public int magnetMode = 1;
	public bool isActive = false;

	public GameObject NMugUI;
	public GameObject SMugUI;
	public GameObject onMugUI;
	public GameObject offMugUI;

	private Rigidbody targetRb;
	private bool isAttached = false;    // くっついているか

	// Start is called before the first frame update
	void Start()
	{
		UpdateUI();
	}

	// Update is called once per frame
	void Update()
	{
		// キー操作でモード切替（直接数字を変えるのではなく、専用関数を呼ぶ！）
		if (Input.GetKeyDown(KeyCode.Q))
		{
			if (magnetMode == 1) ChangeMode(2);
			else ChangeMode(1);
		}
		if (Input.GetKeyDown(KeyCode.E))
		{
			if (isActive) isActive = false;
			else isActive = true;
			UpdateUI(); // オンオフを変えたらUIの表示を更新する

			// オフにした時は持っている物を離す
			if (!isActive)
			{
				ReleaseTarget();
			}
			else
			{
				//オンにした時に既に違う極の物を持っていたら弾き飛ばす
				CheckAndLaunchTarget();
			}
		}

		// 磁石のモードに合わせて引き寄せる
		AttractObjects();
	}

	// ★追加：モードを切り替えるときの専用関数
	void ChangeMode(int newMode)
	{
		magnetMode = newMode;

		UpdateUI();

		if (isActive)
		{
			CheckAndLaunchTarget();
		}
	}

	void CheckAndLaunchTarget()
	{
		// もし何か持っている状態で、モードが「反発」に変わったら吹き飛ばす！
		if (targetRb != null && isAttached)
		{
			bool isS = targetRb.CompareTag("S_Pole");
			bool isN = targetRb.CompareTag("N_Pole");

			// NモードでNを、SモードでSを持っていたら反発！
			if ((magnetMode == 1 && isN && isActive) || (magnetMode == 2 && isS && isActive))
			{
				LaunchTarget();
			}
		}
	}

	void UpdateUI()
	{
		if (NMugUI != null) NMugUI.SetActive(magnetMode == 1);
		if (SMugUI != null) SMugUI.SetActive(magnetMode == 2);
		if (onMugUI != null) onMugUI.SetActive(!isActive);
		if (offMugUI != null) offMugUI.SetActive(isActive);
	}

	// ★追加：勢いよく吹き飛ばす（発射）処理
	void LaunchTarget()
	{
		if (targetRb != null)
		{
			targetRb.transform.SetParent(null);
			targetRb.isKinematic = false;

			Vector3 shootDirection = Camera.main.transform.forward;
			targetRb.AddForce(shootDirection * throwForce, ForceMode.Impulse);

			// 追加：離した扱い
			AuraRing aura = targetRb.GetComponentInChildren<AuraRing>();
			if (aura != null)
			{
				aura.SetHeld(false);
			}

			Bomb bomb = targetRb.GetComponent<Bomb>();
			if (bomb != null)
			{
				bomb.isThrown = true;
			}
		}

		targetRb = null;
		isAttached = false;
	}

	// ロックオンを解除する処理（モードを変えた時などに呼ぶ）
	void ReleaseTarget()
	{
		if (targetRb != null)
		{
			targetRb.transform.SetParent(null);
			targetRb.isKinematic = false;

			// 追加：オーラに「離した」と伝える
			AuraRing aura = targetRb.GetComponentInChildren<AuraRing>();
			if (aura != null)
			{
				aura.SetHeld(false);
			}
		}
		targetRb = null;
		isAttached = false;
	}

	void AttractObjects()
	{
		if (!isActive || magnetMode == 0) return;

		// 誰も持っていないときだけ探す
		if (targetRb == null)
		{
			Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
			RaycastHit hit;
			if (Physics.Raycast(ray, out hit, range))
			{
				bool isS = hit.collider.CompareTag("S_Pole");
				bool isN = hit.collider.CompareTag("N_Pole");

				// 違う極のときだけ引き寄せ開始
				if ((magnetMode == 1 && isS) || (magnetMode == 2 && isN))
				{
					targetRb = hit.collider.GetComponent<Rigidbody>();
				}
			}
		}

		if (targetRb != null && !isAttached)
		{
			float distance = Vector3.Distance(transform.position, targetRb.position);
			Vector3 direction = transform.position - targetRb.position;
			targetRb.AddForce(direction.normalized * magnetStrength);

			if (distance < 2f)
			{
				AttachTarget();
			}
		}

		// ★修正： targetRb が null でない場合のみ GetComponent を実行する
		if (targetRb != null)
		{
			Bomb bomb = targetRb.GetComponent<Bomb>();
			if (bomb != null)
			{
				bomb.isThrown = false;
			}
		}
	}

	void AttachTarget()
	{
		isAttached = true;

		targetRb.transform.SetParent(this.transform);
		targetRb.isKinematic = true;
		targetRb.transform.localPosition = new Vector3(0, 0, 1.5f);

		// 追加：オーラに「持った」と伝える
		AuraRing aura = targetRb.GetComponentInChildren<AuraRing>();
		if (aura != null)
		{
			aura.SetHeld(true);
		}
	}
}