using UnityEngine;

public class CircleRopeway : MonoBehaviour
{
    public Transform center;

    public float radius = 5f;
    public float speed = 1f;
    public float height = 5f;

    private float angle;

    void Update()
    {
        angle += speed * Time.deltaTime;

        float x = Mathf.Cos(angle) * radius;
        float z = Mathf.Sin(angle) * radius;

        transform.position =
            center.position + new Vector3(x, height, z);
    }
}