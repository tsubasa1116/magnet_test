using UnityEngine;
using UnityEngine.UI;

public class TimerUI : MonoBehaviour
{
    [SerializeField] private Image minute10;
    [SerializeField] private Image minute1;
    [SerializeField] private Image second10;
    [SerializeField] private Image second1;

    [SerializeField] private Image colon;

    [SerializeField] private Sprite[] numberSprites; // 0～9
    [SerializeField] private Sprite colonSprite;

    void Update()
    {
        if (GameManager.Instance == null)
            return;

        int total = Mathf.FloorToInt(GameManager.Instance.ElapsedTime);

        int minute = total / 60;
        int second = total % 60;

        minute10.sprite = numberSprites[(minute / 10) % 10];
        minute1.sprite = numberSprites[minute % 10];

        second10.sprite = numberSprites[second / 10];
        second1.sprite = numberSprites[second % 10];

        colon.sprite = colonSprite;
    }
}