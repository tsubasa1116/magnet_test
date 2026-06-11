using System;
using System.Collections;
using UnityEngine;

public static class AnimatorConveni
{
    /// <summary>
    /// 現在再生中のアニメーションが終了するまで待機するコルーチン
    /// </summary>
    /// <param name="animator">対象のAnimator</param>
    /// <param name="layerIndex">レイヤーインデックス（基本は0）</param>
    /// <param name="transitionDelay">ステート遷移が反映されるまでの猶予時間</param>
    /// <returns></returns>
    public static IEnumerator WaitForCurrentAnimationEnd(this Animator animator, int layerIndex = 0, float transitionDelay = 0.1f)
    {
        // Animatorが割り当てられていない場合のセーフティ
        if (animator == null)
        {
            yield return new WaitForSeconds(1.0f);
            yield break;
        }

        // SetTriggerなどがAnimator内部で反映され、ステートが切り替わるまで少し待つ
        if (transitionDelay > 0f)
        {
            yield return new WaitForSeconds(transitionDelay);
        }

        // 遷移後の現在のアニメーションの長さを取得して待機
        float animLength = animator.GetCurrentAnimatorStateInfo(layerIndex).length;
        yield return new WaitForSeconds(animLength);
    }
}