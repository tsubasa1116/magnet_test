using UnityEngine;

public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance;

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip normalBGM;
    [SerializeField] private AudioClip battleBGM;

    private int enemyCount = 0;
    private bool isBattle = false;

    private void Awake()
    {
        Instance = this;

        audioSource.clip = normalBGM;
        audioSource.loop = true;
        audioSource.Play();
    }

    public void EnemyFound()
    {
        enemyCount++;

        if (!isBattle)
        {
            isBattle = true;
            audioSource.clip = battleBGM;
            audioSource.Play();
        }
    }

    public void EnemyLost()
    {
        enemyCount--;

        if (enemyCount < 0)
            enemyCount = 0;

        if (enemyCount == 0 && isBattle)
        {
            isBattle = false;
            audioSource.clip = normalBGM;
            audioSource.Play();
        }
    }
}
