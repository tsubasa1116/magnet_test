using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;

// エイム(ADS)制御。
// カメラは1台の FreeLook のまま、エイム中だけ FOV / 距離 / 肩寄せを補間する。
// カメラを切り替えないので、エイムに入っても "今見ている方向" をそのまま維持できる
// （別カメラへ切り替わる時のような向きのスナップが起きない）。
[RequireComponent(typeof(PlayerInput))]
public class PlayerAim : MonoBehaviour
{
	[Header("対象カメラ(通常時のFreeLookを割り当てる)")]
	[SerializeField] private CinemachineFreeLook freeLook;

	[Header("エイム設定")]
	[SerializeField] private float aimFOV = 28f;           // エイム時のFOV(小さいほどアップ)
	[SerializeField] private float aimRadiusScale = 0.6f;  // エイム時の距離倍率(小さいほど接近)
	[SerializeField] private float aimScreenX = 0.35f;     // エイム時の肩寄せ(0.5=中央, 小さいほど右肩越し)
	[SerializeField] private float lerpSpeed = 10f;        // 寄り/戻りのなめらかさ
	[SerializeField] private LayerMask aimTargetLayer;

	public bool IsAiming { get; private set; }
	public Vector3 AimPoint { get; private set; }

	private Camera mainCamera;
	private PlayerInput playerInput;
	private InputAction aimAction;

	// 通常時の値(復帰用)
	private float normalFOV;
	private float[] normalRadii = new float[3];
	private float normalScreenX = 0.5f;
	private CinemachineComposer[] composers = new CinemachineComposer[3];

	void Awake()
	{
		mainCamera = Camera.main;
		playerInput = GetComponent<PlayerInput>();
		aimAction = playerInput.actions["Aim"];
		// 押し込み一回でAim ON/OFFをトグル（押しっぱなし不要）
		aimAction.started += OnAimToggle;
	}

	void Start()
	{
		// 通常時の見た目を控えておく
		normalFOV = freeLook.m_Lens.FieldOfView;
		for (int i = 0; i < 3; i++)
		{
			normalRadii[i] = freeLook.m_Orbits[i].m_Radius;
			composers[i] = freeLook.GetRig(i).GetCinemachineComponent<CinemachineComposer>();
		}
		if (composers[1] != null) normalScreenX = composers[1].m_ScreenX;
	}

	void OnDestroy()
	{
		if (aimAction != null)
		{
			aimAction.started -= OnAimToggle;
		}
	}

	private void OnAimToggle(InputAction.CallbackContext _) => IsAiming = !IsAiming;

	void Update()
	{
		float t = lerpSpeed * Time.deltaTime;

		// FOV(ズーム)
		float targetFOV = IsAiming ? aimFOV : normalFOV;
		freeLook.m_Lens.FieldOfView = Mathf.Lerp(freeLook.m_Lens.FieldOfView, targetFOV, t);

		// 距離(各リグの半径)と肩寄せ
		for (int i = 0; i < 3; i++)
		{
			float targetRadius = normalRadii[i] * (IsAiming ? aimRadiusScale : 1f);
			freeLook.m_Orbits[i].m_Radius = Mathf.Lerp(freeLook.m_Orbits[i].m_Radius, targetRadius, t);

			if (composers[i] != null)
			{
				float targetX = IsAiming ? aimScreenX : normalScreenX;
				composers[i].m_ScreenX = Mathf.Lerp(composers[i].m_ScreenX, targetX, t);
			}
		}

		if (IsAiming) UpdateAimPoint();
	}

	private void UpdateAimPoint()
	{
		Ray ray = mainCamera.ScreenPointToRay(
			new Vector3(Screen.width / 2f, Screen.height / 2f, 0f)
		);
		AimPoint = Physics.Raycast(ray, out RaycastHit hit, 100f, aimTargetLayer)
			? hit.point
			: ray.GetPoint(100f);
	}
}
