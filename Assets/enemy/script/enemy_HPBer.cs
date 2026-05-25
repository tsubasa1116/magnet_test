using UnityEngine;
using UnityEngine.UI;

public class EnemyHPBar : MonoBehaviour
{
    [Header("HPテクスチャ")]
    [SerializeField] private Image blueBar;
    [SerializeField] private Image whiteBar;
    [SerializeField] private RectTransform hpBarObj;

    [Header("HP")]
    [SerializeField] private float maxHP = 100.0f;
    [SerializeField] private float currentHP;
    private float targetFillAmount = 1.0f;

    [Header("バー速度")]
    [SerializeField] private float blueBarSpeed = 0.5f;
    [SerializeField] private float whiteBarSpeed = 0.5f;
    [SerializeField] private float damageWait = 0.5f;
    private float waitTimer = 0.0f;

    [Header("シェイク")]
    [SerializeField] private float shakeDuration = 0.2f;
    [SerializeField] private float shakeSpeed = 50.0f;
    [SerializeField] private float shakeAmplitude = 10.0f;
    private float currentShakeTimer = 0.0f;
    private Vector2 originalPosition;

    [Header("デバッグ")]
    [SerializeField] private float testDamageValue = 20.0f;
    [SerializeField] private float testHealValue = 100.0f;

    private void Start()
    {
        currentHP = maxHP;

        if (blueBar) blueBar.fillAmount = 1.0f;
        if (whiteBar) whiteBar.fillAmount = 1.0f;

        if (hpBarObj != null)
        {
            originalPosition = hpBarObj.anchoredPosition;
        }
    }

    private void Update()
    {
        // 1. 青色バーの更新
        if (blueBar != null)
        {
            if (blueBar.fillAmount > targetFillAmount)
            {
                blueBar.fillAmount -= blueBarSpeed * Time.deltaTime;
                if (blueBar.fillAmount < targetFillAmount) blueBar.fillAmount = targetFillAmount;
            }
            else if (blueBar.fillAmount < targetFillAmount)
            {
                blueBar.fillAmount += blueBarSpeed * Time.deltaTime;
                if (blueBar.fillAmount > targetFillAmount) blueBar.fillAmount = targetFillAmount;
            }
        }

        // 2. 白色バーの更新
        if (waitTimer > 0.0f)
        {
            waitTimer -= Time.deltaTime;
        }
        else if (whiteBar != null)
        {
            if (whiteBar.fillAmount > blueBar.fillAmount)
            {
                whiteBar.fillAmount -= whiteBarSpeed * Time.deltaTime;
                if (whiteBar.fillAmount < blueBar.fillAmount)
                {
                    whiteBar.fillAmount = blueBar.fillAmount;
                }
            }
            else if (whiteBar.fillAmount < blueBar.fillAmount)
            {
                whiteBar.fillAmount = blueBar.fillAmount;
            }
        }

        // 3. シェイク更新
        UpdateShake();
    }

    // =========================================================
    //  インスペクターの「縦の三点リーダー」から実行できるデバッグ関数
    // =========================================================

    [ContextMenu("Debug/Apply Test Damage")]
    public void ApplyTestDamage()
    {
        TakeDamage(testDamageValue);
    }

    [ContextMenu("Debug/Apply Test Heal")]
    public void ApplyTestHeal()
    {
        TakeHeal(testHealValue);
    }

    // =========================================================

    public void TakeDamage(float damage)
    {
        currentHP -= damage;
        currentHP = Mathf.Clamp(currentHP, 0.0f, maxHP);
        targetFillAmount = currentHP / maxHP;

        waitTimer = damageWait;
        currentShakeTimer = shakeDuration;
    }

    public void TakeHeal(float healAmount)
    {
        currentHP += healAmount;
        currentHP = Mathf.Clamp(currentHP, 0.0f, maxHP);
        targetFillAmount = currentHP / maxHP;
        waitTimer = 0.0f;
    }

    private void UpdateShake()
    {
        if (hpBarObj == null) return;

        if (currentShakeTimer > 0.0f)
        {
            currentShakeTimer -= Time.deltaTime;
            if (currentShakeTimer < 0.0f) currentShakeTimer = 0.0f;

            float t = currentShakeTimer / shakeDuration;
            float elapsed = shakeDuration - currentShakeTimer;
            float phase = elapsed * shakeSpeed;

            float x = Mathf.Sin(phase) * shakeAmplitude * t;
            float y = Mathf.Cos(phase * 1.3f) * (shakeAmplitude * 0.5f) * t;

            hpBarObj.anchoredPosition = originalPosition + new Vector2(x, y);
        }
        else
        {
            hpBarObj.anchoredPosition = originalPosition;
        }
    }
}