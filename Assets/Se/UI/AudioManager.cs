using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [SerializeField] private AudioMixer mixer;

    [Header("BGM再生用")]
    [SerializeField] private AudioSource bgmSource;

    [Header("BGMリスト")]
    [SerializeField] private BGMEntry[] bgmList;

    [System.Serializable]
    public class BGMEntry
    {
        public string bgmName;
        public AudioClip clip;
    }

    private Dictionary<string, AudioClip> bgmDict;

    // ★追加：曲名ごとの再生位置を記憶
    private Dictionary<string, float> bgmPositions = new Dictionary<string, float>();

    private const string BGM_PARAM = "BGMVolume";
    private const string SE_PARAM = "SEVolume";
    private const string MASTER_PARAM = "MasterVolume";

    private string currentBGMName = "";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        bgmDict = new Dictionary<string, AudioClip>();
        foreach (var entry in bgmList)
        {
            if (!bgmDict.ContainsKey(entry.bgmName))
            {
                bgmDict.Add(entry.bgmName, entry.clip);
            }
        }
    }

    // ---- 音量設定（既存のまま） ----

    public void SetBGMVolume(float linearValue) => SetVolume(BGM_PARAM, linearValue);
    public void SetSEVolume(float linearValue) => SetVolume(SE_PARAM, linearValue);
    public void SetMasterVolume(float linearValue) => SetVolume(MASTER_PARAM, linearValue);

    private void SetVolume(string param, float linearValue)
    {
        float dB = linearValue <= 0.0001f ? -80f : Mathf.Log10(linearValue) * 20f;
        mixer.SetFloat(param, dB);
    }

    // ---- BGM再生（続きから再生できる版） ----

    // resumeFromLastPosition: trueなら中断した位置から再開、falseなら常に最初から
    public void PlayBGM(string name, bool loop = true, bool resumeFromLastPosition = false)
    {
        if (!bgmDict.TryGetValue(name, out AudioClip clip))
        {
            Debug.LogWarning($"[AudioManager] BGM '{name}' が見つかりません。");
            return;
        }

        if (currentBGMName == name && bgmSource.isPlaying) return;

        // 今流れている曲の再生位置を保存してから切り替える
        SaveCurrentPosition();

        currentBGMName = name;
        bgmSource.clip = clip;
        bgmSource.loop = loop;
        bgmSource.Play();

        if (resumeFromLastPosition && bgmPositions.TryGetValue(name, out float savedTime))
        {
            // クリップの長さを超えないように安全策
            bgmSource.time = Mathf.Clamp(savedTime, 0f, Mathf.Max(0f, clip.length - 0.01f));
        }
    }

    // 現在再生中の曲の位置を記録する
    private void SaveCurrentPosition()
    {
        if (!string.IsNullOrEmpty(currentBGMName) && bgmSource.clip != null)
        {
            bgmPositions[currentBGMName] = bgmSource.time;
        }
    }

    // 特定の曲の再開位置をリセットしたい場合（例：フィールドに再入場したら最初から、など）
    public void ResetBGMPosition(string name)
    {
        if (bgmPositions.ContainsKey(name))
        {
            bgmPositions.Remove(name);
        }
    }

    public void StopBGM()
    {
        SaveCurrentPosition();
        bgmSource.Stop();
        currentBGMName = "";
    }

    public void PlayBGMWithFade(string name, float fadeDuration = 1.0f, bool loop = true, bool resumeFromLastPosition = false)
    {
        if (!bgmDict.TryGetValue(name, out AudioClip clip))
        {
            Debug.LogWarning($"[AudioManager] BGM '{name}' が見つかりません。");
            return;
        }

        if (currentBGMName == name && bgmSource.isPlaying) return;

        StopAllCoroutines();
        StartCoroutine(FadeToNewBGM(clip, name, fadeDuration, loop, resumeFromLastPosition));
    }

    private IEnumerator FadeToNewBGM(AudioClip newClip, string newName, float duration, bool loop, bool resumeFromLastPosition)
    {
        float startVolume = bgmSource.volume;

        // フェードアウト
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(startVolume, 0f, t / duration);
            yield return null;
        }

        // ★フェードアウトが終わった時点の位置を保存
        SaveCurrentPosition();

        bgmSource.Stop();
        bgmSource.clip = newClip;
        bgmSource.loop = loop;
        bgmSource.Play();
        currentBGMName = newName;

        if (resumeFromLastPosition && bgmPositions.TryGetValue(newName, out float savedTime))
        {
            bgmSource.time = Mathf.Clamp(savedTime, 0f, Mathf.Max(0f, newClip.length - 0.01f));
        }

        // フェードイン
        t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(0f, startVolume, t / duration);
            yield return null;
        }

        bgmSource.volume = startVolume;
    }
}