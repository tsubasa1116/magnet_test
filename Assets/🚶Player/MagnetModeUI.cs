using UnityEngine;

// 磁石モードのUI表示。極(N/S)と Catch(ZRホールド=磁力ON/OFF) を画面に反映する。
//   PlayerStateMachine.OnStateChanged を購読して N/S 表示を切替
//   PlayerCatch.IsCatching を毎フレーム見て ON/OFF 表示を切替
[RequireComponent(typeof(PlayerStateMachine))]
public class MagnetModeUI : MonoBehaviour
{
	[Header("極の表示")]
	[SerializeField] private GameObject nPoleUI;
	[SerializeField] private GameObject sPoleUI;

	[Header("磁力 ON/OFF の表示(ZR=Catch)")]
	[SerializeField] private GameObject onUI;
	[SerializeField] private GameObject offUI;

	private PlayerStateMachine stateMachine;
	private PlayerCatch catchState;

	void Awake()
	{
		stateMachine = GetComponent<PlayerStateMachine>();
		catchState = GetComponent<PlayerCatch>();
	}

	void OnEnable()
	{
		if (stateMachine != null) stateMachine.OnStateChanged += ApplyPole;
	}

	void OnDisable()
	{
		if (stateMachine != null) stateMachine.OnStateChanged -= ApplyPole;
	}

	void Start()
	{
		if (stateMachine != null) ApplyPole(stateMachine.CurrentState);
	}

	void Update()
	{
		bool on = catchState != null && catchState.IsCatching;
		if (onUI != null) onUI.SetActive(on);
		if (offUI != null) offUI.SetActive(!on);
	}

	private void ApplyPole(MagnetState s)
	{
		if (nPoleUI != null) nPoleUI.SetActive(s == MagnetState.N);
		if (sPoleUI != null) sPoleUI.SetActive(s == MagnetState.S);
	}
}
