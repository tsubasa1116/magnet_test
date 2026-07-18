using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerCatch : MonoBehaviour
{
    private InputAction catchAction;
    private PlayerHealth health;
    [Header("SE")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip magnetOnSE;

    public bool IsCatching { get; private set; }
    private bool wasCatching;

    void Awake()
    {
        catchAction = GetComponent<PlayerInput>().actions["Magnet ON OFF"];
        health = GetComponent<PlayerHealth>();

        audioSource = GetComponent<AudioSource>();
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

        bool nowCatching = catchAction != null && catchAction.IsPressed();

        if (!wasCatching && nowCatching)
        {
            if (audioSource != null && magnetOnSE != null)
            {
                audioSource.PlayOneShot(magnetOnSE);
            }
        }

        IsCatching = nowCatching;
        wasCatching = nowCatching;
    }

    private void OnDied()
    {
        IsCatching = false;
    }
}