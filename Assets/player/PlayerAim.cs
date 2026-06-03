using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;

public class PlayerAim : MonoBehaviour
{
	[Header("カメラ設定")]
	[SerializeField] private CinemachineFreeLook aimCamera;
	[SerializeField] private CinemachineFreeLook freeLookCamera;

	[Header("エイム設定")]
	[SerializeField] private LayerMask aimTargetLayer;

	[Header("優先度設定")]
	[SerializeField] private int aimCameraPriority = 20;
	[SerializeField] private int defaultCameraPriority = 10;

	public bool IsAiming { get; private set; }
	public Vector3 AimPoint { get; private set; }

	private Camera mainCamera;
	private PlayerInput playerInput;
	private InputAction aimAction;

	void Awake()
	{
		mainCamera = Camera.main;
		playerInput = GetComponent<PlayerInput>();

		// アクションを直接取得してstarted/canceledを使う
		aimAction = playerInput.actions["Aim"];
		aimAction.started += _ => SetAim(true);
		aimAction.canceled += _ => SetAim(false);
	}

	void OnDestroy()
	{
		aimAction.started -= _ => SetAim(true);
		aimAction.canceled -= _ => SetAim(false);
	}

	private void SetAim(bool aiming)
	{
		IsAiming = aiming;
		SwitchCamera(aiming);
	}

	private void SwitchCamera(bool aiming)
	{
		aimCamera.Priority = aiming ? aimCameraPriority : defaultCameraPriority;
		freeLookCamera.Priority = aiming ? defaultCameraPriority : aimCameraPriority;
	}

	void Update()
	{
		if (IsAiming)
		{
			UpdateAimPoint();
		}
		Debug.Log("Bool IsAiming: " + IsAiming);
	}

	private void UpdateAimPoint()
	{
		Ray ray = mainCamera.ScreenPointToRay(
			new Vector3(Screen.width / 2f, Screen.height / 2f, 0f)
		);

		if (Physics.Raycast(ray, out RaycastHit hit, 100f, aimTargetLayer))
		{
			AimPoint = hit.point;
		}
		else
		{
			AimPoint = ray.GetPoint(100f);
		}
	}
}