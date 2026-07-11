using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ThrowableObject : MonoBehaviour
{
    [Header("ダメージ")]
    [SerializeField] private float damage = 100.0f;

    [Header("発射中か")]
    public bool IsThrown = false;

    /// <summary>
    /// このオブジェクトのダメージ量
    /// </summary>
    public float Damage => damage;

    /// <summary>
    /// 発射状態にする
    /// </summary>
    public void Throw()
    {
        IsThrown = true;
    }

    /// <summary>
    /// 発射状態を解除する
    /// </summary>
    public void ResetThrown()
    {
        IsThrown = false;
    }
}