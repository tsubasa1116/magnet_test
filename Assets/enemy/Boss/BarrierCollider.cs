using UnityEngine;
using System.Collections;

[System.Serializable]
public struct BarrierDamageSetting
{
    public string attackTag;  // 判定するタグ
    public float damage; // そのタグが当たった時のダメージ量
}

public class BarrierCollider : MonoBehaviour
{
    [Header("参照設定")]
    public enemy_Boss bossScript;

    [Header("ダメージ設定")]
    public BarrierDamageSetting[] damageSettings;

    public bool isInvincible = false; // 無敵状態かどうかのフラグ

    private void OnCollisionEnter(Collision collision)
    {
        foreach (BarrierDamageSetting setting in damageSettings)
        {
            if (collision.gameObject.CompareTag(setting.attackTag))
            {
                isInvincible = true;

                // ボス本体にバリアダメージを送る
                bossScript.TakeBarrierDamage(setting.damage);

                break;
            }
        }

        StartCoroutine(InvincibleCooltime());
    }

    // ==========================
    // 無敵時間用コルーチン
    // ==========================
    private IEnumerator InvincibleCooltime()
    {
        yield return new WaitForSeconds(0.5f);
        isInvincible = false;
    }

}