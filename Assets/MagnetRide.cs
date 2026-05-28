using UnityEngine;

public class MagnetRide : MonoBehaviour
{
    // ロープウェイ側の極
    // 1 = N
    // 2 = S
    public int ropePole = 1;

    private void OnTriggerEnter(Collider other)
    {
        // プレイヤーの magnet スクリプト取得
        magnet playerMagnet = other.GetComponent<magnet>();

        if (playerMagnet != null)
        {
            // 同じ極なら吸着
            if (playerMagnet.magnetMode == ropePole)
            {
                other.transform.SetParent(transform);

                Rigidbody rb = other.GetComponent<Rigidbody>();

                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.transform.parent == transform)
        {
            other.transform.SetParent(null);
        }
    }
}