using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bomb2 : MonoBehaviour
{
    public float explosionPower = 10f;
    public float explosionRadius = 3f;
    public float timeToExplode = 7.0f; // �����܂ł̕b��

    public bool isThrown = false;
    private bool isPolarityLocked = false; // �ɐ������b�N���邩�ǂ���
    private bool hasExploded = false;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // ������ꂽ��^�C�}�[�����炵�A0�ɂȂ����甚��
        if (isThrown && !hasExploded)
        {
            timeToExplode -= Time.deltaTime;

            if (timeToExplode <= 0f)
            {
                Explode();
            }
        }
    }

    // ���ǉ��F�����ɂԂ������犊��Ȃ��悤�ɋ}�u���[�L��������
    void OnCollisionEnter(Collision collision)
    {
        if (isThrown && !hasExploded && rb != null)
        {
            // �{�����n�ʂȂǂɓ���������A�]����Ɗ����}���邽�߂ɒ�R��傫������
            rb.linearDamping = 5f;          // �ړ��̒�R�����Ȃ苭������
            rb.angularDamping = 5f;   // ��]�̒�R���������ăR���R���]����̂��~�߂�

            // �������S�Ƀs�^�b�Ǝ~�߂����ꍇ�͈ȉ���2�s�̃R�����g�A�E�g���O���Ă�������
            // rb.velocity = Vector3.zero;
            // rb.angularVelocity = Vector3.zero;
        }
    }

    // ������ꂽ�u�ԂɌĂ΂��֐��i�v���C���[������Ăԁj
    public void Launch()
    {
        isThrown = true;
        isPolarityLocked = true; // �������̂ŋɐ����m��i���b�N�j����

        // ������u�Ԃɋ�C��R�����Z�b�g�i�����悭��΂����߁j
        if (rb != null)
        {
            rb.linearDamping = 0f;
            rb.angularDamping = 0.05f;
        }
    }

    // �ɐ���ݒ肷����J�֐��i�����K�v�ȏꍇ�j
    public void SetPolarity(string newTag)
    {
        // ���b�N����Ă�����ύX������
        if (isPolarityLocked) return;

        if (newTag == "N_Pole" || newTag == "S_Pole")
        {
            gameObject.tag = newTag;
        }
    }

    void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);

        foreach (Collider hit in hits)
        {
            Rigidbody hitRb = hit.GetComponent<Rigidbody>();
            if (hitRb != null)
            {
                hitRb.AddExplosionForce(explosionPower, transform.position, explosionRadius);
            }

            // ���ύX�F�����Ɋ������܂ꂽ�G(enemy)�͊m���ɑ���������
            enemy targetEnemy = hit.GetComponent<enemy>();
            if (targetEnemy != null)
            {
                // 1���œ|�����߁AHP�̍ő�l�ȏ�̋���ȃ_���[�W�𑗂�
                targetEnemy.TakeDamage(9999f);
            }

            // ���ǉ��F�{�X�Ƀ_���[�W��^����i�Œ�_���[�W�̗�Ƃ���50�j
            Boss targetBoss = hit.GetComponent<Boss>();
            if (targetBoss != null)
            {
                targetBoss.TakeDamage(50f); 
            }

            // ���ǉ��F�v���C���[�Ƀ_���[�W��^����i�Œ�_���[�W�̗�Ƃ���1�j
            Controller targetPlayer = hit.GetComponent<Controller>();
            if (targetPlayer != null)
            {
                targetPlayer.TakeDamage(1); 
            }
        }

        Destroy(gameObject);
    }
}