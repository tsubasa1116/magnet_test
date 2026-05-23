    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    public class Bomb2 : MonoBehaviour
    {
        public float explosionPower = 10f;
        public float explosionRadius = 3f;

        public bool isThrown = false;
        private bool hasExploded = false;

        private Rigidbody rb;
        public bool isPolarityLocked = false;
        public float timeToExplode = 3f;

        void Start()
        {
            rb = GetComponent<Rigidbody>();
        }

        void Update()
        {
            if (isThrown && !hasExploded)
            {
                timeToExplode -= Time.deltaTime;

                if (timeToExplode <= 0f)
                {
                    Explode();
                }
            }
        }

        // 追記：地面にぶつかったら転がらないように急ブレーキをかける
        void OnCollisionEnter(Collision collision)
        {
            if (isThrown && !hasExploded && rb != null)
            {
                // 爆弾が地面などに衝突したら、回転や移動を減衰させるために抵抗を大きくする
                rb.linearDamping = 5f;    // 移動の抵抗をかなり強くする
                rb.angularDamping = 5f;   // 回転の抵抗を大きくしてコロコロ回転するのを止める

                // 完全にピタッと止めたい場合は以下の2行のコメントアウトを外してください
                // rb.velocity = Vector3.zero;
                // rb.angularVelocity = Vector3.zero;
            }
        }

        // 投げられた状態に呼ばれる関数（プレイヤー側から呼ぶ）
        public void Launch()
        {
            isThrown = true;
            isPolarityLocked = true; // 反転しないよう極性を確定（ロック）する

            // 投げた状態に空気抵抗をリセット（遠くへ飛ばすため）
            if (rb != null)
            {
                rb.linearDamping = 0f;
                rb.angularDamping = 0.05f;
            }
        }

        // 極性を設定する公開関数（外部から必要な場合）
        public void SetPolarity(string newTag)
        {
            // ロックされていたら変更しない
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

                // 変更：爆発に巻き込まれた敵(enemy)は確実に即死させる
                enemy targetEnemy = hit.GetComponent<enemy>();
                if (targetEnemy != null)
                {
                    // 1撃で倒すため、HPの最大値以上の強大なダメージを送る
                    targetEnemy.TakeDamage(9999f);
                }

                // 追記：ボスにダメージを与える（設定ダメージの例として50）
                Boss targetBoss = hit.GetComponent<Boss>();
                if (targetBoss != null)
                {
                    targetBoss.TakeDamage(50f); 
                }

                // 追記：プレイヤーにダメージを与える（設定ダメージの例として1）
                Controller targetPlayer = hit.GetComponent<Controller>();
                if (targetPlayer != null)
                {
                    targetPlayer.TakeDamage(1); 
                }
            }

            Destroy(gameObject);
        }
    }