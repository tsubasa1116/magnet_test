using UnityEngine;
using UnityEngine.InputSystem;

/// 立体機動の物理処理
/// 振り子運動・張力・巻き取りを管理
[System.Serializable]
public class GrapplePhysics
{
    #region Inspector

    [Header("ロープ")]
    [SerializeField]private float reelSpeed = 6f;
    [SerializeField]private float minimumLength = 5f;
    [SerializeField]private float initialPullForce = 10f;

    [Header("移動量")]
    [SerializeField]private float maxVelocity = 25f;
    [SerializeField]private float inputAcceleration = 6f;
    [SerializeField]private float sideAcceleration = 4f;

    #endregion

    private Rigidbody rb;
    private Transform player;
    private Transform anchor;
    private bool grappling;
    private float ropeLength;

    public bool IsGrappling
    {
        get
        {
            return grappling;
        }
    }

    public void Initialize(Rigidbody rigidbody)
    {
        rb = rigidbody;
        player = rb.transform;
    }

    public void Begin(Transform grapplePoint)
    {
        if (grapplePoint == null) return;

        anchor = grapplePoint;

        grappling = true;

        ropeLength =Vector3.Distance(player.position, anchor.position);

        Vector3 dir = (anchor.position - player.position).normalized;

        rb.AddForce(dir * initialPullForce, ForceMode.VelocityChange);
    }

    public void End()
    {
        grappling = false;
        anchor = null;
    }

    public void FixedTick(Vector2 moveInput, Transform cameraTransform)
    {
        if (!grappling) return;

        if (anchor == null) return;

        ReelRope();

        ApplyRopeConstraint();

        ApplySwingInput(moveInput, cameraTransform);

        ClampVelocity();
    }

    private void ReelRope()
    {
        ropeLength = Mathf.Max(minimumLength, ropeLength - reelSpeed * Time.fixedDeltaTime);
    }

    /// ロープ拘束
    private void ApplyRopeConstraint()
    {
        Vector3 anchorPos = anchor.position;
        Vector3 playerPos = rb.position;

        // アンカー→プレイヤー
        Vector3 rope = playerPos - anchorPos;

        float distance = rope.magnitude;

        if (distance < 0.001f) return;

        Vector3 ropeDir = rope / distance;

        //------------------------------------------------
        // ロープ長より長ければ位置を補正
        //------------------------------------------------

        if (distance > ropeLength)
        {
            playerPos = anchorPos + ropeDir * ropeLength;

            rb.position = playerPos;
        }

        //------------------------------------------------
        // 現在速度
        //------------------------------------------------

        if (distance < ropeLength) return;

        Vector3 velocity = rb.linearVelocity;

        //------------------------------------------------
        // ロープ方向速度
        //------------------------------------------------

        float radialSpeed = Vector3.Dot(velocity, ropeDir);

        //------------------------------------------------
        // 外へ向かう速度だけ除去
        //------------------------------------------------

        if (radialSpeed > 0f)
        {
            velocity -= ropeDir * radialSpeed;
        }

        //------------------------------------------------
        // 接線速度
        //------------------------------------------------

        // Tangential velocity is intentionally preserved for pendulum motion.

        //------------------------------------------------
        // 接線速度は維持
        //------------------------------------------------

        // Do not remove inward velocity: it is required for reeling in.

        rb.linearVelocity = velocity;
    }

    /// スイング中の入力
    private void ApplySwingInput(Vector2 moveInput, Transform cameraTransform)
    {
        if (moveInput.sqrMagnitude < 0.01f) return;

        Vector3 ropeDir = (player.position - anchor.position).normalized;

        // 接線方向
        Transform reference = cameraTransform != null ? cameraTransform : player;

        Vector3 tangent = Vector3.ProjectOnPlane(reference.forward, ropeDir).normalized;

        if (tangent.sqrMagnitude < 0.01f)
        {
            tangent = Vector3.Cross(ropeDir, Vector3.up).normalized;
        }

        //--------------------------------------------------
        // 前後入力
        //--------------------------------------------------
        float forward = moveInput.y;

        if (Mathf.Abs(forward) > 0.01f)
        {
            rb.AddForce(tangent * forward * inputAcceleration, ForceMode.Acceleration);
        }

        //--------------------------------------------------
        // 左右入力
        //--------------------------------------------------
        float side = moveInput.x;

        if (Mathf.Abs(side) > 0.01f)
        {
            Vector3 sideDir = Vector3.ProjectOnPlane(reference.right, ropeDir).normalized;

            if (sideDir.sqrMagnitude < 0.01f) sideDir = Vector3.Cross(ropeDir, tangent).normalized;

            rb.AddForce(sideDir * side * sideAcceleration, ForceMode.Acceleration);
        }
    }

    /// 最大速度
    private void ClampVelocity()
    {
        Vector3 velocity = rb.linearVelocity;

        float speed = velocity.magnitude;

        if (speed <= maxVelocity) return;

        rb.linearVelocity = velocity.normalized * maxVelocity;
    }

    /// 現在のアンカー
    public Transform Anchor
    {
        get
        {
            return anchor;
        }
    }

    /// ロープ長
    public float RopeLength
    {
        get
        {
            return ropeLength;
        }
    }

    /// 現在速度
    public Vector3 Velocity
    {
        get
        {
            return rb.linearVelocity;
        }
    }

    /// プレイヤー位置
    public Vector3 PlayerPosition
    {
        get
        {
            return player.position;
        }
    }

    /// 強制的にロープ長変更
    public void SetRopeLength(float length)
    {
        ropeLength = Mathf.Max( minimumLength, length);
    }

    /// 強制解除
    public void ForceRelease()
    {
        End();
    }
}
