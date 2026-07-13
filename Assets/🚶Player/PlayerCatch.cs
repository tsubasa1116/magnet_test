using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerCatch : MonoBehaviour
{
    private InputAction catchAction;
    private PlayerHealth health;

    public bool IsCatching { get; private set; }

    void Awake()
    {
        catchAction = GetComponent<PlayerInput>().actions["Magnet ON OFF"];
        health = GetComponent<PlayerHealth>();
    }

    void OnEnable()
    {
        if (health != null)
            health.OnDied += OnDied;
    }

    void OnDisable()
    {
        if (health != null)
            health.OnDied -= OnDied;
    }

    void Update()
    {
        if (health != null && health.IsDead)
        {
            IsCatching = false;
            return;
        }

        IsCatching = catchAction != null && catchAction.IsPressed();
    }

    private void OnDied()
    {
        IsCatching = false;
    }
}