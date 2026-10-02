using UnityEngine;

/// <summary>
/// A pickup dropped by enemies. Spins and bobs, then gives the player a bonus on touch.
/// Types: Health, DamageBoost, SpeedBoost, Stamina.
/// The pickup needs a Collider with "Is Trigger" ticked and a kinematic Rigidbody.
/// </summary>
public class PickupItem : MonoBehaviour
{
    public enum PickupType { Health, DamageBoost, SpeedBoost, Stamina }

    [Header("Pickup")]
    [SerializeField] private PickupType type = PickupType.Health;
    [Tooltip("Health/Stamina: amount restored. Boosts: multiplier (e.g. 1.5 = +50%).")]
    [SerializeField] private float amount = 25f;
    [Tooltip("How long a boost lasts (seconds). Ignored for Health/Stamina.")]
    [SerializeField] private float boostDuration = 8f;
    [Tooltip("Pickup disappears if not collected within this time. 0 = never.")]
    [SerializeField] private float lifetime = 15f;
    [Tooltip("Points awarded for collecting.")]
    [SerializeField] private int scoreValue = 10;

    [Header("Animation")]
    [SerializeField] private float rotateSpeed = 90f;
    [SerializeField] private float bobHeight = 0.25f;
    [SerializeField] private float bobSpeed = 2f;

    [Header("Effects & Audio Hooks (optional)")]
    [SerializeField] private GameObject pickupEffectPrefab;
    [SerializeField] private AudioClip pickupSound;

    private Vector3 startPosition;
    private bool collected;

    private void Start()
    {
        startPosition = transform.position;
        if (lifetime > 0f) Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        // Spin and float up and down.
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        float offset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = startPosition + Vector3.up * (offset + bobHeight);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        if (!other.CompareTag("Player")) return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player.IsDead) return;

        collected = true;
        Apply(player);

        GameManager.SpawnEffect(pickupEffectPrefab, transform.position, 2f);
        if (pickupSound != null) AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        if (GameManager.Instance != null) GameManager.Instance.AddScore(scoreValue);

        Destroy(gameObject);
    }

    private void Apply(PlayerController player)
    {
        string message = "";

        switch (type)
        {
            case PickupType.Health:
                player.Heal(amount);
                message = "+" + Mathf.RoundToInt(amount) + " HEALTH";
                break;

            case PickupType.Stamina:
                player.RestoreStamina(amount);
                message = "+" + Mathf.RoundToInt(amount) + " STAMINA";
                break;

            case PickupType.DamageBoost:
                PlayerCombat combat = player.GetComponent<PlayerCombat>();
                if (combat != null) combat.ApplyDamageBoost(amount, boostDuration);
                message = "DAMAGE x" + amount.ToString("0.0") + "!";
                break;

            case PickupType.SpeedBoost:
                player.ApplySpeedBoost(amount, boostDuration);
                message = "SPEED x" + amount.ToString("0.0") + "!";
                break;
        }

        if (UIManager.Instance != null) UIManager.Instance.ShowBanner(message);
    }
}
