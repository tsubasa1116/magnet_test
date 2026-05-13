using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class dash : MonoBehaviour
{
    public AudioClip dashSound;

    // �K�E�X�����̂悤�Ȑ�����΂�������
    [SerializeField] private float dashForce = 30.0f;

    // Y���i������j�ɏ������������ (�n�ʂɈ��������炸�ɔ�Ԃ���)
    [SerializeField] private float liftForce = 2.0f;

    /// <summary>
    /// Collider�����̃g���K�[�ɓ��������ɌĂяo�����
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // ������������̃^�O��"Player"�������ꍇ
        if (other.gameObject.CompareTag("Player"))
        {
            // �v���C���[�������Ă��鎥�΃X�N���v�g���擾
            magnet playerMagnet = other.GetComponentInChildren<magnet>();

            if (playerMagnet != null)
            {
				if (!playerMagnet.isActive)
				{
					return;
				}

				// �d�|�����g�̋ɐ����^�O�Ŕ���
				bool isThisN = gameObject.CompareTag("N_Pole");
                bool isThisS = gameObject.CompareTag("S_Pole");

                // �v���C���[�̌��݂̃��[�h
                int mode = playerMagnet.magnetMode;

                // �y�����ɓ��m�������ꍇ�z���ɐ����悭�e���i�����j
                if ((mode == 1 && isThisN) || (mode == 2 && isThisS))
                {
                    // �����ݒ肳��Ă���Ζ炷
                    if (dashSound != null)
                    {
                        AudioSource.PlayClipAtPoint(dashSound, transform.position);
                    }

                    Rigidbody playerRb = other.GetComponent<Rigidbody>();
                    if (playerRb != null)
                    {
                        // �������Ȃǂ̑��x����x���Z�b�g����i���肵�Ĕ�΂����߁j
                        playerRb.linearVelocity = Vector3.zero;

                        // ���ύX�F�v���C���[���������Ă���O���x�N�g���́u�t�i�}�C�i�X�j�v���擾
                        Vector3 backwardDir = -other.transform.forward;

                        // ����ɋ����A���������(liftForce)�Ŏ΂ߌ��ɑł��o��
                        Vector3 launchDirection = (backwardDir * dashForce) + (Vector3.up * liftForce);

                        // �v���C���[�ɗ͂�������
                        playerRb.AddForce(launchDirection, ForceMode.Impulse);
                    }

                    // �v���C���[��Controller�Ɂu�W�����v�i�󒆁j���v�ł��邱�Ƃ�`����
                    Controller playerCtrl = other.GetComponent<Controller>();
                    if (playerCtrl != null)
                    {
                        playerCtrl.isJumping = true;
                    }
                }
            }
        }
    }

    // ������܂܂Ń��[�h���ォ��؂�ւ������ɂ���Ԃ悤�� Stay ���ǉ�
    private void OnTriggerStay(Collider other)
    {
        OnTriggerEnter(other);
    }
}