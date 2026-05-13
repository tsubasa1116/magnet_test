using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))] // ���ǉ��F�������Z�Ő�����΂����߂ɕK�v
public class enemy : MonoBehaviour
{
    private enum EnemyState
    {
        Wait,   // �ҋ@�i�����ʒu�ɂ���j
        Notice, // �������ċ����Ă���i�����~�܂��Ă���j
        Chase,  // �ǐՁi�����Ēǂ������Ă���j
        Attack, // �U��
        Search, // �������ăE���E���T���Ă���
        Return  // ���߂ď����ʒu�ɋA���Ă���
    }

    [Header("�p�����[�^")]
    [SerializeField] private float maxHp = 100.0f;
    [SerializeField] private float found = 10.0f;
    [SerializeField] private float attackRange = 2.0f;
    [SerializeField] private float searchTime = 3.0f;   // ����������E���E�����鎞��
    [SerializeField] private float searchRadius = 5.0f; // �E���E������͈�
    [SerializeField] private float noticeTime = 1.0f;

    [Header("���̓p�����[�^")] // ���ǉ��F���΂̗͂̐ݒ�
    [SerializeField] private float magnetRadius = 8.0f;  // �{���Ȃǂ����m���鋗��
    [SerializeField] private float magnetForce = 50.0f;  // ���������E���������

    [Header("�Q��")]
    [SerializeField] private Transform targetPlayer;
    [SerializeField] private GameObject markExclamation; // �I
    [SerializeField] private GameObject markQuestion;    // �H

    private float currentHp;
    private NavMeshAgent agent;
    private Rigidbody rb; // ���ǉ��F�������Z�p

    private Vector3 startPosition;

    private EnemyState currentState = EnemyState.Wait;
    private float searchTimer; // �E���E���̎c�莞�Ԃ��v��^�C�}�[
    private float noticeTimer;

    private bool isMagnetized = false; // ���ǉ��F���͂Ő������ł���Œ����ǂ���

    void Start()
    {
        currentHp = maxHp;
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>(); // ���ǉ�

        // ���i��NavMeshAgent�œ����̂ŕ���(Rigidbody)�̓I�t�ɂ��Ă���
        if (rb != null) rb.isKinematic = true;

        startPosition = transform.position;

        if (markExclamation != null) markExclamation.SetActive(false);
        if (markQuestion != null) markQuestion.SetActive(false);
    }

    void FixedUpdate() // ���ǉ��F�����I�ȗ͂̔����FixedUpdate�ōs��
    {
        MagneticInteraction();
    }

    void Update()
    {
        // ���ǉ��F���͂Ŕ�΂���Ă���Ԃ�AI�̎v�l�i�ǐՂȂǁj���X�g�b�v����
        if (isMagnetized) return;

        if (targetPlayer == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);

        // ���̏�Ԃɂ���Ă�邱�Ƃ�ς���
        switch (currentState)
        {
            case EnemyState.Wait:
                if (distanceToPlayer <= found) ChangeState(EnemyState.Notice);
                break;

            case EnemyState.Notice:
                noticeTimer -= Time.deltaTime;
                if (noticeTimer <= 0) ChangeState(EnemyState.Chase);
                break;

            case EnemyState.Chase:
                if (distanceToPlayer <= attackRange) ChangeState(EnemyState.Attack);
                else if (distanceToPlayer > found + 5.0f) ChangeState(EnemyState.Search);
                else agent.SetDestination(targetPlayer.position);
                break;

            case EnemyState.Attack:
                if (distanceToPlayer > attackRange) ChangeState(EnemyState.Chase);
                else
                {
                    agent.isStopped = true;
                    Attack();
                }
                break;

            case EnemyState.Search:
                if (distanceToPlayer <= found) ChangeState(EnemyState.Notice);
                else
                {
                    searchTimer -= Time.deltaTime;
                    if (searchTimer <= 0) ChangeState(EnemyState.Return);
                    else if (agent.remainingDistance < 0.5f) WanderAround();
                }
                break;

            case EnemyState.Return:
                if (distanceToPlayer <= found) ChangeState(EnemyState.Notice);
                else if (agent.remainingDistance < 0.5f) ChangeState(EnemyState.Wait);
                break;
        }
    }

    // ���ǉ��F���͂�N�ɁES�ɂ����m���ė͂������鏈��
    private void MagneticInteraction()
    {
        // �������g�̃^�O���m�F
        bool isMyN = gameObject.CompareTag("N_Pole");
        bool isMyS = gameObject.CompareTag("S_Pole");

        if (!isMyN && !isMyS) return; // ���������Ή����Ă��Ȃ���Ζ���

        Collider[] colliders = Physics.OverlapSphere(transform.position, magnetRadius);
        bool feelingMagnet = false;
        Vector3 totalForce = Vector3.zero;

        // ���������Ă���Ώۂ��L�^
        Transform attachedTarget = null;
        float minDistance = float.MaxValue;

        foreach (Collider col in colliders)
        {
            if (col.gameObject == gameObject) continue; // �������g�͏��O

            // ���肪 enemy (����) �������犱���Ȃ��i��������j
            if (col.GetComponent<enemy>() != null) continue;

            bool isOtherN = col.CompareTag("N_Pole");
            bool isOtherS = col.CompareTag("S_Pole");

            if (isOtherN || isOtherS)
            {
                // �������瑊��ւ̕����Ƌ���
                Vector3 dirToOther = col.transform.position - transform.position;
                float distance = dirToOther.magnitude;

                // �������߂�����ꍇ��0���h�~
                float safeDistance = distance < 0.5f ? 0.5f : distance;

                // �������߂��Ȃ�قǔ���I�ɗ͂������Ȃ�悤�ɂ���
                float force = magnetForce * (1.0f + (magnetRadius - safeDistance) / magnetRadius);

                // �Ⴄ�Ɂi�����񂹂�E�������j
                if ((isMyN && isOtherS) || (isMyS && isOtherN))
                {
                    feelingMagnet = true;

                    // �\���ɋ߂���΁u�܂Ƃ����v�����ɂ��邽�߂̔���
                    if (distance < 2.0f)
                    {
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            attachedTarget = col.transform;
                        }
                    }
                    else
                    {
                        // �܂��������͒ʏ�ʂ��������i�G���g�݂̂Ɍ������͂�������j
                        totalForce += dirToOther.normalized * force;
                    }
                }
                // �����Ɂi�����������j
                else if ((isMyN && isOtherN) || (isMyS && isOtherS))
                {
                    // �G���g�ւ̔����͂̂݉�����
                    totalForce -= dirToOther.normalized * force;
                    feelingMagnet = true;
                }
            }
        }

