using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI; // NavMeshAgent���g������

[RequireComponent(typeof(NavMeshAgent))]
public class Boss : MonoBehaviour
{
	private enum BossState
	{
		Wait,
		Notice,
		Chase,
		Attack,
		Search,
		Return
	}

	[Header("�p�����[�^")]
	[SerializeField] private float maxHp = 500.0f;
	[SerializeField] private float found = 55.0f;
	[SerializeField] private float attackRange = 50.0f;
	[SerializeField] private float searchTime = 3.0f;
	[SerializeField] private float searchRadius = 5.0f;
	[SerializeField] private float noticeTime = 1.0f;

	[Header("�����i�{���j�ݒ�")]
	[SerializeField] private GameObject bombPrefab;
	[SerializeField] private float throwForce = 10.0f;
	[SerializeField] private float upwardForce = 6.0f;
	[SerializeField] private float throwCooldown = 2.0f;

	[Header("�Q��")]
	[SerializeField] private Transform targetPlayer;

	[Header("��e�E�X�^���ݒ�")]
	[Tooltip("�̓�����ȂǂŎ~�܂鎞�ԁi�b�j")]
	[SerializeField] private float stunDuration = 0.5f;
	[Tooltip("�Ռ���臒l�irelativeVelocity.magnitude�j: ����ȏ�Ȃ�X�^�����N����")]
	[SerializeField] private float stunVelocityThreshold = 1.0f;

	private float currentHp;
	private NavMeshAgent agent;

	private Vector3 startPosition;

	private BossState currentState = BossState.Wait;
	private float searchTimer;
	private float noticeTimer = 1.0f;

	private float nextThrowTime = 0f;

	private Rigidbody rb;
	private bool isStunned = false;
	private bool initialRbKinematic = true;

	void Start()
	{
		currentHp = maxHp;
		agent = GetComponent<NavMeshAgent>();

		rb = GetComponent<Rigidbody>();
		if (rb != null)
		{
			initialRbKinematic = rb.isKinematic;
			rb.constraints |= RigidbodyConstraints.FreezeRotation;
		}

		startPosition = transform.position;

		if (agent != null)
		{
			agent.updatePosition = true;
			agent.updateRotation = true;
		}
	}

	void Update()
	{
		if (targetPlayer == null) return;

		if (isStunned)
		{
			SafeSetAgentStopped(true);
			return;
		}

		float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);

		switch (currentState)
		{
			case BossState.Wait:
				if (distanceToPlayer <= found) ChangeState(BossState.Notice);
				break;

			case BossState.Notice:
				noticeTimer -= Time.deltaTime;
				if (noticeTimer <= 0) ChangeState(BossState.Chase);
				break;

			case BossState.Chase:
				if (distanceToPlayer <= attackRange) ChangeState(BossState.Attack);
				else if (distanceToPlayer > found + 5.0f) ChangeState(BossState.Search);
				else SafeSetDestination(targetPlayer.position);
				break;

			case BossState.Attack:
				if (distanceToPlayer > attackRange) ChangeState(BossState.Chase);
				else
				{
					SafeSetAgentStopped(true);
					Attack();
				}
				break;

			case BossState.Search:
				if (distanceToPlayer <= found) ChangeState(BossState.Notice);
				else
				{
					searchTimer -= Time.deltaTime;
					if (searchTimer <= 0) ChangeState(BossState.Return);
					else if (IsAgentUsable() && !agent.pathPending && agent.remainingDistance < 0.5f) WanderAround();
				}
				break;

			case BossState.Return:
				if (distanceToPlayer <= found) ChangeState(BossState.Notice);
				else if (IsAgentUsable() && !agent.pathPending && agent.remainingDistance < 0.5f) ChangeState(BossState.Wait);
				break;
		}

