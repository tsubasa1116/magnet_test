using UnityEngine;

public class pauseManager : MonoBehaviour
{
    [Header("ポーズUI")]
    [SerializeField] private GameObject pauseUI;

    private bool isPause = false;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPause)
            {
                ResumeGame(); // 再開
            }
            else
            {
                PauseGame(); // 一時停止
            }
        }
    }

    // 一時停止
    public void PauseGame()
    {
        pauseUI.SetActive(true); // ポーズUI表示
        Time.timeScale = 0f;     // ゲーム内の時間を止める

        isPause = true;
    }

    // 再開
    public void ResumeGame()
    {
        pauseUI.SetActive(false); // ポーズUI非表示
        Time.timeScale = 1f;      // ゲーム内の時間を戻す

        isPause = false;
    }
}