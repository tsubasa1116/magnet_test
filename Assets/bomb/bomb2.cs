using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bomb2 : MonoBehaviour
{
    public float explosionPower = 10f;
    public float explosionRadius = 3f;
    public float timeToExplode = 7.0f; // 爆発までの秒数

    public bool isThrown = false;
    private bool isPolarityLocked = false; // 極性をロックするかどうか
    private bool hasExploded = false;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // 投げられたらタイマーを減らし、0になったら爆発
        if (isThrown && !hasExploded)
        {
            timeToExplode -= Time.deltaTime;

            if (timeToExplode <= 0f)
            {
                Explode();
            }
        }
    }

    // ★追加：何かにぶつかったら滑らないように急ブレーキをかける
    void OnCollisionEnter(Collision collision)
    {
        if (isThrown && !hasExploded && rb != null)
        {
            // ボムが地面などに当たったら、転がりと滑りを抑えるために抵抗を大きくする
            rb.drag = 5f;          // 移動の抵抗をかなり強くする
            rb.angularDrag = 5f;   // 回転の抵抗も強くしてコロコロ転がるのを止める

            // もし完全にピタッと止めたい場合は以下の2行のコメントアウトを外してください
            // rb.velocity = Vector3.zero;
            // rb.angularVelocity = Vector3.zero;
        }
    }

    // 投げられた瞬間に呼ばれる関数（プレイヤー側から呼ぶ）
    public void Launch()
    {
        isThrown = true;
        isPolarityLocked = true; // 投げたので極性を確定（ロック）する

        // 投げる瞬間に空気抵抗をリセット（勢いよく飛ばすため）
        if (rb != null)
        {
            rb.drag = 0f;
            rb.angularDrag = 0.05f;
        }
    }

    // 極性を設定する公開関数（もし必要な場合）
    public void SetPolarity(string newTag)
    {
        // ロックされていたら変更を拒否
        if (isPolarityLocked) return;

        if (newTag == "N_Pole" || newTag == "S_Pole")
        {
            gameObject.tag = newTag;
        }
    }

    void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);

        foreach (Collider hit in hits)
        {
            Rigidbody hitRb = hit.GetComponent<Rigidbody>();
            if (hitRb != null)
            {
                hitRb.AddExplosionForce(explosionPower, transform.position, explosionRadius);
            }

            // ★変更：爆発に巻き込まれた敵(enemy)は確実に即死させる
            enemy targetEnemy = hit.GetComponent<enemy>();
            if (targetEnemy != null)
            {
                // 1撃で倒すため、HPの最大値以上の巨大なダメージを送る
                targetEnemy.TakeDamage(9999f);
            }

            // ★追加：ボスにダメージを与える（固定ダメージの例として50）
            Boss targetBoss = hit.GetComponent<Boss>();
            if (targetBoss != null)
            {
                targetBoss.TakeDamage(50f); 
            }

            // ★追加：プレイヤーにダメージを与える（固定ダメージの例として1）
            Controller targetPlayer = hit.GetComponent<Controller>();
            if (targetPlayer != null)
            {
                targetPlayer.TakeDamage(1); 
            }
        }

        Destroy(gameObject);
    }
}