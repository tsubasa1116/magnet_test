using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bomb : MonoBehaviour
{
    public float explosionPower = 10f;
    public float explosionRadius = 3f;

    public bool isThrown = false;

    void OnCollisionEnter(Collision collision)
    {
        // “Š‚°‚Ä‚È‚¢Žž‚Í–³Ž‹
        if (!isThrown) return;

        Explode();
    }

    void Explode()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);

        foreach (Collider hit in hits)
        {
            Rigidbody rb = hit.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddExplosionForce(explosionPower, transform.position, explosionRadius);
            }
        }

        Destroy(gameObject);
    }
}