		if (!isStunned && bombPrefab != null && Time.time >= nextThrowTime)
		{
			ThrowBombAtPlayer();
			nextThrowTime = Time.time + throwCooldown;
		}
	}

	private void ChangeState(BossState nextState)
	{
		currentState = nextState;
		SafeSetAgentStopped(false);

		if (nextState == BossState.Wait)
		{
			SafeSetAgentStopped(true);
		}
		else if (nextState == BossState.Notice)
		{
			SafeSetAgentStopped(true);
			noticeTimer = noticeTime;
		}
		else if (nextState == BossState.Chase)
		{
			SafeSetAgentStopped(false);
		}
		else if (nextState == BossState.Search)
		{
			searchTimer = searchTime;
			WanderAround();
		}
		else if (nextState == BossState.Return)
		{
			SafeSetDestination(startPosition);
		}
	}

	private void WanderAround()
	{
		Vector3 randomPos = transform.position + Random.insideUnitSphere * searchRadius;
		NavMeshHit hit;
		if (NavMesh.SamplePosition(randomPos, out hit, searchRadius, NavMesh.AllAreas))
		{
			SafeSetDestination(hit.position);
		}
	}

	private void Attack()
	{
		if (bombPrefab == null)
		{
			Debug.LogWarning("[boss] Attack skipped: bombPrefab not assigned.");
			return;
		}

		Debug.Log("[boss] Attack() called. distance OK. time=" + Time.time + " nextThrowTime=" + nextThrowTime);

		if (Time.time >= nextThrowTime)
		{
			ThrowBombAtPlayer();
			nextThrowTime = Time.time + throwCooldown;
		}

		Vector3 lookDir = targetPlayer.position - transform.position;
		lookDir.y = 0;
		if (lookDir.sqrMagnitude > 0.001f)
		{
			transform.rotation = Quaternion.LookRotation(lookDir);
		}
	}

	private void ThrowBombAtPlayer()
	{
		if (bombPrefab == null)
		{
			Debug.LogWarning("[boss] ThrowBombAtPlayer aborted: bombPrefab not assigned.");
			return;
		}

		Vector3 spawnPos = transform.position;
		Debug.Log("[boss] Instantiate bomb at " + spawnPos);

		GameObject bombObj = Instantiate(bombPrefab, spawnPos, Quaternion.identity);
		bombObj.transform.SetParent(null);

		Rigidbody rb = bombObj.GetComponent<Rigidbody>();
		Bomb2 bombComp2 = bombObj.GetComponent<Bomb2>();
		Collider bombCol = bombObj.GetComponent<Collider>();
		Collider myCol = GetComponent<Collider>();

		Debug.Log("[boss] Bomb instantiated. rb=" + (rb != null) + " bombComp2=" + (bombComp2 != null) + " bombCol=" + (bombCol != null) + " myCol=" + (myCol != null));

		if (bombComp2 != null) bombComp2.Launch();
		else Debug.LogWarning("[boss] Bomb2 component not found on prefab. timeToExplode will not start.");

		if (bombCol != null && myCol != null)
		{
			Physics.IgnoreCollision(bombCol, myCol, true);
			StartCoroutine(ReenableCollision(bombCol, myCol, 0.5f));
		}
		else Debug.LogWarning("[boss] Could not IgnoreCollision: bombCol or myCol is null.");

		if (rb == null) Debug.LogWarning("[boss] Bomb has no Rigidbody; it will not move.");

		Vector3 toTarget = targetPlayer.position - spawnPos;
		Vector3 forwardDir = new Vector3(toTarget.x, 0f, toTarget.z).normalized;
		Vector3 force = forwardDir * throwForce + Vector3.up * upwardForce;

		if (rb != null) rb.AddForce(force, ForceMode.Impulse);
	}

	private IEnumerator ReenableCollision(Collider a, Collider b, float delay)
	{
		yield return new WaitForSeconds(delay);
		if (a != null && b != null) Physics.IgnoreCollision(a, b, false);
	}

	private void OnCollisionEnter(Collision collision)
	{
		if (!collision.gameObject.CompareTag("Player")) return;
		float impact = collision.relativeVelocity.magnitude;
		if (impact >= stunVelocityThreshold) StartCoroutine(ApplyStun(stunDuration));
	}

	private IEnumerator ApplyStun(float duration)
	{
		if (isStunned) yield break;
		isStunned = true;

		if (agent != null)
		{
			DisableAgentForPhysics();
			if (agent.isOnNavMesh) agent.ResetPath();
		}

		if (rb != null)
		{
			rb.linearVelocity = Vector3.zero;
			rb.isKinematic = true;
		}

		yield return new WaitForSeconds(duration);

		if (rb != null)
		{
			rb.linearVelocity = Vector3.zero;
			rb.isKinematic = initialRbKinematic;
			rb.constraints |= RigidbodyConstraints.FreezeRotation;
		}

		if (agent != null)
		{
			ReenableAgentAt(transform.position);
		}

		isStunned = false;
	}

	private void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.red;
		Gizmos.DrawWireSphere(transform.position, attackRange);

		Gizmos.color = Color.green;
		Gizmos.DrawWireSphere(transform.position, found);
	}

	public void TakeDamage(float damageAmount)
	{
		currentHp -= damageAmount;
		if (currentHp <= 0) Die();
	}

	private void Die()
	{
		// �����~
		StopAllCoroutines();

		// NavMeshAgent �𖳌������ē����Ăяo���G���[��h��
		if (agent != null)
		{
			try
			{
				agent.isStopped = true;
			}
			catch { /* �����ȏ�ԂȂ疳�� */ }

			agent.updatePosition = false;
			agent.updateRotation = false;
			agent.enabled = false;
		}

		// �������~�߂�
		if (rb != null)
		{
			rb.linearVelocity = Vector3.zero;
			rb.angularVelocity = Vector3.zero;
			rb.isKinematic = true;
		}

		// �K�v�Ȃ炱���ŃG�t�F�N�g��h���b�v�𐶐��i�C�Ӂj
		// e.g. if (deathEffect != null) Instantiate(deathEffect, transform.position, Quaternion.identity);

		Destroy(gameObject);
	}

	// ---------- ���S�w���p�[ ----------

	private void DisableAgentForPhysics()
	{
		if (agent == null) return;

		// ��� NavMeshAgent �̎����X�V�͎~�߂ĕ�������ɔC����
		agent.updatePosition = false;
		agent.updateRotation = false;

		// NavMesh ��ɔz�u����Ă��Ă��L���ȏꍇ�̂� isStopped �𑀍삷��
		if (agent.enabled && agent.isOnNavMesh)
		{
			agent.isStopped = true;
		}
		else
		{
			// �f�o�b�O�p�i�K�v�Ȃ�L���ɂ���j  �X�p���ɂȂ�ꍇ�̓R�����g�A�E�g��
			Debug.Log("[boss] DisableAgentForPhysics: agent not on NavMesh or disabled; skipping isStopped.");
		}
	}

	private void ReenableAgentAt(Vector3 desiredPosition)
	{
		if (agent == null) return;

		NavMeshHit hit;
		if (NavMesh.SamplePosition(desiredPosition, out hit, 3.0f, NavMesh.AllAreas))
		{
			transform.position = hit.position;
			agent.Warp(hit.position);
			agent.updatePosition = true;
			agent.updateRotation = true;
			agent.isStopped = false;
		}
		else
		{
			Debug.LogWarning("[boss] ReenableAgentAt: cannot find NavMesh near " + desiredPosition);
		}
	}

	private void SafeSetAgentStopped(bool stopped)
	{
		if (agent == null) return;
		if (!agent.isOnNavMesh) return;
		agent.isStopped = stopped;
	}

	private bool TryReenableAgentSafely()
	{
		if (agent == null) return false;

		if (agent.isOnNavMesh)
		{
			agent.isStopped = false;
			agent.updatePosition = true;
			agent.updateRotation = true;
			return true;
		}

		NavMeshHit hit;
		if (NavMesh.SamplePosition(transform.position, out hit, 3.0f, NavMesh.AllAreas))
		{
			transform.position = hit.position;
			agent.Warp(hit.position);
			agent.updatePosition = true;
			agent.updateRotation = true;
			agent.isStopped = false;
			return true;
		}

		return false;
	}

	private bool SafeSetDestination(Vector3 target)
	{
		if (agent == null) return false;
		if (agent.isOnNavMesh)
		{
			agent.SetDestination(target);
			return true;
		}

		if (TryReenableAgentSafely())
		{
			agent.SetDestination(target);
			return true;
		}

		return false;
	}

	// �ǉ��Fagent.remainingDistance �������S�ɎQ�Ƃł��邩
	private bool IsAgentUsable()
	{
		return agent != null && agent.enabled && agent.isOnNavMesh;
	}
}