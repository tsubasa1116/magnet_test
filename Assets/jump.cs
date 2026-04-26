using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class jump : MonoBehaviour
{
    public AudioClip jumpSound;

    // �W�����v����́i������̗́j���`
    [SerializeField] private float jumpForceX = 0f;
    [SerializeField] private float jumpForceY = 15.0f; // ��Impulse�Ŕ�΂��Ȃ�15�`20���炢�Ŕ�т܂�
    [SerializeField] private float jumpForceZ = 0f;

    /// <summary>
    /// Collider�����̃g���K�[�ɓ��������ɌĂяo�����
    /// </summary>
    /// <param name="other">������������̃I�u�W�F�N�g</param>
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

				// �����g�̋ɐ����^�O�Ŕ���
				bool isThisN = gameObject.CompareTag("N_Pole");
                bool isThisS = gameObject.CompareTag("S_Pole");

                // �v���C���[�̌��݂̃��[�h
                int mode = playerMagnet.magnetMode;

                // �y�����ɓ��m�������ꍇ�z��ɃW�����v������
                if ((mode == 1 && isThisN) || (mode == 2 && isThisS))
                {
                    // �����ݒ肳��Ă���Ζ炷
                    if (jumpSound != null)
                    {
                        AudioSource.PlayClipAtPoint(jumpSound, transform.position);
                    }

                    Rigidbody playerRb = other.GetComponent<Rigidbody>();
                    if (playerRb != null)
                    {
                        // �������̑��x����x���Z�b�g����ƁA������肵�������܂Ŕ�т܂�
                        Vector3 vel = playerRb.linearVelocity;
                        vel.y = 0;
                        playerRb.linearVelocity = vel;

                        // �v���C���[�ɏ�����̗͂�������
                        playerRb.AddForce(new Vector3(jumpForceX, jumpForceY, jumpForceZ), ForceMode.Impulse);
                    }

                    // �v���C���[��Controller�Ɂu�W�����v���v�ł��邱�Ƃ�`����
                    Controller playerCtrl = other.GetComponent<Controller>();
                    if (playerCtrl != null)
                    {
                        playerCtrl.isJumping = true;
                    }
                }
            }
        }
    }

    // ���ɏ�����܂܂Ń��[�h���ォ��؂�ւ������ɂ���Ԃ悤�� Stay ���ǉ�
    private void OnTriggerStay(Collider other)
    {
        OnTriggerEnter(other);
    }
}