using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AuraRing : MonoBehaviour
{
    public LineRenderer lr;
    public int segments = 40;
    public float radius = 0.5f;

    public enum Pole { N, S }
    public Pole pole;

    public bool isHeld = false;

    float timer = 5f;

    void Start()
    {
        if (lr == null)
            lr = GetComponent<LineRenderer>();

        CreateCircle();
        UpdateColor();

        lr.widthMultiplier = 2.0f;
    }

    void Update()
    {
        // カメラ向き
        transform.forward = Camera.main.transform.forward;

        // 脈動
        lr.widthMultiplier = 2.0f + Mathf.Sin(Time.time * 3f) * 0.02f;

        // 持ってる間は完全停止
        if (isHeld)
        {
            lr.enabled = true; // 点滅止める
            return;
        }

        // 通常タイマー処理
        timer -= Time.deltaTime;

        // 切り替え前に点滅
        if (timer < 1f)
        {
            lr.enabled = Mathf.FloorToInt(Time.time * 10) % 2 == 0;
        }
        else
        {
            lr.enabled = true;
        }

        if (timer <= 0f)
        {
            SwitchPole();
            timer = 5f;
        }
    }

    void CreateCircle()
    {
        lr.positionCount = segments;

        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2 / segments;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;

            lr.SetPosition(i, new Vector3(x, y, 0));
        }
    }

    public void UpdateColor()
    {
        if (pole == Pole.N)
        {
            lr.startColor = lr.endColor = Color.red;
            transform.parent.tag = "N_Pole";
        }
        else
        {
            lr.startColor = lr.endColor = Color.blue;
            transform.parent.tag = "S_Pole";
        }
    }

    public void SwitchPole()
    {
        pole = (pole == Pole.N) ? Pole.S : Pole.N;
        UpdateColor();
    }

    // 追加：持つ/離すを管理する関数
    public void SetHeld(bool held)
    {
        isHeld = held;

        if (held)
        {
            timer = 5f; // 持った瞬間リセット
        }
    }
}