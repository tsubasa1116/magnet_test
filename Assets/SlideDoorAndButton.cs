using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SlideDoorAndButton : MonoBehaviour
{
    [Header("ドアの設定")]
    [Tooltip("左側の扉オブジェクトをセットしてください")]
    public GameObject leftDoor;

    [Tooltip("右側の扉オブジェクトをセットしてください")]
    public GameObject rightDoor;

    [Tooltip("それぞれの扉がスライドして開く距離（ローカルX軸方向）")]
    public float doorSlideDistance = 3.0f;

    [Tooltip("ドアが開くスピード")]
    public float doorOpenSpeed = 2.0f;

    [Header("ボタンの設定")]
    [Tooltip("実際に押し込まれるボタン可動部（キャップ）のオブジェクト")]
    public GameObject movingSwitch;

    [Tooltip("ボタンが押し込まれるローカル方向 (例: 下に沈むなら Vector3.down)")]
    public Vector3 localPushDirection = Vector3.down;

    [Tooltip("ボタンが押し込まれる距離")]
    public float switchPushDistance = 0.2f;

    [Tooltip("ボタンが沈むスピード")]
    public float switchPushSpeed = 10.0f;

    private bool isPressed = false;

    // ローカルの初期位置と目標位置
    private Vector3 leftDoorStartLocalPos;
    private Vector3 leftDoorOpenLocalPos;
    private Vector3 rightDoorStartLocalPos;
    private Vector3 rightDoorOpenLocalPos;

    private Vector3 switchStartLocalPos;
    private Vector3 switchPressedLocalPos;

    void Start()
    {
        // 左扉のローカル初期位置と目標位置を計算 (ローカルの左方向 -X へスライド)
        if (leftDoor != null)
        {
            leftDoorStartLocalPos = leftDoor.transform.localPosition;
            leftDoorOpenLocalPos = leftDoorStartLocalPos + Vector3.left * doorSlideDistance;
        }

        // 右扉のローカル初期位置と目標位置を計算 (ローカルの右方向 +X へスライド)
        if (rightDoor != null)
        {
            rightDoorStartLocalPos = rightDoor.transform.localPosition;
            rightDoorOpenLocalPos = rightDoorStartLocalPos + Vector3.right * doorSlideDistance;
        }

        // スイッチ可動部のローカル初期位置と目標位置を計算
        if (movingSwitch != null)
        {
            switchStartLocalPos = movingSwitch.transform.localPosition;
            switchPressedLocalPos = switchStartLocalPos + localPushDirection.normalized * switchPushDistance;
        }
    }

    void Update()
    {
        if (isPressed)
        {
            // 左扉をローカル座標でスムーズにスライド
            if (leftDoor != null)
            {
                leftDoor.transform.localPosition = Vector3.Lerp(
                    leftDoor.transform.localPosition,
                    leftDoorOpenLocalPos,
                    Time.deltaTime * doorOpenSpeed
                );
            }

            // 右扉をローカル座標でスムーズにスライド
            if (rightDoor != null)
            {
                rightDoor.transform.localPosition = Vector3.Lerp(
                    rightDoor.transform.localPosition,
                    rightDoorOpenLocalPos,
                    Time.deltaTime * doorOpenSpeed
                );
            }

            // スイッチをローカル座標でスムーズに押し込む
            if (movingSwitch != null)
            {
                movingSwitch.transform.localPosition = Vector3.Lerp(
                    movingSwitch.transform.localPosition,
                    switchPressedLocalPos,
                    Time.deltaTime * switchPushSpeed
                );
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // すでに押されている場合は何もせず終了
        if (isPressed) return;

        // プレイヤーやギミック用オブジェクトがぶつかったら作動
        isPressed = true;
    }
}