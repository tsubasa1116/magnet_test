using UnityEngine;

// プレイヤーを磁力でロープウェイへ引き寄せ、接続中はロープウェイと一緒に運ぶ。
public class RopewayMagnet : MonoBehaviour
{
    [Header("Attach")]
    [SerializeField] private float attachDistance = 0.8f;
    [SerializeField] private float pullSpeed = 14f;
    [SerializeField] private float rideMoveSpeed = 1.5f;
    [SerializeField] private float rideMoveRadius = 0.45f;

    private Transform player;
    private Rigidbody playerRb;
    private PlayerMovement playerMovement;
    private bool isAttached;
    private bool savedUseGravity;
    private Vector3 rideOffset;
    private Vector3 contactPointLocal;
    private Vector3 approachDirectionLocal;

    public bool HasPlayer => player != null;
    public bool IsAttached => isAttached;
    public Vector3 TetherPoint => transform.TransformPoint(contactPointLocal);

    // 発動時のプレイヤー側にある表面を接続点として記録する。
    public void AttachPlayer(GameObject playerObj)
    {
        if (HasPlayer && player != playerObj.transform) return;

        player = playerObj.transform;
        playerRb = playerObj.GetComponent<Rigidbody>();
        playerMovement = playerObj.GetComponent<PlayerMovement>();
        isAttached = false;
        rideOffset = Vector3.zero;

        SetContactPoint(player.position);

        if (playerRb != null)
        {
            savedUseGravity = playerRb.useGravity;
            playerRb.linearVelocity = Vector3.zero;
            playerRb.useGravity = false;
        }
    }

    public void DetachPlayer()
    {
        if (playerRb != null)
        {
            playerRb.useGravity = savedUseGravity;
            playerRb.linearVelocity = Vector3.zero;
        }
        if (playerMovement != null) playerMovement.IsOnRopeway = false;

        player = null;
        playerRb = null;
        playerMovement = null;
        isAttached = false;
        rideOffset = Vector3.zero;
    }

    void FixedUpdate()
    {
        if (player == null || playerRb == null) return;

        Vector3 targetPosition = GetAttachPosition();
        if (!isAttached)
        {
            playerRb.MovePosition(Vector3.MoveTowards(
                playerRb.position, targetPosition, pullSpeed * Time.fixedDeltaTime));

            if (Vector3.Distance(playerRb.position, targetPosition) <= 0.05f)
            {
                isAttached = true;
                if (playerMovement != null) playerMovement.IsOnRopeway = true;
            }
            return;
        }

        UpdateRideOffset();
        playerRb.MovePosition(GetAttachPosition() + transform.TransformDirection(rideOffset));

        // 接続しているロープウェイ本体を常に見る。
        Vector3 lookDirection = transform.position - playerRb.position;
        lookDirection.y = 0f;
        if (lookDirection.sqrMagnitude > 0.0001f)
            playerRb.MoveRotation(Quaternion.LookRotation(lookDirection, Vector3.up));
    }

    private void SetContactPoint(Vector3 playerPosition)
    {
        Vector3 closestPoint = transform.position;
        float closestDistance = float.MaxValue;

        foreach (Collider collider in GetComponentsInChildren<Collider>())
        {
            if (collider.isTrigger) continue;

            Vector3 point = collider.ClosestPoint(playerPosition);
            float distance = (point - playerPosition).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPoint = point;
            }
        }

        Vector3 direction = playerPosition - closestPoint;
        if (direction.sqrMagnitude < 0.0001f)
            direction = playerPosition - transform.position;
        if (direction.sqrMagnitude < 0.0001f)
            direction = -transform.forward;

        contactPointLocal = transform.InverseTransformPoint(closestPoint);
        approachDirectionLocal = transform.InverseTransformDirection(direction.normalized);
    }

    private Vector3 GetAttachPosition()
        => TetherPoint + transform.TransformDirection(approachDirectionLocal) * attachDistance;

    private void UpdateRideOffset()
    {
        if (playerMovement == null) return;

        Vector2 input = playerMovement.MoveInput;
        Vector3 localMove = new Vector3(input.x, 0f, input.y);
        rideOffset = Vector3.ClampMagnitude(
            rideOffset + localMove * rideMoveSpeed * Time.fixedDeltaTime,
            rideMoveRadius);
    }
}
