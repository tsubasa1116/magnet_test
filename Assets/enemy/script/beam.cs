using UnityEngine;

public class beam : MonoBehaviour
{
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float shrinkSpeed = 0.0001f;  // ビームの縮小速度

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //Vector3 scale = transform.localScale;

        //scale.z -= shrinkSpeed * Time.deltaTime;
        //scale.z = Mathf.Max(0, scale.z);

        //transform.localScale = scale;

        //if (scale.z <= 0f)
        //{
        //    Destroy(gameObject);
        //}
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerHealth playerHealth = collision.gameObject.GetComponent<PlayerHealth>();
            Debug.Log(playerHealth);

            if (playerHealth != null) playerHealth.TakeDamage(attackDamage);

            Destroy(gameObject);
        }
    }
}
