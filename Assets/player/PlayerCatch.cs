using UnityEngine;
using UnityEngine.InputSystem;

// Catch状態の一元管理。ZR(Magnet ON OFF アクション)を押している間 Catch状態。
// 他のスクリプト(移動の向き・アニメ・引き寄せ・Aim)はここを参照する。
[RequireComponent(typeof(PlayerInput))]
public class PlayerCatch : MonoBehaviour
{
	private InputAction catchAction;

	public bool IsCatching { get; private set; }

	void Awake()
	{
		catchAction = GetComponent<PlayerInput>().actions["Magnet ON OFF"]; // ZR
	}

	void Update()
	{
		IsCatching = catchAction != null && catchAction.IsPressed();
	}
}