        // ���͂��󂯂Ă���ۂ̐؂�ւ� (NavMeshAgent��Rigidbody�̐؂�ւ�)
        if (feelingMagnet)
        {
            if (agent.enabled)
            {
                agent.enabled = false;   // �ړ�AI���ꎞ��~
                rb.isKinematic = false;  // �������Z���I��
                isMagnetized = true;

                // �z���񂹂��₷�����邽�߂ɁA��C��R���ꎞ�I�ɉ�����
                rb.linearDamping = 0.5f;
            }

            // ������ԁi�܂Ƃ����j�̏���
            if (attachedTarget != null)
            {
                // ���ǉ��F���e�Ȃǂɖ����������ɂ͎���(mass)���ꎞ�I�ɋɏ��ɂ��đ���������͂��Ȃ���
                rb.mass = 0.01f;

                // ����̒��S�Ɍ����Ĕ��ɋ����͂ň������葱����i�������j
                Vector3 stickDir = attachedTarget.position - transform.position;

                // �o�E���h�i�����ɂ��\��j��h�����߁A���x�𐧌�����������
                rb.linearVelocity = stickDir.normalized * 5f;

                // �͋Z�ŃX���b�v�i���C�Ȃǂ𖳎����Ē���t���j������BVelocityChange���g���ċ����I�ɓ������B
                rb.AddForce(stickDir.normalized * (magnetForce * 5f), ForceMode.Acceleration);
            }
            else
            {
                // ���ǉ��F�������Ă��Ȃ����͌��̎��ʂɖ߂��i�f�t�H���g��1�̏ꍇ�j
                rb.mass = 1.0f;

                if (totalForce.magnitude > 0.1f)
                {
                    // ForceMode.VelocityChange (���ʖ����ő����ɉ���) ���g���ăO���b�ƈ����񂹂�
                    rb.AddForce(totalForce * Time.fixedDeltaTime, ForceMode.VelocityChange);
                }
            }
        }
        else if (isMagnetized)
        {
            // ���͂̉e���͈͂���O��A������������������AI(NavMesh)�ɖ߂�
            if (rb.linearVelocity.magnitude < 0.5f)
            {
                rb.mass = 1.0f; // �����ʂ����ɖ߂�
                rb.isKinematic = true;
                isMagnetized = false;

                // NavMesh�i�����鏰�j�̏�ɒ��n�ł��Ă��邩�m�F���Ă��畜�A
                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position, out hit, 2.0f, NavMesh.AllAreas))
                {
                    transform.position = hit.position;
                    agent.enabled = true;
                }
            }
        }
    }

    // ���[�h��؂�ւ���
    private void ChangeState(EnemyState nextState)
    {
        currentState = nextState;
        if (!agent.enabled) return; // ���͂Ŕ��ł��鎞�̓G���[�h�~

        agent.isStopped = false;

        if (nextState == EnemyState.Wait)
        {
            agent.isStopped = true;
        }
        else if (nextState == EnemyState.Notice)
        {
            agent.isStopped = true;
            noticeTimer = noticeTime;
            if (markExclamation != null) markExclamation.SetActive(true);
            StartCoroutine(HideMark(markExclamation, noticeTime));
        }
        else if (nextState == EnemyState.Chase)
        {
            agent.isStopped = false;
        }
        else if (nextState == EnemyState.Search)
        {
            if (markQuestion != null) markQuestion.SetActive(true);
            StartCoroutine(HideMark(markQuestion, 1.5f));
            searchTimer = searchTime;
            WanderAround();
        }
        else if (nextState == EnemyState.Return)
        {
            agent.SetDestination(startPosition);
        }
    }

    private void WanderAround()
    {
        if (!agent.enabled) return;
        Vector3 randomPos = transform.position + Random.insideUnitSphere * searchRadius;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomPos, out hit, searchRadius, 1))
        {
            agent.SetDestination(hit.position);
        }
    }

    private IEnumerator HideMark(GameObject mark, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (mark != null) mark.SetActive(false);
    }

    private void Attack() { /* �U������ */ }

    public void TakeDamage(float damageAmount)
    {
        currentHp -= damageAmount;
        if (currentHp <= 0) Die();
    }

    private void Die()
    {
        Destroy(gameObject);
    }
}