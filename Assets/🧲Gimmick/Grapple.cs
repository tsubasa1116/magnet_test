using UnityEngine;

public class Grapple : MonoBehaviour
{
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

    [Header("立体機動")]
    [SerializeField] private float pullForce = 50f;      // 引っ張る力
    [SerializeField] private float reelSpeed = 15f;      // ワイヤー巻き取り速度
    [SerializeField] private float stopDistance = 5f;    // 終了距離

    private bool isGrappling;

    private Vector3 grapplePoint;        // アンカー位置
    private float ropeLength;            // 現在のロープ長

    // プレイヤーから呼び出す
    public void StartGrapple(GameObject targetPlayer)
    {
        if (isGrappling)
            return;

        player = targetPlayer;

        if (player == null)
            return;

        playerRb = player.GetComponent<Rigidbody>();

        if (playerRb == null)
            return;

        isGrappling = true;

        // アンカー位置を保存
        grapplePoint = transform.position;

        // 現在のロープ長
        ropeLength = Vector3.Distance(playerRb.position, grapplePoint);

        // 最初に少しだけ引っ張る
        Vector3 dir = (grapplePoint - playerRb.position).normalized;
        playerRb.AddForce(dir * pullForce * 0.5f, ForceMode.VelocityChange);

        PlayerMovement movement = player.GetComponent<PlayerMovement>();

        if (movement != null)
        {
            movement.IsOnRopeway = true;
        }

        // エフェクト開始
        StartGrappleEffect();
    }

    void Update()
    {
        if (!isGrappling || player == null)
            return;

        // プレイヤーをアンカー方向へ向ける
        Vector3 direction = (grapplePoint - player.transform.position).normalized;

        if (direction != Vector3.zero)
        {
            player.transform.rotation = Quaternion.RotateTowards(
                player.transform.rotation,
                Quaternion.LookRotation(direction),
                600f * Time.deltaTime);
        }

        // エフェクト
        if (currentEffect != null)
        {
            currentEffect.transform.position =
                grapplePoint + direction * 5f;

            currentEffect.transform.rotation =
                Quaternion.LookRotation(-direction);
        }

        // ワイヤー描画
        if (currentLine != null)
        {
            currentLine.SetPosition(0, grapplePoint);
            currentLine.SetPosition(1, player.transform.position);
        }

        // 近づいたら終了
        if (Vector3.Distance(player.transform.position, grapplePoint) <= stopDistance)
        {
            StopGrapple();
        }
    }

    private void FixedUpdate()
    {
        if (!isGrappling || playerRb == null)
            return;

        GrappleMove();
    }

    private void GrappleMove()
    {
        // ロープ方向
        Vector3 rope = grapplePoint - playerRb.position;

        float distance = rope.magnitude;

        if (distance <= 0.1f)
            return;

        Vector3 dir = rope.normalized;

        // 上方向へ引っ張りすぎない
        if (dir.y > 0f)
        {
            dir.y *= 0.2f;
            dir.Normalize();
        }

        // ワイヤー巻き取り
        ropeLength -= reelSpeed * Time.fixedDeltaTime;
        ropeLength = Mathf.Max(stopDistance, ropeLength);

        // アンカー方向へ引っ張る
        playerRb.AddForce(dir * pullForce, ForceMode.Acceleration);

        // ロープ長を超えたら補正
        if (distance > ropeLength)
        {
            // ロープ長に合わせる
            playerRb.position =
                grapplePoint - dir * ropeLength;

            // ロープ方向へ離れる速度だけ消す
            Vector3 velocity = playerRb.linearVelocity;

            float awaySpeed = Vector3.Dot(velocity, dir);

            if (awaySpeed < 0f)
            {
                velocity -= dir * awaySpeed;
            }

            playerRb.linearVelocity = velocity;
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
            // この点はプレイヤーの逆極なので、プレイヤー側の極でレーザー色を選ぶ
            GameObject laserPrefab = CompareTag("S_Pole") ? nPoleLaserEffect : sPoleLaserEffect;

            if (laserPrefab != null)
            {
                currentLaser = Instantiate(laserPrefab);
                currentLine = currentLaser.GetComponentInChildren<LineRenderer>();
            }
        }
    }

    public void StopGrapple()
    {
        if (!isGrappling)
            return;

        isGrappling = false;

        StopGrappleEffect();

        PlayerMovement movement = player.GetComponent<PlayerMovement>();

        if (movement != null)
        {
            movement.IsOnRopeway = false;
        }

        player = null;
        playerRb = null;
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