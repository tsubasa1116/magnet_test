using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class Mission: MonoBehaviour
{
    [SerializeField] private Sprite[] missionSprites;
    [SerializeField] private Image[] missionImages;

    private GameObject[] checkMarks;

    void Start()
    {
        InitializeCheckMarks();
        ClearAllMissions();
    }

    private void InitializeCheckMarks()
    {
        if (missionImages == null) return;

        checkMarks = new GameObject[missionImages.Length];

        for (int i = 0; i < missionImages.Length; i++)
        {
            if (missionImages[i] == null) continue;

            // 各MissionImageの「一番最初の子オブジェクト」をチェックマークとして自動取得
            if (missionImages[i].transform.childCount > 0)
            {
                checkMarks[i] = missionImages[i].transform.GetChild(0).gameObject;
            }
        }
    }

    public void ClearAllMissions()
    {
        for (int i = 0; i < missionImages.Length; i++)
        {
            if (missionImages[i] != null)
            {
                missionImages[i].gameObject.SetActive(false);
                missionImages[i].sprite = null;
                missionImages[i].enabled = false; // 非表示にする
            }

            if (checkMarks != null && i < checkMarks.Length && checkMarks[i] != null)
            {
                checkMarks[i].SetActive(false);
            }
        }
    }

    /// <summary>
    /// 1＝2Fへ移動 2＝バリア 3＝ボス 4＝ドア 5＝敵 6＝部屋移動
    /// </summary>
    public void SetMission(int spriteIndex)
    {
        if (missionSprites == null || spriteIndex < 0 || spriteIndex >= missionSprites.Length) return;

        Sprite targetSprite = missionSprites[spriteIndex];
        if (targetSprite == null) return;

        for (int i = 0; i < missionImages.Length; i++)
        {
            if (missionImages[i] == null) continue;

            if (!missionImages[i].enabled)
            {
                missionImages[i].gameObject.SetActive(true);
                missionImages[i].sprite = targetSprite;
                missionImages[i].enabled = true;

                missionImages[i].SetNativeSize();

                return;
            }
        }
    }

    public void ClearMission(int spriteIndex)
    {
        // 1. 指定されたIDの画像を取得
        if (missionSprites == null || spriteIndex < 0 || spriteIndex >= missionSprites.Length) return;
        Sprite targetSprite = missionSprites[spriteIndex];
        if (targetSprite == null) return;

        // 2. 現在表示されているImage（0〜2）の中から、その画像を持っているスロットを検索する
        for (int i = 0; i < missionImages.Length; i++)
        {
            if (missionImages[i] == null) continue;

            // スロットに入っている画像が、クリア対象の画像と一致したら
            if (missionImages[i].sprite == targetSprite)
            {
                // そのスロット番号（i）を使ってクリア演出＆詰め処理を実行！
                StartCoroutine(CompleteMissionRoutine(i));
                return; // 見つかったら処理終了
            }
        }

        // ここに到達した場合は、そもそもそのミッションがまだ画面に出ていない状態
        Debug.LogWarning($"ミッションID {spriteIndex} は現在表示されていません。");
    }

    // コルーチン側は「スロット番号」で処理するので、以前のままでOKです！
    private IEnumerator CompleteMissionRoutine(int slotIndex)
    {
        // 1. 対応する位置のチェックマークを表示する
        if (checkMarks != null && slotIndex < checkMarks.Length && checkMarks[slotIndex] != null)
        {
            checkMarks[slotIndex].SetActive(true);
        }

        // 2. 指定した秒数だけ待つ
        yield return new WaitForSeconds(2.0f);

        // チェックマークを消す
        if (checkMarks != null && slotIndex < checkMarks.Length && checkMarks[slotIndex] != null)
        {
            checkMarks[slotIndex].SetActive(false);
        }

        // 3. 残す必要のあるミッションのSpriteを一時的にリストに集める
        List<Sprite> remainingMissions = new List<Sprite>();
        for (int i = 0; i < missionImages.Length; i++)
        {
            if (i != slotIndex && missionImages[i].sprite != null)
            {
                remainingMissions.Add(missionImages[i].sprite);
            }
        }

        // 4. 一旦すべてのUIをリセット
        ClearAllMissions();

        // 5. 残ったミッションを上から順に詰め直して再表示する
        for (int i = 0; i < remainingMissions.Count; i++)
        {
            if (i < missionImages.Length && missionImages[i] != null)
            {
                missionImages[i].gameObject.SetActive(true);
                missionImages[i].sprite = remainingMissions[i];
                missionImages[i].enabled = true;
                missionImages[i].SetNativeSize();
            }
        }
    }
}
