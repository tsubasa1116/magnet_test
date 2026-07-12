using System.Collections;
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

    [Header("UI")]
    [SerializeField] private GameObject NMugUI;
    [SerializeField] private GameObject SMugUI;
    [SerializeField] private GameObject ModeButton;
    [SerializeField] private GameObject onMugUI;
    [SerializeField] private GameObject offMugUI;

    private PlayerCatch catchState;

    [Header("アニメーション")]
    [SerializeField] private float buttonZoomScale = 1.2f;
    [SerializeField] private float buttonAnimSec = 0.15f;
    [SerializeField] private float uiRotateSec = 0.3f;

    private bool isChangeMode = false;

    void Awake()
    {
        catchState = GetComponent<PlayerCatch>();
    }

    void Update()
    {
        UpdateUI();
    }

    void UpdateUI()
    {
        if (NMugUI != null)
            NMugUI.SetActive(CurrentState == MagnetState.N);

        if (SMugUI != null)
            SMugUI.SetActive(CurrentState == MagnetState.S);

        bool isActive = catchState != null && catchState.IsCatching;

        if (onMugUI != null)
            onMugUI.SetActive(isActive);

        if (offMugUI != null)
            offMugUI.SetActive(!isActive);
    }

    // PlayerInputから自動で呼ばれる（Invertアクション = ZL / Q で極を切り替え）
    public void OnInvert(InputValue value)
    {
        if (value.isPressed && !isChangeMode)
        {
            SwitchState();
        }
    }

    private void SwitchState()
    {
        CurrentState = CurrentState == MagnetState.N ? MagnetState.S : MagnetState.N;

        StartCoroutine(AnimateButtonPress());
        StartCoroutine(RotateUI());

        OnStateChanged?.Invoke(CurrentState);

        Debug.Log($"磁石モード切り替え: {CurrentState}");
    }


    private IEnumerator AnimateButtonPress()
    {
        if (ModeButton == null)
            yield break;

        Vector3 initScale = ModeButton.transform.localScale;
        Vector3 targetScale = initScale * buttonZoomScale;
        float halfSec = buttonAnimSec / 2f;
        float time = 0f;

        while (time < halfSec)
        {
            time += Time.deltaTime;
            float rate = time / halfSec;
            ModeButton.transform.localScale = Vector3.Lerp(initScale, targetScale, rate);
            yield return null;
        }

        time = 0f;

        while (time < halfSec)
        {
            time += Time.deltaTime;
            float rate = time / halfSec;
            ModeButton.transform.localScale = Vector3.Lerp(targetScale, initScale, rate);
            yield return null;
        }

        ModeButton.transform.localScale = initScale;
    }

    private IEnumerator RotateUI()
    {
        isChangeMode = true;

        GameObject currentUI = CurrentState == MagnetState.N ? NMugUI : SMugUI;

        if (currentUI != null)
        {
            float time = 0f;
            Vector3 start = currentUI.transform.localEulerAngles;

            while (time < uiRotateSec)
            {
                time += Time.deltaTime;
                float rate = time / uiRotateSec;
                float angle = Mathf.Lerp(0f, 360f, rate);

                currentUI.transform.localEulerAngles =
                    new Vector3(start.x, start.y, start.z - angle);

                yield return null;
            }

            currentUI.transform.localEulerAngles = start;
        }

        UpdateUI();

        isChangeMode = false;
    }
}