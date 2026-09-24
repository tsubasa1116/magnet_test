using UnityEngine;

// ボスの部位(体・腕)。
// ※ボスへのダメージは enemy_Boss.OnThrownObjectHit に一本化した
//   (投げた物の ThrowableObject がボスのどの部位に当たったかを見て、バリア越しは半減・コアむき出しは大ダメージ)。
//   ここでダメージを入れると二重になるため、このコンポーネントはダメージ処理をしない。
//   プレハブに付いたままなので設定値は残してある。
public class BossDmgToParts : MonoBehaviour
{
    [Header("参照設定")]
    [Tooltip("スクリプト")]
    public enemy_Boss  bossScript;

    [Header("判定設定")]
    [Tooltip("部位毎のダメージ倍(現在は未使用)")]
    public float damageMultiplier = 1.0f;

    [Tooltip("ダメージ判定を行う攻撃オブジェクトのタグ(現在は未使用)")]
    public string attackTagN = "N_Pole";
    public string attackTagS = "S_Pole";
}
