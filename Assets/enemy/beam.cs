using UnityEngine;

public class beam : MonoBehaviour
{
    [SerializeField] private int attackDamage = 1;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnCollisionEnter(Collision collision)
    {
        var playerController = collision.gameObject.GetComponent<Controller>();
        if (playerController != null)
        {
            playerController.TakeDamage(attackDamage);
        }
        Destroy(gameObject);
    }
}
