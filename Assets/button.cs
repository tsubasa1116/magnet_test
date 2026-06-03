using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class button : MonoBehaviour
{
    [Header("ドアの設定")]
    [Tooltip("開けたい対象のドアのオブジェクトをここにドラッグ＆ドロップしてください")]
    public GameObject targetDoor;

    [Tooltip("ドアが上に上がる高さ")]
    public float doorLiftHeight = 6.0f;

    [Tooltip("ドアが開くスピード")]
    public float doorOpenSpeed = 2.0f;

    [Header("ボタンの設定")]
    [Tooltip("当たられた後にボタンが押し込まれる距離")]
    public float pushDistance = 5.0f;

    private bool isPressed = false;
    private Vector3 doorOpenPosition;

    void Start()
    {
        // 対象のドアがセットされていれば、開いたあとの目標位置を計算しておく
        if (targetDoor != null)
        {
            doorOpenPosition = targetDoor.transform.position + Vector3.up * doorLiftHeight;
        }
    }

    void Update()
    {
        // ボタンが押されており、なおかつ対象のドアが設定されている場合
        if (isPressed && targetDoor != null)
        {
            // Vector3.Lerpを使って、ドアを目標位置まで滑らかに移動させる
            targetDoor.transform.position = Vector3.Lerp(
                targetDoor.transform.position,
                doorOpenPosition,
                Time.deltaTime * doorOpenSpeed
            );
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 既に押されている場合は反応しないようにする
        if (!isPressed && collision.contacts.Length > 0)
        {
            isPressed = true;

            // ぶつかった面の向き（法線）を取得し、その逆方向（ボタンの内部に向かう方向）を計算する
            Vector3 pushDirection = -collision.contacts[0].normal;
            
            // ぶつかった面の方向へボタンを押し込む
            transform.position += pushDirection * pushDistance;
        }
    }
}
