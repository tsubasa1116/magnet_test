using UnityEngine;

public class BossDmgToParts : MonoBehaviour
{
    [Header("参照設定")]
    [Tooltip("スクリプト")]
    public enemy_Boss  bossScript;

    [Header("判定設定")]
    [Tooltip("部位毎のダメージ倍")]
    public float damageMultiplier = 1.0f;

    [Tooltip("ダメージ判定を行う攻撃オブジェクトのタグ")]
    public string attackTagN = "N_Pole";
    public string attackTagS = "S_Pole";

    // 物理衝突（Rigidbody等）で判定する場合
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag(attackTagN) || collision.gameObject.CompareTag(attackTagS))
        {
            SendDamageToBoss(collision.gameObject);
        }
    }

    // トリガー侵入（IsTrigger）で判定する場合
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(attackTagN) || other.CompareTag(attackTagS))
        {
            SendDamageToBoss(other.gameObject);
        }
    }

    private void SendDamageToBoss(GameObject attacker)
    {
        if (bossScript == null) return;

        // 【重要】ここで攻撃側のオブジェクトから基本ダメージを取得します。
        // ※以下の "PlayerAttackScript" はご自身のプロジェクトのクラス名に書き換えてください。
        float baseDamage = bossScript.takenDamage; // 仮の固定ダメージ

        /* // 実際のゲームでの実装例：
        PlayerAttackScript attackScript = attacker.GetComponent<PlayerAttackScript>();
        if (attackScript != null)
        {
            baseDamage = attackScript.attackPower;
        }
        */

        // メインスクリプトにダメージと倍率を送信
        bossScript.TakeDamage(baseDamage, damageMultiplier);
    }
}