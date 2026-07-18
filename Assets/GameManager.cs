using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public float ElapsedTime { get; private set; }
    public int TotalKillCount { get; private set; }

    private bool isPlaying = false;

    private void Start()
    {
        StartGame();
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (isPlaying)
        {
            ElapsedTime += Time.deltaTime;
        }
    }

    public void StartGame()
    {
        ElapsedTime = 0f;
        TotalKillCount = 0;
        isPlaying = true;
    }

    public void EndGame()
    {
        isPlaying = false;
    }

    public void AddKill()
    {
        TotalKillCount++;
    }

    public void ResetGame()
    {
        ElapsedTime = 0f;
        TotalKillCount = 0;
        isPlaying = false;
    }

    // デバッグ表示
    private void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 24;
        style.normal.textColor = Color.white;

        GUI.Label(
            new Rect(20, 20, 400, 30),
            $"Time : {ElapsedTime:F1} sec",
            style);

        GUI.Label(
            new Rect(20, 50, 400, 30),
            $"Kills : {TotalKillCount}",
            style);
    }
}