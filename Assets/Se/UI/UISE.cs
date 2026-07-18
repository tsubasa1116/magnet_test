using UnityEngine;

public class UISE : MonoBehaviour
{
    public static UISE Instance;

    [Header("UI SE")]
    [SerializeField] private AudioClip startSE;
    [SerializeField] private AudioClip enterSE;
    [SerializeField] private AudioClip cursorSE;
    [SerializeField] private AudioClip cancelSE;

    [SerializeField] private AudioSource audioSource; // OutputにSEグループを割り当てる

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    public void StartUI() => PlaySE(startSE);
    public void EnterUI() => PlaySE(enterSE);
    public void CursorUI() => PlaySE(cursorSE);
    public void CancelUI() => PlaySE(cancelSE);

    private void PlaySE(AudioClip clip)
    {
        if (clip == null) return;
        audioSource.PlayOneShot(clip);
    }
}