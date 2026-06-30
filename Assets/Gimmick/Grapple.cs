using UnityEngine;

public class Grapple : MonoBehaviour
{
    [Header("立体機動")]
    [SerializeField] private float grappleSpeed = 10f;
    [SerializeField] private float stopDistance = 5f;
    private bool isGrappling = false;

    [Header("エフェクト")]
    [SerializeField] private GameObject nPoleObjectAttractEffect;
    [SerializeField] private GameObject sPoleObjectAttractEffect;

    [Header("レーザーエフェクト")]
    [SerializeField] private GameObject nPoleLaserEffect;
    [SerializeField] private GameObject sPoleLaserEffect;

    private GameObject currentLaser;
    private LineRenderer currentLine;
    private GameObject currentEffect;

    private GameObject player;
    private Rigidbody playerRb;
    private SpringJoint grappleJoint;

    // プレイヤーから呼び出す
    public void StartGrapple(GameObject targetPlayer)
    {
        if (isGrappling) return;

        magnet magnetScript = targetPlayer.GetComponentInChildren<magnet>();

        // 磁力OFFなら立体機動できない
        if (magnetScript == null || !magnetScript.isActive) return;

        player = targetPlayer;
        playerRb = player.GetComponent<Rigidbody>();

        if (playerRb == null) return;

        isGrappling = true;

        grappleJoint = player.AddComponent<SpringJoint>();
        grappleJoint.autoConfigureConnectedAnchor = false;
        grappleJoint.connectedAnchor = transform.position;

        float distance = Vector3.Distance(player.transform.position, transform.position);

        grappleJoint.maxDistance = distance * 0.8f;
        grappleJoint.minDistance = 0f;
        grappleJoint.spring = 10f;
        grappleJoint.damper = 5f;
        grappleJoint.massScale = 4.5f;

        // 開始時に少し勢いをつける
        Vector3 dir = (transform.position - player.transform.position).normalized;

        playerRb.AddForce(dir * 10f, ForceMode.Impulse);

        StartGrappleEffect();
    }

    void Update()
    {
        if (!isGrappling || player == null) return;

        magnet magnetScript = player.GetComponentInChildren<magnet>();

        if (magnetScript == null || !magnetScript.isActive)
        {
            StopGrapple();
            return;
        }
        // プレイヤーを対象方向へ向かせる
        Vector3 direction = (transform.position - player.transform.position).normalized;

        player.transform.rotation = 
            Quaternion.RotateTowards(player.transform.rotation, 
            Quaternion.LookRotation(direction), 600f * Time.deltaTime);


        // エフェクトを常にプレイヤー方向へ向ける
        if (currentEffect != null)
        {
            // オブジェクトの少し前に配置
            currentEffect.transform.position = transform.position + direction * 5.0f;

            // プレイヤーの方向を向かせる
            currentEffect.transform.rotation = Quaternion.LookRotation(-direction);
        }

        // 徐々にワイヤーを縮める
        if (grappleJoint != null)
        {
            grappleJoint.maxDistance = Mathf.MoveTowards(grappleJoint.maxDistance,0f,grappleSpeed * Time.deltaTime);
        }

        if (currentLine != null && player != null)
        {
            currentLine.SetPosition(0, transform.position);        // オブジェクト
            currentLine.SetPosition(1, player.transform.position); // プレイヤー
        }

        // 十分近づいたら終了
        if (Vector3.Distance(player.transform.position, transform.position) <= stopDistance)
        {
            StopGrapple();
        }
    }

    // プレイヤーがこのオブジェクトに立体機動した時に呼ぶ
    public void StartGrappleEffect()
    {
        if (currentEffect == null && sPoleObjectAttractEffect != null)
        {
            currentEffect = Instantiate(sPoleObjectAttractEffect, transform.position, Quaternion.identity, transform);
        }

        // レーザー生成
        if (currentLaser == null)
        {
            GameObject laserPrefab = null;

            magnet magnetScript = player.GetComponentInChildren<magnet>();

            if (magnetScript != null)
            {
                if (magnetScript.magnetMode == 1)   laserPrefab = nPoleLaserEffect;
                else                                laserPrefab = sPoleLaserEffect;
            }

            if (laserPrefab != null)
            {
                currentLaser = Instantiate(laserPrefab);
                currentLine = currentLaser.GetComponentInChildren<LineRenderer>();
            }
        }
    }

    public void StopGrapple()
    {
        isGrappling = false;

        if (grappleJoint != null)
        {
            Destroy(grappleJoint);
        }

        StopGrappleEffect();

        player = null;
        playerRb = null;
        grappleJoint = null;
    }

    // 立体機動終了時に呼ぶ
    public void StopGrappleEffect()
    {
        if (currentLaser != null)
        {
            Destroy(currentLaser);
            currentLaser = null;
            currentLine = null;
        }

        if (currentEffect != null)
        {
            Destroy(currentEffect);
            currentEffect = null;
        }
    }

}