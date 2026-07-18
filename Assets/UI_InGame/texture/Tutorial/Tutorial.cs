using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class Tutorial : MonoBehaviour
{
    [SerializeField] private Sprite[] tutorialSprites;
    [SerializeField] private Image tutorialImage;

    private GameObject[] checkMarks;

    void Start()
    {
        ResetMission();
    }

    public void ResetMission()
    {
        if (tutorialImage != null)
        {
            tutorialImage.gameObject.SetActive(false);
            tutorialImage.sprite = null;
            tutorialImage.enabled = false;
        }
    }

    /// <summary>
    /// 1＝磁力 2＝極 3＝ボタン 4＝ジャンプ 5＝移動
    /// </summary>
    public void SetTutorial(int spriteIndex)
    {
        if (tutorialSprites == null || spriteIndex < 0 || spriteIndex >= tutorialSprites.Length) return;

        Sprite targetSprite = tutorialSprites[spriteIndex];
        if (targetSprite == null) return;

        if (!tutorialImage.enabled)
        {
            tutorialImage.gameObject.SetActive(true);
            tutorialImage.sprite = targetSprite;
            tutorialImage.enabled = true;

            tutorialImage.SetNativeSize();

            return;
        }
        
    }

    /// <summary>
    /// 1＝磁力 2＝極 3＝ボタン 4＝ジャンプ 5＝移動
    /// </summary>
    public void ClearTutorial(int spriteIndex)
    {
        if (tutorialSprites == null || spriteIndex < 0 || spriteIndex >= tutorialSprites.Length) return;
        Sprite targetSprite = tutorialSprites[spriteIndex];
        if (targetSprite == null) return;

        if (tutorialImage.sprite == targetSprite)
        {
            StartCoroutine(CompleteTutorialRoutine());
            return; // 見つかったら処理終了
        }

    }

    private IEnumerator CompleteTutorialRoutine()
    { 
        yield return new WaitForSeconds(1.0f);
        // チュートリアルを非表示にする
        ResetMission();
    }
}
