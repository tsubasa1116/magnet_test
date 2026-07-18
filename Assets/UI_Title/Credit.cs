using UnityEngine;
using System.Collections;

public class Credit : MonoBehaviour
{
    [SerializeField] public GameObject creditUI;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(CreditAnimationSequence());
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private IEnumerator CreditAnimationSequence()
    {
        yield return new WaitForSeconds(1.0f);

        // タイトルロゴをフェードで表示
        yield return StartCoroutine(FadeIn(creditUI, 2.0f));
        yield return new WaitForSeconds(0.5f);

        SceneLoad.LoadDirect("TitleScene", FadeType.Black);
    }

    private IEnumerator FadeIn(GameObject target, float duration)
    {
        CanvasGroup cg = target.GetComponent<CanvasGroup>();
        if (cg == null) cg = target.AddComponent<CanvasGroup>();

        float elapsed = 0.0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }
        cg.alpha = 1.0f;
    }
}
