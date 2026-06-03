using UnityEngine;
using UnityEngine.InputSystem;

public enum MagnetState
{
	N,
	S
}

public class PlayerStateMachine : MonoBehaviour
{
	public MagnetState CurrentState { get; private set; } = MagnetState.N;

	// 状態が変わったとき他スクリプトに通知するイベント
	public event System.Action<MagnetState> OnStateChanged;

	// PlayerInputから自動で呼ばれる
	public void OnMagnetONOFF(InputValue value)
	{
		if (value.isPressed)
		{
			SwitchState();
		}
	}

	private void SwitchState()
	{
		CurrentState = CurrentState == MagnetState.N ? MagnetState.S : MagnetState.N;

		OnStateChanged?.Invoke(CurrentState);

		Debug.Log($"磁石モード切り替え: {CurrentState}");
	}
}