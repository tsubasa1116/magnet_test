using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; 

public class magnet : MonoBehaviour
{
	public float magnetStrength = 10f;  // 引き寄せる強さ
	public float range = 300f;           // 磁石が届く範囲

	public float throwForce = 50f; // ★追加：放り投げる（反発）強さ

	// 磁石のモード 0:無し 1:N極 2:S極
	public int magnetMode = 1;
	public bool isActive = false;

    public float uiRotateSec = 0.3f; // UIが回転するのにかかる時間
    private bool isChangeMode = false; // モードが切り替わっている途中かどうか「

	public float buttonAnimSec = 0.2f; // ボタンが押されてから元に戻るまでの時間
	public float buttonZoomScale = 1.2f; // ボタンが押されたときにどれくらい大きくなるか

    public GameObject NMugUI;
	public GameObject SMugUI;
	public GameObject ModeButton;
	public GameObject onMugUI;
	public GameObject offMugUI;

	private Rigidbody targetRb;
	private bool isAttached = false;    // くっついているか

	// ★追加: Input Systemのコントローラー
	private PlayerControls controls;

    // Start is called before the first frame update
    void Start()
	{
		UpdateUI();
	}

	// ★追加: Input Systemの初期化とイベント登録
	void Awake()
	{
		controls = new PlayerControls();

		// Magnet ON OFFボタンが押されたとき（ON）
		controls.Player.MagnetONOFF.started += ctx => StartMagnet();
		// Magnet ON OFFボタンが離されたとき（OFF）
		controls.Player.MagnetONOFF.canceled += ctx => StopMagnet();
	}

	void OnEnable()
	{
		controls.Enable();
	}

	void OnDisable()
	{
		controls.Disable();
	}

	// ★追加: 磁石ONの処理
	private void StartMagnet()
	{
		if (!isActive)
		{
			isActive = true;
			UpdateUI();
			
			//オンにした時に既に違う極の物を持っていたら弾き飛ばす
			CheckAndLaunchTarget();
		}
	}

	// ★追加: 磁石OFFの処理
	private void StopMagnet()
	{
		if (isActive)
		{
			isActive = false;
			UpdateUI();
			
			// オフにした時は持っている物を離す
			ReleaseTarget();
		}
	}


	// Update is called once per frame
	void Update()
	{
		// キー操作でモード切替（直接数字を変えるのではなく、専用関数を呼ぶ！）
		if (Input.GetKeyDown(KeyCode.Mouse1))
		{
			if (magnetMode == 1) ChangeMode(2);
			else ChangeMode(1);
		}

		// ↑古いInput.GetKeyDownやInput.GetKeyUpの処理は削除して、
		// Input Systemのイベント（StartMagnet, StopMagnet）に任せます。

		// 磁石のモードに合わせて引き寄せる
		AttractObjects();
	}

	// ★追加：モードを切り替えるときの専用関数
	public void ChangeMode(int newMode)
	{
		if(isChangeMode) return; // すでに切り替え中なら何もしない

		if(ModeButton != null)
		{
			StartCoroutine(AnimateButtonPress());
        }
        StartCoroutine(RotateUI(newMode)); // UI回転開始
    }
	private IEnumerator AnimateButtonPress()
	{
		Vector3 initScale = ModeButton.transform.localScale;
		Vector3 targetScale = initScale * buttonZoomScale;
		float halfSec = buttonAnimSec / 2f;
        float time = 0.0f;

		while (time < halfSec)
		{
			time += Time.deltaTime;
			float rate = time / halfSec;
			ModeButton.transform.localScale = Vector3.Lerp(initScale, targetScale, rate);
			yield return null;
		}
		time = 0.0f;

		while (time < halfSec)
		{
			time += Time.deltaTime;
			float rate = time / halfSec;
			ModeButton.transform.localScale = Vector3.Lerp(targetScale, initScale, rate);
			yield return null;
		}
		ModeButton.transform.localScale = initScale;
    }

    private IEnumerator RotateUI(int newMode)
	{
		isChangeMode = true;

		GameObject currentUI = magnetMode == 1 ? NMugUI : SMugUI;

		if (currentUI != null)
		{
			float time = 0.0f;
			Vector3 start = currentUI.transform.localEulerAngles;

			while (time < uiRotateSec)
			{
				time += Time.deltaTime;
				float rate = time / uiRotateSec;
				float angle = Mathf.Lerp(0f, 360f, rate);
				currentUI.transform.localEulerAngles = new Vector3(start.x, start.y, start.z - angle);
				yield return null;
            }
			currentUI.transform.localEulerAngles = start;
        }
		magnetMode = newMode;
		UpdateUI();
		CheckAndLaunchTarget();
		isChangeMode = false;
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
		if (onMugUI != null) onMugUI.SetActive(isActive);
		if (offMugUI != null) offMugUI.SetActive(!isActive);
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
					targetRb = hit.collider.GetComponentInParent<Rigidbody>();
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