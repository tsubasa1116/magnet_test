using System.Collections;
using UnityEngine;

public class Nyuxtu3 : MonoBehaviour
{
    public static Nyuxtu3 Instance { get; private set; }

    [SerializeField] private RectTransform rightSide;
    [SerializeField] private RectTransform leftSide;
    [SerializeField] private RectTransform downSide;
    [SerializeField] private CanvasGroup fadeCanvasGroup;

    [SerializeField] public Vector2[] startPos; // 画面外
    [SerializeField] public Vector2[] newPos;   // 画面内

    [SerializeField] private float duration = 0.5f;

    private Coroutine rightRoutine, leftRoutine, downRoutine, fadeRoutine;

    [SerializeField] private FollowUI leftFollowUI; // Nyuxtu3側に追加

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        rightSide.anchoredPosition = startPos[0];
        leftSide.anchoredPosition = startPos[1];
        downSide.anchoredPosition = startPos[2];

        if (fadeCanvasGroup != null)
            fadeCanvasGroup.alpha = 0f;
    }

    /// <summary>
    /// 画面内へスライドイン+フェードインさせる(Nyuxtu3.Instance.ShowUI())
    /// </summary>
    public void ShowUI()
    {
        //if (leftFollowUI != null) leftFollowUI.isPaused = false;
        StopAllRoutines();

        rightRoutine = StartCoroutine(MoveRoutine(rightSide, rightSide.anchoredPosition, newPos[0], duration));
        leftRoutine = StartCoroutine(MoveRoutine(leftSide, leftSide.anchoredPosition, newPos[1], duration));
        downRoutine = StartCoroutine(MoveRoutine(downSide, downSide.anchoredPosition, newPos[2], duration));

        if (fadeCanvasGroup != null)
            fadeRoutine = StartCoroutine(FadeRoutine(fadeCanvasGroup, fadeCanvasGroup.alpha, 1f, duration));

        //if (leftFollowUI != null) leftFollowUI.enabled = true;
        if(leftFollowUI != null)
        StartCoroutine(ResumeFollowAfterDelay(leftFollowUI, duration));
    }

    private IEnumerator ResumeFollowAfterDelay(FollowUI followUI, float delay)
    {
        yield return new WaitForSeconds(delay);
        followUI.ResumeFollow(); // 後述
    }

    /// <summary>
    /// 画面外へスライドアウト+フェードアウトさせる(Nyuxtu3.Instance.HideUI())
    /// </summary>
    public void HideUI()
    {
        if (leftFollowUI != null) leftFollowUI.isPaused = true;
        StopAllRoutines();

        rightRoutine = StartCoroutine(MoveRoutine(rightSide, rightSide.anchoredPosition, startPos[0], duration));
        leftRoutine = StartCoroutine(MoveRoutine(leftSide, leftSide.anchoredPosition, startPos[1], duration));
        downRoutine = StartCoroutine(MoveRoutine(downSide, downSide.anchoredPosition, startPos[2], duration));

        if (fadeCanvasGroup != null)
            fadeRoutine = StartCoroutine(FadeRoutine(fadeCanvasGroup, fadeCanvasGroup.alpha, 0f, duration));
        //if (leftFollowUI != null) leftFollowUI.enabled = false;
       
    }

    /// <summary>
    /// アニメ無しで即座に画面外へ置く(ゲーム開始時など、最初から隠しておきたい時用)。
    /// HideUI()だと画面内→画面外のスライドが一瞬見えてしまう。
    /// </summary>
    public void HideUIImmediate()
    {
        if (leftFollowUI != null) leftFollowUI.isPaused = true;
        StopAllRoutines();

        rightSide.anchoredPosition = startPos[0];
        leftSide.anchoredPosition = startPos[1];
        downSide.anchoredPosition = startPos[2];

        if (fadeCanvasGroup != null)
            fadeCanvasGroup.alpha = 0f;
    }

    private void StopAllRoutines()
    {
        if (rightRoutine != null) StopCoroutine(rightRoutine);
        if (leftRoutine != null) StopCoroutine(leftRoutine);
        if (downRoutine != null) StopCoroutine(downRoutine);
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
    }

    private IEnumerator MoveRoutine(RectTransform target, Vector2 from, Vector2 to, float time)
    {
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / time);
            float eased = EaseOutCubic(normalized); // ぬるっと減速しながら止まる動き
            target.anchoredPosition = Vector2.LerpUnclamped(from, to, eased);
            yield return null;
        }
        target.anchoredPosition = to;
    }

    private IEnumerator FadeRoutine(CanvasGroup cg, float from, float to, float time)
    {
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / time);
            cg.alpha = Mathf.Lerp(from, to, normalized);
            yield return null;
        }
        cg.alpha = to;
    }

    private float EaseOutCubic(float x)
    {
        return 1f - Mathf.Pow(1f - x, 3f);
    }
}